from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
import PyPDF2
import base64
import io
import logging
from typing import Optional, List, Dict
import asyncio
from concurrent.futures import ThreadPoolExecutor
import time
from transformers import pipeline
import hashlib

# Importar novos módulos
from redis_client import initialize_redis, get_cache, set_cache, get_cache_stats
from ner_extractor import initialize_ner, extract_entities, enrich_entities_with_patterns, extract_structured_patterns
from embed_matcher import initialize_embeddings, match_fields_with_embeddings
from gpt_fallback import initialize_openai, call_gpt_fallback, estimate_gpt_cost

# Configuração de logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Inicializar FastAPI
app = FastAPI(
    title="PDF Text Extractor & Smart Extraction API",
    description="API com NER, Embeddings, Cache Redis e Zero-Shot Classification",
    version="4.0.0"
)

# Inicializar componentes na inicialização da aplicação
@app.on_event("startup")
async def startup_event():
    """Inicializa todos os componentes ML na inicialização"""
    logger.info("🚀 Iniciando aplicação...")
    
    # 1. Redis
    redis_ok = initialize_redis()
    if not redis_ok:
        logger.warning("⚠️ Redis não disponível - cache desabilitado")
    
    # 2. NER (spaCy)
    ner_ok = initialize_ner()
    if not ner_ok:
        logger.warning("⚠️ NER não disponível - fallback para regex")
    
    # 3. Embeddings (sentence-transformers)
    embed_ok = initialize_embeddings()
    if not embed_ok:
        logger.warning("⚠️ Embeddings não disponível")
    
    # 4. OpenAI (opcional para fallback)
    openai_ok = initialize_openai()
    if not openai_ok:
        logger.warning("⚠️ OpenAI não disponível - fallback GPT desabilitado")
    
    # 5. Zero-Shot Classification
    logger.info("🤖 Carregando modelo Zero-Shot Classification...")
    global zero_shot_classifier
    try:
        zero_shot_classifier = pipeline(
            "zero-shot-classification",
            model="MoritzLaurer/mDeBERTa-v3-base-xnli-multilingual-nli-2mil7",
            device=-1  # CPU (-1), para GPU use 0
        )
        logger.info("✅ Modelo Zero-Shot carregado com sucesso!")
    except Exception as e:
        logger.error(f"❌ Erro ao carregar modelo: {e}")
        zero_shot_classifier = None
    
    # 6. Semantic Embeddings (para /semantic-extract)
    logger.info("🧠 Carregando modelo de embeddings para extração semântica...")
    global semantic_embeddings_model
    try:
        from sentence_transformers import SentenceTransformer
        semantic_embeddings_model = SentenceTransformer('paraphrase-multilingual-MiniLM-L12-v2')
        logger.info("✅ Modelo de embeddings carregado com sucesso!")
        logger.info("   Modelo: paraphrase-multilingual-MiniLM-L12-v2")
    except Exception as e:
        logger.error(f"❌ Erro ao carregar modelo de embeddings: {e}")
        semantic_embeddings_model = None
    
    logger.info("✅ Aplicação iniciada com sucesso!")

# Thread pool para operações de I/O bloqueantes
executor = ThreadPoolExecutor(max_workers=4)

# Variável global para zero-shot
zero_shot_classifier = None

# Variável global para embeddings (semantic extraction)
semantic_embeddings_model = None

# Modelos Pydantic para validação
class PDFRequest(BaseModel):
    pdf_base64: str = Field(..., description="PDF codificado em base64")

class PDFResponse(BaseModel):
    text: str = Field(..., description="Texto extraído do PDF")
    char_count: int = Field(..., description="Número de caracteres extraídos")
    success: bool = Field(default=True, description="Status da operação")

class ErrorResponse(BaseModel):
    error: str = Field(..., description="Mensagem de erro")
    success: bool = Field(default=False, description="Status da operação")

class HealthResponse(BaseModel):
    status: str = Field(default="healthy", description="Status da API")

# Modelos para Zero-Shot Classification
class ZeroShotRequest(BaseModel):
    text: str = Field(..., description="Texto a ser classificado", max_length=512)
    candidate_labels: List[str] = Field(..., description="Lista de labels candidatas", min_items=1)
    hypothesis_template: Optional[str] = Field(
        default="Este texto é sobre {}",
        description="Template para construir hipóteses"
    )
    multi_label: bool = Field(default=False, description="Se True, permite múltiplas labels")

class ZeroShotResponse(BaseModel):
    text: str = Field(..., description="Texto classificado")
    labels: List[str] = Field(..., description="Labels ordenadas por score")
    scores: List[float] = Field(..., description="Scores de confiança (0.0 a 1.0)")
    best_label: str = Field(..., description="Label com maior score")
    best_score: float = Field(..., description="Score da melhor label")
    success: bool = Field(default=True, description="Status da operação")

class BinaryClassificationRequest(BaseModel):
    text: str = Field(..., description="Texto a ser validado", max_length=512)
    category: str = Field(..., description="Categoria a validar")
    hypothesis_template: Optional[str] = Field(
        default="Este texto é {}",
        description="Template para construir hipótese"
    )

class BinaryClassificationResponse(BaseModel):
    text: str = Field(..., description="Texto analisado")
    category: str = Field(..., description="Categoria testada")
    is_category: bool = Field(..., description="Se o texto pertence à categoria")
    confidence: float = Field(..., description="Score de confiança (0.0 a 1.0)")
    success: bool = Field(default=True, description="Status da operação")

