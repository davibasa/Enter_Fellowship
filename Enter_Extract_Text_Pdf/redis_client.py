"""
Redis Client for caching extraction results, embeddings, and NLI scores
"""
import redis
import json
import logging
import os
import hashlib
import numpy as np
from typing import Optional, Dict, Any, List

logger = logging.getLogger(__name__)

# Configuração do Redis CACHE (volátil)
REDIS_CACHE_HOST = os.getenv("REDIS_CACHE_HOST", "localhost")
REDIS_CACHE_PORT = int(os.getenv("REDIS_CACHE_PORT", "6379"))
REDIS_CACHE_DB = int(os.getenv("REDIS_CACHE_DB", "0"))

# Configuração do Redis STORAGE (persistente)
REDIS_STORAGE_HOST = os.getenv("REDIS_STORAGE_HOST", "localhost")
REDIS_STORAGE_PORT = int(os.getenv("REDIS_STORAGE_PORT", "6380"))
REDIS_STORAGE_DB = int(os.getenv("REDIS_STORAGE_DB", "0"))

# TTLs padrão
REDIS_EXTRACTION_TTL = int(os.getenv("REDIS_EXTRACTION_TTL", "604800"))  # 7 dias
REDIS_EMBEDDING_TTL = int(os.getenv("REDIS_EMBEDDING_TTL", "2592000"))  # 30 dias
REDIS_NLI_TTL = int(os.getenv("REDIS_NLI_TTL", "1209600"))  # 14 dias

# Clientes Redis globais
redis_cache_client: Optional[redis.StrictRedis] = None
redis_storage_client: Optional[redis.StrictRedis] = None


def initialize_redis():
    """Inicializa conexões com Redis Cache e Storage"""
    global redis_cache_client, redis_storage_client
    
    cache_success = False
    storage_success = False
    
    # Conectar Redis Cache (volátil)
    try:
        redis_cache_client = redis.StrictRedis(
            host=REDIS_CACHE_HOST,
            port=REDIS_CACHE_PORT,
            db=REDIS_CACHE_DB,
            decode_responses=False,  # Para embeddings binários
            socket_timeout=5,
            socket_connect_timeout=5
        )
        redis_cache_client.ping()
        logger.info(f"✅ Redis CACHE conectado em {REDIS_CACHE_HOST}:{REDIS_CACHE_PORT}")
        cache_success = True
    except Exception as e:
        logger.error(f"❌ Erro ao conectar Redis CACHE: {e}")
        redis_cache_client = None
    
    # Conectar Redis Storage (persistente)
    try:
        redis_storage_client = redis.StrictRedis(
            host=REDIS_STORAGE_HOST,
            port=REDIS_STORAGE_PORT,
            db=REDIS_STORAGE_DB,
            decode_responses=True,
            socket_timeout=5,
            socket_connect_timeout=5
        )
        redis_storage_client.ping()
        logger.info(f"✅ Redis STORAGE conectado em {REDIS_STORAGE_HOST}:{REDIS_STORAGE_PORT}")
        storage_success = True
    except Exception as e:
        logger.error(f"❌ Erro ao conectar Redis STORAGE: {e}")
        redis_storage_client = None
    
    return cache_success or storage_success


def compute_text_hash(text: str) -> str:
    """Calcula hash SHA256 de um texto (16 chars)"""
    return hashlib.sha256(text.encode('utf-8')).hexdigest()[:16]

# ============================================================================
# CACHE DE RESULTADOS DE EXTRAÇÃO
# ============================================================================

def get_cache(key: str) -> Optional[Dict[str, Any]]:
    """
    Busca valor do cache
    
    Args:
        key: Chave no formato "extraction:label:pdf_hash"
        
    Returns:
        Dict com resultado ou None se não encontrado
    """
    if redis_cache_client is None:
        return None
    
    try:
        value = redis_cache_client.get(key)
        if value:
            logger.info(f"🎯 Cache HIT: {key[:50]}...")
            if isinstance(value, bytes):
                value = value.decode('utf-8')
            return json.loads(value)
        else:
            logger.debug(f"⚠️ Cache MISS: {key[:50]}...")
            return None
    except Exception as e:
        logger.error(f"❌ Erro ao buscar cache: {e}")
        return None


