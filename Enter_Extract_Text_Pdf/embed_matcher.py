"""
Embedding Matcher using sentence-transformers with Redis cache
"""
import logging
from typing import Dict, Any, List, Tuple
import torch
import numpy as np

from cache.embedding_cache import EmbeddingCache

logger = logging.getLogger(__name__)

# Modelo de embeddings (será carregado na inicialização)
model = None
embedding_cache = None


def initialize_embeddings():
    """Inicializa modelo de embeddings e cache"""
    global model, embedding_cache
    try:
        from sentence_transformers import SentenceTransformer
        
        # Modelo multilíngue leve e rápido
        model_name = "all-MiniLM-L6-v2"
        model = SentenceTransformer(model_name)
        
        # Inicializar cache
        embedding_cache = EmbeddingCache(model_name=model_name)
        
        logger.info(f"✅ Modelo sentence-transformers '{model_name}' carregado")
        logger.info(f"✅ Cache de embeddings inicializado: {embedding_cache.get_stats()}")
        return True
        
    except ImportError:
        logger.error("❌ sentence-transformers não instalado. Execute: pip install sentence-transformers")
        model = None
        embedding_cache = None
        return False
    except Exception as e:
        logger.error(f"❌ Erro ao carregar modelo de embeddings: {e}")
        model = None
        embedding_cache = None
        return False


def get_embedding_with_cache(text: str) -> torch.Tensor:
    """
    Obtém embedding com cache Redis
    
    Args:
        text: Texto para gerar embedding
        
    Returns:
        Tensor com embedding
    """
    global model, embedding_cache
    
    if model is None:
        raise RuntimeError("Model not initialized")
    
    # Tentar obter do cache
    if embedding_cache:
        cached = embedding_cache.get(text)
        if cached is not None:
            # Cache HIT - converter para tensor
            return torch.tensor(cached)
    
    # Cache MISS - calcular embedding
    embedding = model.encode(text, convert_to_tensor=True)
    
    # Salvar no cache (fire-and-forget)
    if embedding_cache:
        try:
            embedding_np = embedding.cpu().numpy() if torch.is_tensor(embedding) else embedding
            embedding_cache.set(text, embedding_np)
        except Exception as e:
            logger.debug(f"Failed to cache embedding (non-critical): {e}")
    
    return embedding


def get_embeddings_batch_with_cache(texts: List[str]) -> torch.Tensor:
    """
    Obtém embeddings em lote com cache Redis
    
    Args:
        texts: Lista de textos
        
    Returns:
        Tensor com embeddings
    """
    global model, embedding_cache
    
    if model is None:
        raise RuntimeError("Model not initialized")
    
    embeddings = []
    texts_to_compute = []
    indices_to_compute = []
    
    # Tentar obter do cache
    if embedding_cache:
        cached_embeddings = embedding_cache.get_batch(texts)
        
        for i, (text, cached) in enumerate(zip(texts, cached_embeddings)):
            if cached is not None:
                # Cache HIT
                embeddings.append(torch.tensor(cached))
            else:
                # Cache MISS - guardar para calcular depois
                embeddings.append(None)
                texts_to_compute.append(text)
                indices_to_compute.append(i)
    else:
        # Sem cache - calcular todos
        texts_to_compute = texts
        indices_to_compute = list(range(len(texts)))
        embeddings = [None] * len(texts)
    
    # Calcular embeddings que faltam
    if texts_to_compute:
        computed = model.encode(texts_to_compute, convert_to_tensor=True)
        
        # Salvar no cache (fire-and-forget)
        if embedding_cache:
            try:
                computed_np = [emb.cpu().numpy() for emb in computed]
                embedding_cache.set_batch(texts_to_compute, computed_np)
            except Exception as e:
                logger.debug(f"Failed to cache embeddings batch (non-critical): {e}")
        
        # Inserir embeddings calculados nas posições corretas
        for idx, emb in zip(indices_to_compute, computed):
            embeddings[idx] = emb
    
    # Converter lista para tensor
    return torch.stack(embeddings)