# ============================================================================
# MODELOS PARA /nli/classify (FASE 2 - Remoção de Labels)
# ============================================================================

class NliClassifyRequest(BaseModel):
    label: Optional[str] = Field(None, description="Label do documento (para cache)")
    schema: Dict[str, str] = Field(..., description="Schema com {campo: descrição}")
    text_blocks: List[str] = Field(..., description="Lista de blocos de texto para classificar")

class ClassifiedBlock(BaseModel):
    text: str = Field(..., description="Texto do bloco")
    label: str = Field(..., description="'label' ou 'valor'")
    confidence: float = Field(..., description="Confiança da classificação")

class NliClassifyResponse(BaseModel):
    labels_detected: List[str] = Field(..., description="Blocos identificados como labels")
    classified_blocks: List[ClassifiedBlock] = Field(..., description="Todos os blocos classificados")
    processing_time_ms: int = Field(..., description="Tempo de processamento")
    cache_hits: int = Field(0, description="Quantos blocos vieram do cache")
    total_blocks: int = Field(..., description="Total de blocos processados")

# ============================================================================
# MODELOS PARA /semantic-extract (Extração Semântica)
# ============================================================================

class SemanticExtractRequest(BaseModel):
    labels: Dict[str, str] = Field(..., description="Dicionário com {campo: descrição}")
    text: str = Field(..., description="Texto não estruturado do documento")
    top_k: int = Field(3, description="Quantidade de top matches para retornar", ge=1, le=10)
    min_token_length: int = Field(2, description="Tamanho mínimo de tokens para considerar", ge=1)
    similarity_threshold: float = Field(0.0, description="Score mínimo de similaridade (0-1)", ge=0.0, le=1.0)

class SemanticMatch(BaseModel):
    text: str = Field(..., description="Texto candidato")
    score: float = Field(..., description="Score de similaridade (0-1)")
    rank: int = Field(..., description="Ranking (1, 2, 3...)")

class LabelExtractionResult(BaseModel):
    label: str = Field(..., description="Nome do campo")
    description: str = Field(..., description="Descrição do campo")
    top_matches: List[SemanticMatch] = Field(..., description="Top K candidatos")
    best_match: str = Field(..., description="Melhor candidato (maior score)")
    best_score: float = Field(..., description="Score do melhor candidato")

class SemanticExtractResponse(BaseModel):
    results: List[LabelExtractionResult] = Field(..., description="Resultados por label")
    extraction_summary: Dict[str, str] = Field(..., description="Extração final sugerida {campo: valor}")
    processing_time_ms: int = Field(..., description="Tempo de processamento")
    total_candidates: int = Field(..., description="Total de candidatos avaliados")
    model_used: str = Field(..., description="Modelo de embedding usado")

# ============================================================================
# MODELOS PARA /semantic-label-detect (Detecção de Labels no Texto)
# ============================================================================

class SemanticLabelDetectRequest(BaseModel):
    labels: Dict[str, str] = Field(..., description="Schema com {campo: descrição}")
    text: str = Field(..., description="Texto do documento")
    top_k: int = Field(3, description="Quantidade de top labels por candidato", ge=1, le=10)
    min_token_length: int = Field(3, description="Tamanho mínimo de tokens", ge=1)
    similarity_threshold: float = Field(0.5, description="Score mínimo (0-1)", ge=0.0, le=1.0)

class CandidateLabelMatch(BaseModel):
    candidate_text: str = Field(..., description="Texto candidato do documento")
    matched_label: str = Field(..., description="Label do schema detectada")
    score: float = Field(..., description="Score de similaridade (0-1)")
    rank: int = Field(..., description="Ranking do match")

class SemanticLabelDetectResponse(BaseModel):
    detected_labels: List[CandidateLabelMatch] = Field(..., description="Labels detectadas no texto")
    labels_summary: Dict[str, str] = Field(..., description="Mapeamento {candidato: label}")
    processing_time_ms: int = Field(..., description="Tempo de processamento")
    total_candidates: int = Field(..., description="Total de candidatos avaliados")
    model_used: str = Field(..., description="Modelo usado")

# ============================================================================
# MODELOS PARA /smart-extract (FASE 2.5 - Smart Extract)
# ============================================================================

class SmartExtractRequest(BaseModel):
    label: Optional[str] = Field(None, description="Label do documento (para cache e memória)")
    text: str = Field(..., description="Texto limpo (pós-FASE 2)")
    schema: Dict[str, str] = Field(..., description="Schema com {campo: descrição} - ORDEM IMPORTA! (sequencial)")
    confidence_threshold: float = Field(0.7, description="Confiança mínima para aceitar resultado")
    enable_gpt_fallback: bool = Field(False, description="Habilitar fallback para GPT se confiança baixa")

class FieldExtraction(BaseModel):
    value: Optional[str] = Field(None, description="Valor extraído (pode ser None se não encontrado)")
    confidence: float = Field(..., description="Confiança da extração (0-1)")
    method: str = Field(..., description="Método usado (ner, embeddings, pattern, gpt_fallback)")
    line_index: Optional[int] = Field(None, description="Índice da linha de origem")

class SmartExtractResponse(BaseModel):
    fields: Dict[str, Optional[str]] = Field(..., description="Campos extraídos com seus valores")

