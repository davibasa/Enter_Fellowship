"""
NER (Named Entity Recognition) Extractor using spaCy
"""
import logging
from typing import List, Dict, Any
import re

logger = logging.getLogger(__name__)

# spaCy NLP model (será carregado na inicialização)
nlp = None


def initialize_ner():
    """Inicializa modelo spaCy para português"""
    global nlp
    try:
        import spacy
        
        # Tenta carregar modelo português
        try:
            nlp = spacy.load("pt_core_news_lg")
            logger.info("✅ Modelo spaCy 'pt_core_news_lg' carregado")
        except OSError:
            logger.warning("⚠️ Modelo 'pt_core_news_lg' não encontrado, tentando 'pt_core_news_sm'")
            try:
                nlp = spacy.load("pt_core_news_sm")
                logger.info("✅ Modelo spaCy 'pt_core_news_sm' carregado")
            except OSError:
                logger.error("❌ Nenhum modelo spaCy português encontrado. Execute: python -m spacy download pt_core_news_lg")
                nlp = None
                return False
        
        return True
    except ImportError:
        logger.error("❌ spaCy não instalado. Execute: pip install spacy")
        nlp = None
        return False


def extract_entities(text: str) -> List[Dict[str, Any]]:
    """
    Extrai entidades nomeadas do texto usando spaCy
    
    Args:
        text: Texto para análise
        
    Returns:
        Lista de entidades com texto, label e posição
    """
    if nlp is None:
        logger.warning("⚠️ spaCy não inicializado, retornando entidades vazias")
        return []
    
    try:
        doc = nlp(text)
        
        entities = []
        for ent in doc.ents:
            entities.append({
                "text": ent.text,
                "label": ent.label_,
                "start": ent.start_char,
                "end": ent.end_char
            })
        
        logger.debug(f"🔍 NER: {len(entities)} entidades extraídas")
        return entities
        
    except Exception as e:
        logger.error(f"❌ Erro ao extrair entidades: {e}")
        return []


def extract_structured_patterns(text: str) -> Dict[str, List[str]]:
    """
    Extrai padrões estruturados com regex
    Complementa o NER para dados brasileiros
    
    Returns:
        Dict com tipo → lista de valores encontrados
    """
    patterns = {
        "cpf": r"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b",
        "cnpj": r"\b\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}\b",
        "phone": r"\(?\d{2}\)?\s*\d{4,5}-?\d{4}",
        "email": r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        "cep": r"\b\d{5}-\d{3}\b",
        "date": r"\b\d{2}/\d{2}/\d{4}\b",
        "currency": r"R\$\s*\d{1,3}(?:\.\d{3})*(?:,\d{2})?",
        "percentage": r"\d+(?:,\d+)?%"
    }
    
    results = {}
    
    for pattern_name, pattern in patterns.items():
        matches = re.findall(pattern, text)
        if matches:
            results[pattern_name] = matches
            logger.debug(f"  • {pattern_name}: {len(matches)} matches")
    
    return results


def enrich_entities_with_patterns(
    text: str,
    ner_entities: List[Dict[str, Any]]
) -> Dict[str, Any]:
    """
    Combina entidades NER com padrões regex
    
    Returns:
        Dict com entidades enriquecidas
    """
    # Extrair padrões estruturados
    patterns = extract_structured_patterns(text)
    
    # Organizar entidades NER por tipo
    ner_by_type = {}
    for ent in ner_entities:
        label = ent["label"]
        if label not in ner_by_type:
            ner_by_type[label] = []
        ner_by_type[label].append(ent["text"])
    
    # Combinar tudo
    combined = {
        "ner_entities": ner_by_type,
        "structured_patterns": patterns,
        "all_entities": ner_entities
    }
    
    logger.info(f"🧩 Entidades enriquecidas: {len(ner_entities)} NER + {sum(len(v) for v in patterns.values())} padrões")
    
    return combined


def get_entity_context(text: str, entity_text: str, window: int = 50) -> str:
    """
    Retorna contexto ao redor de uma entidade
    
    Args:
        text: Texto completo
        entity_text: Texto da entidade
        window: Janela de caracteres ao redor
        
    Returns:
        Contexto ao redor da entidade
    """
    idx = text.find(entity_text)
    if idx == -1:
        return ""
    
    start = max(0, idx - window)
    end = min(len(text), idx + len(entity_text) + window)
    
    return text[start:end]