def set_cache(key: str, value: Dict[str, Any], ttl: int = REDIS_EXTRACTION_TTL) -> bool:
    """
    Salva valor no cache
    
    Args:
        key: Chave no formato "extraction:label:pdf_hash"
        value: Dicionário com resultado da extração
        ttl: Time to live em segundos (padrão 7 dias)
        
    Returns:
        True se salvou com sucesso
    """
    if redis_cache_client is None:
        return False
    
    try:
        json_value = json.dumps(value, ensure_ascii=False)
        redis_cache_client.setex(key, ttl, json_value)
        logger.info(f"💾 Cache SAVED: {key[:50]}... (TTL: {ttl}s)")
        return True
    except Exception as e:
        logger.error(f"❌ Erro ao salvar cache: {e}")
        return False


def invalidate_cache(pattern: str) -> int:
    """
    Invalida cache por padrão
    
    Args:
        pattern: Padrão de chave (ex: "extraction:*")
        
    Returns:
        Número de chaves deletadas
    """
    if redis_cache_client is None:
        return 0
    
    try:
        keys = redis_cache_client.keys(pattern)
        if keys:
            deleted = redis_cache_client.delete(*keys)
            logger.info(f"🗑️ Cache invalidado: {deleted} chaves")
            return deleted
        return 0
    except Exception as e:
        logger.error(f"❌ Erro ao invalidar cache: {e}")
        return 0


# ============================================================================
# CACHE DE EMBEDDINGS
# ============================================================================

def get_embedding_cache(text: str, model: str = "mpnet") -> Optional[np.ndarray]:
    """
    Busca embedding do cache
    
    Args:
        text: Texto para buscar embedding
        model: Nome do modelo (default: mpnet)
        
    Returns:
        Array numpy com embedding ou None
    """
    if redis_cache_client is None:
        return None
    
    try:
        text_hash = compute_text_hash(text)
        key = f"embedding:{model}:{text_hash}"
        value = redis_cache_client.hget(key, "embedding_json")
        
        if value:
            logger.debug(f"🎯 Embedding cache HIT: {text[:30]}...")
            if isinstance(value, bytes):
                value = value.decode('utf-8')
            embedding_list = json.loads(value)
            return np.array(embedding_list, dtype=np.float32)
        else:
            logger.debug(f"⚠️ Embedding cache MISS: {text[:30]}...")
            return None
    except Exception as e:
        logger.error(f"❌ Erro ao buscar embedding cache: {e}")
        return None


def save_embedding_cache(text: str, embedding: np.ndarray, model: str = "mpnet",
                        ttl: int = REDIS_EMBEDDING_TTL) -> bool:
    """
    Salva embedding no cache
    
    Args:
        text: Texto original
        embedding: Array numpy com embedding
        model: Nome do modelo
        ttl: Time to live em segundos (default: 30 dias)
        
    Returns:
        True se salvou com sucesso
    """
    if redis_cache_client is None:
        return False
    
    try:
        text_hash = compute_text_hash(text)
        key = f"embedding:{model}:{text_hash}"
        
        # Converter numpy array para JSON
        embedding_json = json.dumps(embedding.tolist())
        
        # Salvar no Redis Hash
        redis_cache_client.hset(key, mapping={
            "text": text[:100],  # Primeiros 100 chars
            "embedding_json": embedding_json,
            "model": model,
            "dimensions": str(len(embedding))
        })
        redis_cache_client.expire(key, ttl)
        
        logger.debug(f"💾 Embedding cache SAVED: {text[:30]}... (dim={len(embedding)})")
        return True
    except Exception as e:
        logger.error(f"❌ Erro ao salvar embedding cache: {e}")
        return False


# ============================================================================
# CACHE DE NLI (Zero-Shot Classification)
# ============================================================================