def extract_text_from_pdf(pdf_bytes: bytes) -> str:
    """Extrai texto de bytes de um arquivo PDF (função síncrona)."""
    start_time = time.time()
    try:
        logger.info(f"📄 Iniciando extração de PDF ({len(pdf_bytes)} bytes)")
        
        pdf_file = io.BytesIO(pdf_bytes)
        reader = PyPDF2.PdfReader(pdf_file)
        
        num_pages = len(reader.pages)
        logger.info(f"📖 PDF contém {num_pages} página(s)")
        
        text = ""
        for page_num in range(num_pages):
            page_start = time.time()
            page_text = reader.pages[page_num].extract_text()
            text += page_text + "\n"
            page_time = time.time() - page_start
            logger.info(f"  ✓ Página {page_num + 1}/{num_pages} processada em {page_time:.3f}s ({len(page_text)} caracteres)")
        
        total_time = time.time() - start_time
        logger.info(f"✅ Extração completa em {total_time:.3f}s - Total: {len(text.strip())} caracteres")
        
        return text.strip()
    except Exception as e:
        error_time = time.time() - start_time
        logger.error(f"❌ Erro ao extrair texto do PDF após {error_time:.3f}s: {e}")
        raise

async def extract_text_from_pdf_async(pdf_bytes: bytes) -> str:
    """Wrapper assíncrono para extração de texto do PDF."""
    loop = asyncio.get_event_loop()
    text = await loop.run_in_executor(executor, extract_text_from_pdf, pdf_bytes)
    return text

@app.get('/health', response_model=HealthResponse, tags=["Health"])
async def health():
    """Health check endpoint."""
    model_status = "loaded" if zero_shot_classifier is not None else "not_loaded"
    embeddings_status = "loaded" if semantic_embeddings_model is not None else "not_loaded"
    return {
        "status": "healthy",
        "zero_shot_model": model_status,
        "embeddings_model": embeddings_status
    }

@app.post('/cache/clear', tags=["Cache"])
async def clear_cache():
    """Limpa todo o cache Redis (use com cuidado!)"""
    from redis_client import redis_client
    if redis_client:
        redis_client.flushdb()
        logger.info("🗑️ Cache Redis limpo!")
        return {"status": "ok", "message": "Cache limpo com sucesso"}
    return {"status": "error", "message": "Redis não disponível"}

@app.post('/zero-shot/classify', response_model=ZeroShotResponse, tags=["Zero-Shot Classification"])
async def zero_shot_classify(request: ZeroShotRequest):
    """
    Classifica um texto usando Zero-Shot Classification.
    
    **Parâmetros:**
    - **text**: Texto a ser classificado (max 512 caracteres)
    - **candidate_labels**: Lista de labels candidatas (ex: ["nome de pessoa", "número", "endereço"])
    - **hypothesis_template**: Template para construir hipóteses (padrão: "Este texto é sobre {}")
    - **multi_label**: Se True, permite múltiplas labels simultâneas
    
    **Retorna:**
    - **labels**: Labels ordenadas por score (maior para menor)
    - **scores**: Scores de confiança correspondentes
    - **best_label**: Label com maior score
    - **best_score**: Score da melhor label
    
    **Exemplo:**
    ```json
    {
        "text": "JOANA D'ARC",
        "candidate_labels": ["nome de pessoa", "número", "endereço", "data"]
    }
    ```
    """
    logger.info(f"🔍 Zero-Shot Classification: '{request.text[:50]}...' com {len(request.candidate_labels)} labels")
    
    if zero_shot_classifier is None:
        raise HTTPException(
            status_code=503,
            detail="Modelo Zero-Shot não está carregado"
        )
    
    try:
        start_time = time.time()
        
        # Executar classificação
        result = zero_shot_classifier(
            request.text,
            request.candidate_labels,
            hypothesis_template=request.hypothesis_template,
            multi_label=request.multi_label
        )
        
        elapsed = time.time() - start_time
        
        logger.info(f"✅ Classificação concluída em {elapsed:.3f}s - Melhor: '{result['labels'][0]}' ({result['scores'][0]:.3f})")
        
        return ZeroShotResponse(
            text=request.text,
            labels=result['labels'],
            scores=result['scores'],
            best_label=result['labels'][0],
            best_score=result['scores'][0],
            success=True
        )
        
    except Exception as e:
        logger.error(f"❌ Erro na classificação: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Erro ao classificar: {str(e)}"
        )

@app.post('/zero-shot/validate', response_model=BinaryClassificationResponse, tags=["Zero-Shot Classification"])
async def zero_shot_validate(request: BinaryClassificationRequest):
    """
    Valida se um texto pertence a uma categoria específica (classificação binária).
    
    **Parâmetros:**
    - **text**: Texto a ser validado
    - **category**: Categoria a testar (ex: "nome de pessoa")
    - **hypothesis_template**: Template para construir hipótese (padrão: "Este texto é {}")
    
    **Retorna:**
    - **is_category**: True se o texto pertence à categoria
    - **confidence**: Score de confiança (0.0 a 1.0)
    
    **Exemplo:**
    ```json
    {
        "text": "JOANA D'ARC",
        "category": "nome de pessoa"
    }
    ```
    
    **Resposta:**
    ```json
    {
        "is_category": true,
        "confidence": 0.95
    }
    ```
    """
    logger.info(f"✓ Validação binária: '{request.text[:50]}...' é '{request.category}'?")
    
    if zero_shot_classifier is None:
        raise HTTPException(
            status_code=503,
            detail="Modelo Zero-Shot não está carregado"
        )
    
    try:
        start_time = time.time()
        
        # Classificar entre a categoria e "outro"
        result = zero_shot_classifier(
            request.text,
            [request.category, "outro"],
            hypothesis_template=request.hypothesis_template,
            multi_label=False
        )
        
        elapsed = time.time() - start_time
        
        # O texto pertence à categoria se ela for a melhor label
        is_category = result['labels'][0] == request.category
        confidence = result['scores'][0] if is_category else (1 - result['scores'][0])
        
        logger.info(f"{'✅' if is_category else '❌'} Validação em {elapsed:.3f}s - Confiança: {confidence:.3f}")
        
        return BinaryClassificationResponse(
            text=request.text,
            category=request.category,
            is_category=is_category,
            confidence=confidence,
            success=True
        )
        
    except Exception as e:
        logger.error(f"❌ Erro na validação: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Erro ao validar: {str(e)}"
        )