def match_fields_with_embeddings(
    schema: Dict[str, str],
    text: str,
    ner_entities: Dict[str, Any]
) -> Tuple[Dict[str, Any], float, Dict[str, int]]:
    """
    Faz match SEQUENCIAL de campos do schema com o texto usando embeddings.
    
    REGRAS:
    1. Respeita a ORDEM dos campos no schema (primeira chave = primeiro campo)
    2. Usa a PRIMEIRA ocorrência encontrada para cada campo
    3. Após encontrar um campo, busca o próximo A PARTIR da posição seguinte
    4. Nunca reutiliza a mesma linha para múltiplos campos
    
    Args:
        schema: Dict {campo: descrição} - ORDEM IMPORTA!
        text: Texto extraído
        ner_entities: Entidades extraídas por NER
        
    Returns:
        (resultado, confiança_média, methods_used)
    """
    if model is None:
        logger.warning("⚠️ Modelo de embeddings não inicializado")
        return {}, 0.0, {}
    
    try:
        from sentence_transformers import util
        
        # Dividir texto em linhas não vazias
        all_lines = [line.strip() for line in text.split("\n") if line.strip()]
        
        if not all_lines:
            logger.warning("⚠️ Texto vazio para matching")
            return {}, 0.0, {}
        
        logger.info(f"📋 Matching SEQUENCIAL de {len(schema)} campos em {len(all_lines)} linhas")
        logger.info(f"🔄 Ordem dos campos: {list(schema.keys())}")
        
        # Gerar embeddings de TODAS as linhas uma vez só (COM CACHE)
        logger.debug(f"🔢 Gerando embeddings para {len(all_lines)} linhas...")
        emb_all_lines = get_embeddings_batch_with_cache(all_lines)
        
        result = {}
        confidences = []
        methods_used = {}
        
        # ⭐ POSIÇÃO ATUAL: começa do início e avança conforme encontra campos
        current_start_index = 0
        
        # Para cada campo no schema (NA ORDEM RECEBIDA)
        for field_name, field_description in schema.items():
            logger.info(f"🔍 Campo '{field_name}' - buscando a partir da linha {current_start_index}")
            
            # Primeira tentativa: buscar em padrões estruturados (CPF, CNPJ, etc)
            matched_by_pattern = match_structured_pattern(
                field_name, 
                field_description, 
                ner_entities
            )
            
            if matched_by_pattern:
                result[field_name] = matched_by_pattern
                # Padrões estruturados não consomem posição (podem estar em qualquer lugar)
                continue
            
            # Se não há mais linhas disponíveis
            if current_start_index >= len(all_lines):
                result[field_name] = None
                continue
            
            # Segunda tentativa: usar embeddings nas linhas DISPONÍVEIS (a partir de current_start_index)
            available_lines = all_lines[current_start_index:]
            emb_available_lines = emb_all_lines[current_start_index:]
            
            logger.debug(f"  🔎 Buscando em {len(available_lines)} linhas disponíveis")
            
            # Gerar embedding do campo (COM CACHE)
            emb_field = get_embedding_with_cache(field_description)
            scores = util.cos_sim(emb_field, emb_available_lines)[0]
            
            # Pegar MELHOR match (PRIMEIRO na ordem que tem maior similaridade)
            best_relative_idx = torch.argmax(scores).item()
            best_score = float(scores[best_relative_idx])
            
            # Converter índice relativo para absoluto
            best_absolute_idx = current_start_index + best_relative_idx
            best_line = all_lines[best_absolute_idx]
            
            # Tentar limpar o valor (remover possível label)
            cleaned_value = clean_extracted_value(best_line)
            
            result[field_name] = cleaned_value
            
            
            confidences.append(best_score)
            
            # ⭐ AVANÇAR POSIÇÃO: próximo campo começa NA LINHA SEGUINTE
            current_start_index = best_absolute_idx + 1
        
        # Calcular confiança média
        avg_confidence = sum(confidences) / len(confidences) if confidences else 0.0
        
        return result, avg_confidence, methods_used
        
    except Exception as e:
        logger.error(f"❌ Erro no matching: {e}")
        return {}, 0.0, {}


def match_structured_pattern(
    field_name: str,
    field_description: str,
    ner_entities: Dict[str, Any]
) -> Dict[str, Any] | None:
    """
    Tenta fazer match com padrões estruturados (CPF, CNPJ, etc.)
    
    Returns:
        Dict com resultado ou None se não encontrou
    """
    structured = ner_entities.get("structured_patterns", {})
    
    # Mapear nome do campo para tipo de padrão
    field_lower = field_name.lower()
    desc_lower = field_description.lower()
    
    pattern_map = {
        "cpf": "cpf",
        "cnpj": "cnpj",
        "telefone": "phone",
        "celular": "phone",
        "fone": "phone",
        "email": "email",
        "e-mail": "email",
        "cep": "cep",
        "data": "date",
        "valor": "currency",
        "preco": "currency",
        "percentual": "percentage",
        "taxa": "percentage"
    }
    
    for keyword, pattern_type in pattern_map.items():
        if keyword in field_lower or keyword in desc_lower:
            if pattern_type in structured and structured[pattern_type]:
                # Pegar primeiro match
                value = structured[pattern_type][0]
                logger.debug(f"  ✓ Match estruturado: {field_name} → {value} ({pattern_type})")
                return {
                    value
                }
    
    return None


def clean_extracted_value(line: str) -> str:
    """
    Remove possíveis labels do valor extraído
    
    Args:
        line: Linha extraída
        
    Returns:
        Valor limpo
    """
    # Padrões comuns de labels
    label_patterns = [
        r"^[A-Za-zÀ-ÿ\s]+:\s*",  # "Nome: "
        r"^[A-Za-zÀ-ÿ\s]+\s*-\s*",  # "Nome - "
    ]
    
    cleaned = line
    for pattern in label_patterns:
        import re
        cleaned = re.sub(pattern, "", cleaned, flags=re.IGNORECASE)
    
    return cleaned.strip()


def find_similar_lines(
    query: str,
    lines: List[str],
    top_k: int = 3
) -> List[Tuple[str, float]]:
    """
    Encontra linhas mais similares a uma query
    
    Args:
        query: Texto de busca
        lines: Lista de linhas
        top_k: Número de resultados
        
    Returns:
        Lista de (linha, score)
    """
    if model is None:
        return []
    
    try:
        from sentence_transformers import util
        
        query_emb = get_embedding_with_cache(query)
        lines_emb = get_embeddings_batch_with_cache(lines)
        
        scores = util.cos_sim(query_emb, lines_emb)[0]
        
        # Pegar top_k
        top_indices = torch.topk(scores, min(top_k, len(lines))).indices
        
        results = [
            (lines[idx], float(scores[idx]))
            for idx in top_indices
        ]
        
        return results
        
    except Exception as e:
        logger.error(f"❌ Erro ao buscar linhas similares: {e}")
        return []