def get_nli_cache(text: str, hypothesis: str) -> Optional[Dict[str, float]]:
    """
    Busca classificação NLI do cache
    
    Args:
        text: Texto (premissa)
        hypothesis: Hipótese
        
    Returns:
        Dict com scores ou None
    """
    if redis_cache_client is None:
        return None
    
    try:
        text_hash = compute_text_hash(text)
        hyp_hash = compute_text_hash(hypothesis)
        key = f"nli:{text_hash}:{hyp_hash}"
        
        value = redis_cache_client.get(key)
        if value:
            logger.debug(f"🎯 NLI cache HIT: {text[:20]}... | {hypothesis[:20]}...")
            if isinstance(value, bytes):
                value = value.decode('utf-8')
            return json.loads(value)
        else:
            logger.debug(f"⚠️ NLI cache MISS: {text[:20]}... | {hypothesis[:20]}...")
            return None
    except Exception as e:
        logger.error(f"❌ Erro ao buscar NLI cache: {e}")
        return None


def save_nli_cache(text: str, hypothesis: str, scores: Dict[str, float],
                   model: str = "mDeBERTa", ttl: int = REDIS_NLI_TTL) -> bool:
    """
    Salva classificação NLI no cache
    
    Args:
        text: Texto (premissa)
        hypothesis: Hipótese
        scores: Dict com scores (entailment, contradiction, neutral)
        model: Nome do modelo
        ttl: Time to live em segundos (default: 14 dias)
        
    Returns:
        True se salvou com sucesso
    """
    if redis_cache_client is None:
        return False
    
    try:
        text_hash = compute_text_hash(text)
        hyp_hash = compute_text_hash(hypothesis)
        key = f"nli:{text_hash}:{hyp_hash}"
        
        cache_data = {
            "scores": scores,
            "model": model
        }
        
        json_value = json.dumps(cache_data)
        redis_cache_client.setex(key, ttl, json_value)
        
        logger.debug(f"💾 NLI cache SAVED: {text[:20]}... | {hypothesis[:20]}...")
        return True
    except Exception as e:
        logger.error(f"❌ Erro ao salvar NLI cache: {e}")
        return False


# ============================================================================
# ESTATÍSTICAS E MONITORAMENTO
# ============================================================================

def get_cache_stats() -> Dict[str, Any]:
    """
    Retorna estatísticas do cache
    
    Returns:
        Dict com informações do Redis
    """
    stats = {}
    
    # Stats do Cache
    if redis_cache_client is not None:
        try:
            info = redis_cache_client.info()
            stats["cache"] = {
                "status": "connected",
                "host": f"{REDIS_CACHE_HOST}:{REDIS_CACHE_PORT}",
                "db_size": redis_cache_client.dbsize(),
                "used_memory_human": info.get("used_memory_human"),
                "connected_clients": info.get("connected_clients"),
                "total_commands_processed": info.get("total_commands_processed"),
                "keyspace_hits": info.get("keyspace_hits", 0),
                "keyspace_misses": info.get("keyspace_misses", 0)
            }
            # Calcular hit rate
            hits = info.get("keyspace_hits", 0)
            misses = info.get("keyspace_misses", 0)
            total = hits + misses
            if total > 0:
                stats["cache"]["hit_rate"] = round(hits / total * 100, 2)
        except Exception as e:
            stats["cache"] = {"status": "error", "error": str(e)}
    else:
        stats["cache"] = {"status": "disconnected"}
    
    # Stats do Storage
    if redis_storage_client is not None:
        try:
            info = redis_storage_client.info()
            stats["storage"] = {
                "status": "connected",
                "host": f"{REDIS_STORAGE_HOST}:{REDIS_STORAGE_PORT}",
                "db_size": redis_storage_client.dbsize(),
                "used_memory_human": info.get("used_memory_human")
            }
        except Exception as e:
            stats["storage"] = {"status": "error", "error": str(e)}
    else:
        stats["storage"] = {"status": "disconnected"}
    
    return stats