@app.post('/nli/classify', response_model=NliClassifyResponse, tags=["NLI Classification"])
async def nli_classify(request: NliClassifyRequest):
    """
    🎯 FASE 2 - Classificação NLI para remover labels
    
    Classifica blocos de texto como "label" ou "valor" usando Zero-Shot NLI.
    Otimizado com:
    - Processamento em batch
    - Cache Redis
    - Pré-filtragem com embeddings (futuro)
    
    **Parâmetros:**
    - **label**: Label do documento (para cache)
    - **schema**: Dict {campo: descrição} dos campos pendentes
    - **text_blocks**: Lista de blocos de texto para classificar
    
    **Retorna:**
    - **labels_detected**: Blocos identificados como labels (para remover)
    - **classified_blocks**: Todos os blocos com classificação
    - **processing_time_ms**: Tempo total
    - **cache_hits**: Quantos vieram do cache
    
    **Exemplo:**
    ```json
    {
        "schema": {
            "nome_completo": "Nome completo",
            "endereco": "Endereço completo"
        },
        "text_blocks": [
            "Nome Completo:",
            "JOANA D'ARC",
            "Endereço:",
            "Rua ABC, 123"
        ]
    }
    ```
    """
    start_time = time.time()
    
    logger.info("="*60)
    logger.info(f"🏷️ NLI Classify iniciado")
    logger.info(f"📋 Schema: {len(request.schema)} campos")
    logger.info(f"📦 Blocos: {len(request.text_blocks)}")
    logger.info(f"🏷️ Label: {request.label or 'N/A'}")
    
    if zero_shot_classifier is None:
        raise HTTPException(
            status_code=503,
            detail="Modelo Zero-Shot não está carregado"
        )
    
    try:
        # 1️⃣ Construir candidatos a partir do schema
        candidate_labels = []
        for field_name in request.schema.keys():
            candidate_labels.append(f"label do campo '{field_name}'")
        candidate_labels.append("valor ou dado extraído")
        
        logger.info(f"🎯 Candidatos: {len(candidate_labels)}")
        for label in candidate_labels:
            logger.info(f"  • {label}")
        
        # 2️⃣ Processar blocos (com cache Redis futuro)
        classified_blocks = []
        labels_detected = []
        cache_hits = 0
        
        logger.info(f"🔄 Processando {len(request.text_blocks)} blocos...")
        
        # Processar em batch (otimização)
        for block in request.text_blocks:
            if not block.strip():
                continue
            
            # TODO: Adicionar cache Redis aqui
            # cache_key = f"nli:{hash(block)}:{hash(tuple(candidate_labels))}"
            # cached = get_cache(cache_key)
            # if cached: ...
            
            # Classificar bloco
            result = zero_shot_classifier(
                block,
                candidate_labels,
                hypothesis_template="Este texto é {}",
                multi_label=False
            )
            
            best_label = result['labels'][0]
            best_score = result['scores'][0]
            
            # Se NÃO for "valor ou dado extraído" → é label
            is_label = best_label != "valor ou dado extraído" and best_score > 0.30
            
            classified_blocks.append(ClassifiedBlock(
                text=block,
                label="label" if is_label else "valor",
                confidence=best_score
            ))
            
            if is_label:
                labels_detected.append(block)
                logger.info(f"  🏷️ Label: '{block}' → {best_label} ({best_score:.2f})")
            else:
                logger.info(f"  ✅ Valor: '{block}' ({best_score:.2f})")
        
        elapsed_ms = int((time.time() - start_time) * 1000)
        
        logger.info(f"✅ NLI Classify completo em {elapsed_ms}ms")
        logger.info(f"📊 Labels detectadas: {len(labels_detected)}/{len(request.text_blocks)}")
        logger.info(f"💾 Cache hits: {cache_hits}")
        logger.info("="*60)
        
        return NliClassifyResponse(
            labels_detected=labels_detected,
            classified_blocks=classified_blocks,
            processing_time_ms=elapsed_ms,
            cache_hits=cache_hits,
            total_blocks=len(request.text_blocks)
        )
        
    except Exception as e:
        elapsed_ms = int((time.time() - start_time) * 1000)
        logger.error(f"❌ Erro no NLI Classify após {elapsed_ms}ms: {e}")
        logger.info("="*60)
        raise HTTPException(
            status_code=500,
            detail=str(e)
        )

# ============================================================================
# ENDPOINT: /semantic-extract (Extração Semântica com Embeddings)
# ============================================================================

