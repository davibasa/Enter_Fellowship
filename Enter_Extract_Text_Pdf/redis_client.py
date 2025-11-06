"""
Redis Client for caching extraction results
"""
import redis
import json
import logging
import os
from typing import Optional, Dict, Any

logger = logging.getLogger(__name__)

# Configuração do Redis
REDIS_HOST = os.getenv("REDIS_HOST", "localhost")
REDIS_PORT = int(os.getenv("REDIS_PORT", "6379"))
REDIS_DB = int(os.getenv("REDIS_DB", "0"))
REDIS_TTL = int(os.getenv("REDIS_TTL", "604800"))  # 7 dias padrão

# Cliente Redis global
redis_client: Optional[redis.StrictRedis] = None


def initialize_redis():
    """Inicializa conexão com Redis"""
    global redis_client
    try:
        redis_client = redis.StrictRedis(
            host=REDIS_HOST,
            port=REDIS_PORT,
            db=REDIS_DB,
            decode_responses=True,
            socket_timeout=5,
            socket_connect_timeout=5
        )
        # Testa conexão
        redis_client.ping()
        logger.info(f"✅ Redis conectado em {REDIS_HOST}:{REDIS_PORT}")
        return True
    except Exception as e:
        logger.error(f"❌ Erro ao conectar Redis: {e}")
        redis_client = None
        return False


def get_cache(key: str) -> Optional[Dict[str, Any]]:
    """
    Busca valor do cache
    
    Args:
        key: Chave no formato "label:text_hash"
        
    Returns:
        Dict com resultado ou None se não encontrado
    """
    if redis_client is None:
        return None
    
    try:
        value = redis_client.get(key)
        if value:
            logger.info(f"🎯 Cache HIT: {key[:50]}...")
            return json.loads(value)
        else:
            logger.debug(f"⚠️ Cache MISS: {key[:50]}...")
            return None
    except Exception as e:
        logger.error(f"❌ Erro ao buscar cache: {e}")
        return None


def set_cache(key: str, value: Dict[str, Any], ttl: int = REDIS_TTL) -> bool:
    """
    Salva valor no cache
    
    Args:
        key: Chave no formato "label:text_hash"
        value: Dicionário com resultado da extração
        ttl: Time to live em segundos (padrão 7 dias)
        
    Returns:
        True se salvou com sucesso
    """
    if redis_client is None:
        return False
    
    try:
        json_value = json.dumps(value, ensure_ascii=False)
        redis_client.setex(key, ttl, json_value)
        logger.info(f"💾 Cache SAVED: {key[:50]}... (TTL: {ttl}s)")
        return True
    except Exception as e:
        logger.error(f"❌ Erro ao salvar cache: {e}")
        return False


def invalidate_cache(pattern: str) -> int:
    """
    Invalida cache por padrão
    
    Args:
        pattern: Padrão de chave (ex: "label_*")
        
    Returns:
        Número de chaves deletadas
    """
    if redis_client is None:
        return 0
    
    try:
        keys = redis_client.keys(pattern)
        if keys:
            deleted = redis_client.delete(*keys)
            logger.info(f"🗑️ Cache invalidado: {deleted} chaves")
            return deleted
        return 0
    except Exception as e:
        logger.error(f"❌ Erro ao invalidar cache: {e}")
        return 0


def get_cache_stats() -> Dict[str, Any]:
    """
    Retorna estatísticas do cache
    
    Returns:
        Dict com informações do Redis
    """
    if redis_client is None:
        return {"status": "disconnected"}
    
    try:
        info = redis_client.info()
        return {
            "status": "connected",
            "host": REDIS_HOST,
            "port": REDIS_PORT,
            "db_size": redis_client.dbsize(),
            "used_memory_human": info.get("used_memory_human"),
            "connected_clients": info.get("connected_clients"),
            "total_commands_processed": info.get("total_commands_processed")
        }
    except Exception as e:
        logger.error(f"❌ Erro ao obter stats: {e}")
        return {"status": "error", "error": str(e)}
