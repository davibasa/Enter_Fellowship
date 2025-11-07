"""
Cliente Redis genérico para cache de embeddings e outros dados
"""
import os
import redis
from typing import Optional
import logging

logger = logging.getLogger(__name__)


class RedisClient:
    """Cliente Redis singleton"""
    
    _instance: Optional[redis.Redis] = None
    _available: bool = True
    
    @classmethod
    def get_instance(cls) -> Optional[redis.Redis]:
        """Obtém instância singleton do Redis"""
        if cls._instance is None and cls._available:
            try:
                redis_url = os.getenv("REDIS_URL", "redis://localhost:6379/0")
                cls._instance = redis.from_url(
                    redis_url,
                    decode_responses=False,  # Trabalhar com bytes para embeddings
                    socket_connect_timeout=5,
                    socket_timeout=5,
                    retry_on_timeout=True,
                    health_check_interval=30
                )
                
                # Testar conexão
                cls._instance.ping()
                logger.info(f"✅ Redis connected successfully at {redis_url}")
                
            except Exception as e:
                logger.warning(f"⚠️ Redis unavailable, caching disabled: {e}")
                cls._available = False
                cls._instance = None
        
        return cls._instance
    
    @classmethod
    def is_available(cls) -> bool:
        """Verifica se Redis está disponível"""
        if not cls._available:
            return False
        
        try:
            instance = cls.get_instance()
            if instance:
                instance.ping()
                return True
        except:
            cls._available = False
            logger.warning("Redis connection lost, disabling cache")
        
        return False
    
    @classmethod
    def reset(cls):
        """Reset da instância (útil para testes)"""
        cls._instance = None
        cls._available = True