@app.post('/semantic-extract', response_model=SemanticExtractResponse, tags=["Semantic Extraction"])
async def semantic_extract(request: SemanticExtractRequest):
    """
    🎯 Extração Semântica com Embeddings
    
    Encontra os top K matches semânticos para cada label usando embeddings multilíngues.
    
    **Como funciona:**
    1. Divide o texto em candidatos (linhas e tokens grandes)
    2. Gera embeddings para as descrições dos labels
    3. Gera embeddings para todos os candidatos
    4. Calcula similaridade cosine entre labels e candidatos
    5. Retorna top K matches para cada label
    
    **Parâmetros:**
    - **labels**: Dicionário {campo: descrição} dos campos a extrair
    - **text**: Texto não estruturado do documento
    - **top_k**: Quantidade de top matches (padrão: 3)
    - **min_token_length**: Tamanho mínimo de tokens (padrão: 2)
    - **similarity_threshold**: Score mínimo (0-1, padrão: 0.0)
    
    **Retorna:**
    - **results**: Lista com top K matches para cada label
    - **extraction_summary**: Melhores valores extraídos {campo: valor}
    - **processing_time_ms**: Tempo de processamento
    
    **Exemplo:**
    ```json
    {
        "labels": {
            "nome": "Nome do profissional, normalmente no canto superior esquerdo",
            "inscricao": "Número de inscrição do profissional"
        },
        "text": "JOANA D'ARC\\nInscrição: 101943\\nPR"
    }
    ```
    """
    start_time = time.time()
    
    try:
        logger.info("="*80)
        logger.info("🎯 SEMANTIC EXTRACT - Extração Semântica Iniciada")
        logger.info(f"📋 Labels: {len(request.labels)}")
        logger.info(f"📄 Texto: {len(request.text)} caracteres")
        logger.info(f"🔝 Top K: {request.top_k}")
        logger.info("="*80)
        
        # 1️⃣ Preparar candidatos do texto
        logger.info("📝 Preparando candidatos do texto...")
        candidates = set()
        
        # Adicionar linhas não vazias
        lines = [line.strip() for line in request.text.split('\n') if line.strip()]
        candidates.update(lines)
        logger.info(f"   • {len(lines)} linhas adicionadas")
        
        # Adicionar tokens grandes de cada linha (palavras compostas, números, etc)
        for line in lines:
            tokens = line.split()
            large_tokens = [
                token.strip() 
                for token in tokens 
                if len(token.strip()) >= request.min_token_length
            ]
            candidates.update(large_tokens)
        
        candidates = list(candidates)
        logger.info(f"   ✓ Total de candidatos: {len(candidates)}")
        
        if not candidates:
            raise HTTPException(
                status_code=400,
                detail="Nenhum candidato válido encontrado no texto"
            )
        
        # 2️⃣ Gerar embeddings
        logger.info("🧠 Gerando embeddings...")
        
        # Verificar se modelo está carregado
        if semantic_embeddings_model is None:
            raise HTTPException(
                status_code=503,
                detail="Modelo de embeddings não está carregado. Reinicie a aplicação."
            )
        
        from sentence_transformers import util
        model = semantic_embeddings_model
        logger.info("   ✓ Usando modelo pré-carregado: paraphrase-multilingual-MiniLM-L12-v2")
        
        # Embeddings das descrições dos labels
        label_descriptions = [desc for desc in request.labels.values()]
        label_names = list(request.labels.keys())
        
        logger.info(f"   • Gerando embeddings para {len(label_descriptions)} labels...")
        label_embeddings = model.encode(label_descriptions, convert_to_tensor=True)
        
        logger.info(f"   • Gerando embeddings para {len(candidates)} candidatos...")
        candidate_embeddings = model.encode(candidates, convert_to_tensor=True)
        
        logger.info("   ✓ Embeddings gerados com sucesso")
        
        # 3️⃣ Calcular similaridades e extrair top K para cada label
        logger.info("🔍 Calculando similaridades...")
        results = []
        extraction_summary = {}
        
        # CORREÇÃO: Processar cada label independentemente
        for label_idx, (label_name, label_desc) in enumerate(request.labels.items()):
            logger.info(f"\n📋 {label_name.upper()} (descrição: '{label_desc[:50]}...')")
            
            # CRÍTICO: Pegar o embedding correto para ESTE label específico
            label_embedding = label_embeddings[label_idx]
            
            # Calcular similaridade cosine APENAS para este label
            similarities = util.cos_sim(label_embedding, candidate_embeddings)[0]
            
            # DEBUG: Log dos top 5 scores brutos
            top_5_debug = similarities.argsort(descending=True)[:5]
            logger.info(f"   🔍 DEBUG - Top 5 scores:")
            for rank, idx in enumerate(top_5_debug, start=1):
                logger.info(f"      {rank}. '{candidates[idx]}' → {float(similarities[idx]):.4f}")
            
            # Obter top K índices
            top_k_indices = similarities.argsort(descending=True)[:request.top_k]
            
            top_matches = []
            for rank, candidate_idx in enumerate(top_k_indices, start=1):
                candidate_text = candidates[candidate_idx]
                score = float(similarities[candidate_idx])
                
                # Filtrar por threshold
                if score >= request.similarity_threshold:
                    top_matches.append(SemanticMatch(
                        text=candidate_text,
                        score=round(score, 3),
                        rank=rank
                    ))
                    
                    logger.info(f"   ✅ {rank}. '{candidate_text}' (score: {score:.3f})")
            
            # Se não tem matches acima do threshold, pegar o melhor mesmo assim
            if not top_matches and len(top_k_indices) > 0:
                best_idx = top_k_indices[0]
                best_text = candidates[best_idx]
                best_score = float(similarities[best_idx])
                
                top_matches.append(SemanticMatch(
                    text=best_text,
                    score=round(best_score, 3),
                    rank=1
                ))
                logger.info(f"   ⚠️  1. '{best_text}' (score: {best_score:.3f}) [único match, abaixo do threshold]")
            
            # Melhor match
            best_match = top_matches[0].text if top_matches else ""
            best_score = top_matches[0].score if top_matches else 0.0
            
            # IMPORTANTE: Salvar resultado com label correto
            results.append(LabelExtractionResult(
                label=label_name,
                description=label_desc,
                top_matches=top_matches,
                best_match=best_match,
                best_score=best_score
            ))
            
            extraction_summary[label_name] = best_match
            
            logger.info(f"   🎯 Melhor match para '{label_name}': '{best_match}' ({best_score:.3f})")
        
        # 4️⃣ Retornar resultado
        elapsed_ms = int((time.time() - start_time) * 1000)
        
        logger.info("\n" + "="*80)
        logger.info("✅ SEMANTIC EXTRACT - Concluído")
        logger.info(f"⏱️  Tempo: {elapsed_ms}ms")
        logger.info(f"📊 Candidatos avaliados: {len(candidates)}")
        logger.info(f"🎯 Labels processados: {len(results)}")
        logger.info("="*80)
        
        return SemanticExtractResponse(
            results=results,
            extraction_summary=extraction_summary,
            processing_time_ms=elapsed_ms,
            total_candidates=len(candidates),
            model_used="paraphrase-multilingual-MiniLM-L12-v2"
        )
        
    except Exception as e:
        elapsed_ms = int((time.time() - start_time) * 1000)
        logger.error(f"❌ Erro no Semantic Extract após {elapsed_ms}ms: {e}")
        logger.info("="*80)
        raise HTTPException(
            status_code=500,
            detail=str(e)
        )

# ============================================================================
# ENDPOINT: /semantic-label-detect (Detecção de Labels no Texto)
# ============================================================================

@app.post('/semantic-label-detect', response_model=SemanticLabelDetectResponse, tags=["Semantic Extraction"])
async def semantic_label_detect(request: SemanticLabelDetectRequest):
    """
    🏷️ Detecção de Labels no Texto (LÓGICA INVERTIDA)
    
    **NOVO:** Identifica quais labels do schema existem no documento.
    
    **Como funciona:**
    1. Divide o texto em candidatos (possíveis labels no documento)
    2. Gera embeddings para os CANDIDATOS do texto
    3. Gera embeddings para as DESCRIÇÕES dos labels do schema
    4. Para cada CANDIDATO, calcula similaridade com TODOS os labels
    5. Retorna labels detectadas acima do threshold
    
    **Diferença do /semantic-extract:**
    - `/semantic-extract`: Label → busca VALORES no texto
    - `/semantic-label-detect`: Candidato → busca LABELS do schema
    
    **Uso típico:**
    - FASE 2 (remoção de labels do documento)
    - Identificar quais campos do schema aparecem como labels no PDF
    - Mapear estrutura do documento
    
    **Parâmetros:**
    - **labels**: Schema com {campo: descrição}
    - **text**: Texto do documento
    - **top_k**: Top labels por candidato (padrão: 3)
    - **min_token_length**: Tamanho mínimo (padrão: 3)
    - **similarity_threshold**: Score mínimo (padrão: 0.5)
    
    **Exemplo:**
    ```json
    {
        "labels": {
            "nome": "Nome completo da pessoa",
            "cpf": "CPF ou documento",
            "inscricao": "Número de inscrição profissional"
        },
        "text": "Nome Completo:\\nJOANA D'ARC\\nInscrição: 101943\\nCPF: 123.456.789-00"
    }
    ```
    
    **Resultado esperado:**
    ```json
    {
        "detected_labels": [
            {
                "candidate_text": "Nome Completo:",
                "matched_label": "nome",
                "score": 0.89,
                "rank": 1
            },
            {
                "candidate_text": "Inscrição:",
                "matched_label": "inscricao",
                "score": 0.85,
                "rank": 1
            }
        ],
        "labels_summary": {
            "Nome Completo:": "nome",
            "Inscrição:": "inscricao"
        }
    }
    ```
    """
    start_time = time.time()
    
    try:
        logger.info("="*80)
        logger.info("🏷️ SEMANTIC LABEL DETECT - Detecção de Labels Iniciada (MODO INVERTIDO)")
        logger.info(f"📋 Schema: {len(request.labels)} campos")
        logger.info(f"📄 Texto: {len(request.text)} caracteres")
        logger.info(f"🔝 Top K: {request.top_k}")
        logger.info(f"🎯 Threshold: {request.similarity_threshold}")
        logger.info("="*80)
        
        # 1️⃣ Preparar candidatos (possíveis labels no documento)
        logger.info("📝 Preparando candidatos (possíveis labels no documento)...")
        candidates = set()
        
        # Adicionar linhas não vazias
        lines = [line.strip() for line in request.text.split('\n') if line.strip()]
        candidates.update(lines)
        logger.info(f"   • {len(lines)} linhas adicionadas")
        
        # Adicionar tokens grandes (palavras/frases que podem ser labels)
        for line in lines:
            tokens = line.split()
            large_tokens = [
                token.strip() 
                for token in tokens 
                if len(token.strip()) >= request.min_token_length
            ]
            candidates.update(large_tokens)
        
        candidates = list(candidates)
        logger.info(f"   ✓ Total de candidatos: {len(candidates)}")
        
        if not candidates:
            raise HTTPException(
                status_code=400,
                detail="Nenhum candidato válido encontrado no texto"
            )
        
        # 2️⃣ Gerar embeddings
        logger.info("🧠 Gerando embeddings...")
        
        if semantic_embeddings_model is None:
            raise HTTPException(
                status_code=503,
                detail="Modelo de embeddings não está carregado. Reinicie a aplicação."
            )
        
        from sentence_transformers import util
        model = semantic_embeddings_model
        logger.info("   ✓ Usando modelo pré-carregado: paraphrase-multilingual-MiniLM-L12-v2")
        
        # INVERSÃO: Embeddings das DESCRIÇÕES dos labels do schema
        label_descriptions = list(request.labels.values())
        label_names = list(request.labels.keys())
        
        logger.info(f"   • Gerando embeddings para {len(label_descriptions)} labels do schema...")
        label_embeddings = model.encode(label_descriptions, convert_to_tensor=True)
        
        logger.info(f"   • Gerando embeddings para {len(candidates)} candidatos do texto...")
        candidate_embeddings = model.encode(candidates, convert_to_tensor=True)
        
        logger.info("   ✓ Embeddings gerados com sucesso")
        
        # 3️⃣ INVERSÃO: Para cada CANDIDATO, buscar labels mais similares
        logger.info("🔍 Calculando similaridades (candidato → labels do schema)...")
        detected_labels = []
        labels_summary = {}
        
        for candidate_idx, candidate_text in enumerate(candidates):
            # Embedding do candidato
            candidate_embedding = candidate_embeddings[candidate_idx]
            
            # Calcular similaridade com TODOS os labels do schema
            similarities = util.cos_sim(candidate_embedding, label_embeddings)[0]
            
            # Obter top K labels
            top_k_indices = similarities.argsort(descending=True)[:request.top_k]
            
            # Processar apenas se o melhor score >= threshold
            best_idx = top_k_indices[0]
            best_score = float(similarities[best_idx])
            
            if best_score >= request.similarity_threshold:
                best_label = label_names[best_idx]
                
                logger.info(f"\n✅ Candidato: '{candidate_text}'")
                logger.info(f"   → Label: '{best_label}' (score: {best_score:.3f})")
                
                # DEBUG: Mostrar top 3
                logger.info(f"   🔍 Top 3:")
                for rank, idx in enumerate(top_k_indices[:3], start=1):
                    logger.info(f"      {rank}. '{label_names[idx]}' → {float(similarities[idx]):.4f}")
                
                detected_labels.append(CandidateLabelMatch(
                    candidate_text=candidate_text,
                    matched_label=best_label,
                    score=round(best_score, 3),
                    rank=1
                ))
                
                labels_summary[candidate_text] = best_label
        
        # 4️⃣ Retornar resultado
        elapsed_ms = int((time.time() - start_time) * 1000)
        
        logger.info("\n" + "="*80)
        logger.info("✅ SEMANTIC LABEL DETECT - Concluído")
        logger.info(f"⏱️  Tempo: {elapsed_ms}ms")
        logger.info(f"📊 Candidatos avaliados: {len(candidates)}")
        logger.info(f"🏷️  Labels detectadas: {len(detected_labels)}")
        logger.info(f"📋 Mapeamento: {labels_summary}")
        logger.info("="*80)
        
        return SemanticLabelDetectResponse(
            detected_labels=detected_labels,
            labels_summary=labels_summary,
            processing_time_ms=elapsed_ms,
            total_candidates=len(candidates),
            model_used="paraphrase-multilingual-MiniLM-L12-v2"
        )
        
    except Exception as e:
        elapsed_ms = int((time.time() - start_time) * 1000)
        logger.error(f"❌ Erro no Semantic Label Detect após {elapsed_ms}ms: {e}")
        logger.info("="*80)
        raise HTTPException(
            status_code=500,
            detail=str(e)
        )

# ============================================================================
# ENDPOINT: /smart-extract (FASE 2.5 - Smart Extract)
# ============================================================================

@app.post('/smart-extract', response_model=SmartExtractResponse, tags=["Smart Extraction"])
async def smart_extract(request: SmartExtractRequest):
    """
    🧠 FASE 2.5 - Smart Extract
    
    Extrai valores de campos usando:
    - NER (Reconhecimento de Entidades Nomeadas)
    - Embeddings semânticos (similaridade)
    - Cache Redis (reuso de inferências)
    - Memória adaptativa por label
    
    **Fluxo SEQUENCIAL:**
    1. Verifica cache Redis (por hash do texto + schema)
    2. Extrai entidades com NER (spaCy)
    3. Faz match SEQUENCIAL de campos usando embeddings (sentence-transformers)
       - Respeita a ORDEM dos campos no schema
       - Usa a PRIMEIRA ocorrência encontrada
       - Próximo campo busca A PARTIR da linha seguinte
    4. Atualiza memória adaptativa por label
    5. Cacheia resultado (TTL 7 dias)
    
    **Quando usar:**
    - Após FASE 2 (remoção de labels)
    - Se confiança da FASE 1 < 0.7
    - Para documentos com layout variável
    
    **IMPORTANTE:** A ordem dos campos no schema é CRÍTICA!
    Exemplo: {"nome": "...", "cpf": "...", "endereco": "..."}
    Irá buscar NOME primeiro, depois CPF APÓS o nome, depois ENDEREÇO APÓS o CPF.
    """
    start_time = time.time()
    
    try:
        # 1️⃣ Verificar cache Redis
        cache_key = None
        if request.label:
            text_hash = hashlib.md5(request.text.encode()).hexdigest()
            schema_hash = hashlib.md5(str(sorted(request.schema.items())).encode()).hexdigest()
            cache_key = f"smart:{request.label}:{text_hash}:{schema_hash}"
            
            cached = get_cache(cache_key)
            if cached:
                elapsed_ms = int((time.time() - start_time) * 1000)
                logger.info(f"💾 Cache HIT! Retornando resultado em {elapsed_ms}ms")
                logger.info("="*60)
                return SmartExtractResponse(**cached)
        
        # logger.info("🔍 Cache MISS - processando...")
        
        # 2️⃣ Extrair entidades com NER e padrões estruturados
        # logger.info("🏷️ Extraindo entidades com NER...")
        # entities_list = extract_entities(request.text)
        # logger.info(f"  ✓ {len(entities_list)} entidades NER encontradas")
        
        # logger.info("🔍 Extraindo padrões estruturados (CPF, CNPJ, etc)...")
        # structured_patterns = extract_structured_patterns(request.text)
        # patterns_count = sum(len(v) for v in structured_patterns.values())
        # logger.info(f"  ✓ {patterns_count} padrões estruturados encontrados")
        
        # Montar dicionário combinado para match_fields_with_embeddings
        # ner_entities = {
        #     "entities": entities_list,
        #     "structured_patterns": structured_patterns
        # }
        
        # 3️⃣ Match de campos com embeddings
        # logger.info("🎯 Fazendo match de campos com embeddings...")
        # results, avg_conf, methods_used = match_fields_with_embeddings(
        #     schema=request.schema,
        #     text=request.text,
        #     ner_entities=ner_entities
        # )
        
        # logger.info(f"  ✓ {len(results)} campos extraídos")
        # logger.info(f"  📊 Confiança média: {avg_conf:.3f}")
        results = {}
        
        try:
            results = call_gpt_fallback(
                schema=request.schema,
                text=request.text,
                model="gpt-5-mini"
            )
            
        except Exception as gpt_error:
            logger.error(f"❌ Erro no GPT fallback: {gpt_error}")
        
        # 5️⃣ Calcular tempo de processamento
        elapsed_ms = int((time.time() - start_time) * 1000)
        
        # 6️⃣ Cachear resultado
        if cache_key:
            cache_data = {
                "fields": results
            }
            set_cache(cache_key, cache_data, ttl=604800)  # 7 dias
            logger.info("💾 Resultado cacheado")
        elapsed_ms = int((time.time() - start_time) * 1000)
        logger.info(f"⏱️ Tempo de processamento: {elapsed_ms}ms")
        return SmartExtractResponse(fields=results)
        
    except Exception as e:
        elapsed_ms = int((time.time() - start_time) * 1000)
        logger.error(f"❌ Erro no Smart Extract após {elapsed_ms}ms: {e}")
        logger.info("="*60)
        raise HTTPException(
            status_code=500,
            detail=str(e)
        )

@app.post('/extract-text', response_model=PDFResponse, tags=["PDF"])
async def extract_text(request: PDFRequest):
    """
    Endpoint assíncrono para extrair texto de PDF.
    
    **Parâmetros:**
    - **pdf_base64**: PDF codificado em base64
    
    **Retorna:**
    - **text**: Texto extraído do PDF
    - **char_count**: Número de caracteres extraídos
    - **success**: Status da operação
    """
    request_start = time.time()
    
    try:
        try:
            pdf_bytes = base64.b64decode(request.pdf_base64)
        except Exception:
            raise HTTPException(
                status_code=400,
                detail="PDF base64 inválido"
            )
        
        # Extrair texto de forma assíncrona
        extracted_text = await extract_text_from_pdf_async(pdf_bytes)
        
        total_time = time.time() - request_start
        logger.info(f"✨ Requisição completa em {total_time:.3f}s - {len(extracted_text)} caracteres extraídos")
        
        return PDFResponse(
            text=extracted_text,
            char_count=len(extracted_text),
            success=True
        )
        
    except HTTPException:
        error_time = time.time() - request_start
        logger.warning(f"⚠️  Requisição falhou após {error_time:.3f}s")
        raise
    except Exception as e:
        error_time = time.time() - request_start
        logger.error(f"❌ Erro no endpoint após {error_time:.3f}s: {e}")
        logger.info("="*60)
        raise HTTPException(
            status_code=500,
            detail=str(e)
        )

@app.get('/cache/stats', tags=["Cache"])
async def cache_stats():
    """Retorna estatísticas do cache Redis"""
    return get_cache_stats()

if __name__ == '__main__':
    import uvicorn
    # Desenvolvimento
    uvicorn.run(
        "main:app",
        host='0.0.0.0',
        port=5001,
        reload=True,
        log_level="info"
    )
    
    # Produção (comando no terminal):
    # uvicorn main:app --host 0.0.0.0 --port 5001 --workers 4