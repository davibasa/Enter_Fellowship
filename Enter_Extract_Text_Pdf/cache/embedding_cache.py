"""
Cache de embeddings usando Redis
"""
import hashlib
import json
import pickle
import numpy as np
from typing import Optional, List
from datetime import datetime, timedelta
import logging

from .redis_client import RedisClient

logger = logging.getLogger(__name__)


class EmbeddingCache:
    """Cache de embeddings com Redis"""
    
    def __init__(self, model_name: str = "paraphrase-multilingual-mpnet-base-v2"):
        self.model_name = model_name
        self.ttl_seconds = 30 * 24 * 60 * 60  # 30 dias
        self.redis = RedisClient.get_instance()
    
    def _normalize_text(self, text: str) -> str:
        """Normaliza texto para hashing consistente"""
        return text.lower().strip()
    
    def _calculate_hash(self, text: str) -> str:
        """Calcula hash SHA-256 do texto normalizado"""
        normalized = self._normalize_text(text)
        return hashlib.sha256(normalized.encode('utf-8')).hexdigest()[:40]
    
    def _build_cache_key(self, text: str) -> str:
        """Constrói chave de cache"""
        text_hash = self._calculate_hash(text)
        return f"embedding:{self.model_name}:{text_hash}"
    
    def get(self, text: str) -> Optional[np.ndarray]:
        """
        Obtém embedding do cache
        
        Args:
            text: Texto para buscar embedding
            
        Returns:
            Array numpy com embedding ou None se não encontrado
        """
        if not self.redis or not RedisClient.is_available():
            return None
        
        try:
            cache_key = self._build_cache_key(text)
            cached_data = self.redis.hgetall(cache_key)
            
            if not cached_data:
                logger.debug(f"Cache MISS for text: {text[:30]}...")
                return None
            
            # Deserializar embedding (stored as pickle bytes)
            embedding_bytes = cached_data.get(b'embedding')
            if not embedding_bytes:
                return None
            
            embedding = pickle.loads(embedding_bytes)
            logger.debug(f"Cache HIT for text: {text[:30]}... (dim: {len(embedding)})")
            
            return embedding
            
        except Exception as e:
            logger.warning(f"Error getting cached embedding: {e}")
            return None
    
    def set(self, text: str, embedding: np.ndarray) -> bool:
        """
        Salva embedding no cache
        
        Args:
            text: Texto original
            embedding: Array numpy com embedding
            
        Returns:
            True se salvo com sucesso, False caso contrário
        """
        if not self.redis or not RedisClient.is_available():
            return False
        
        try:
            cache_key = self._build_cache_key(text)
            
            # Serializar embedding como pickle (mais eficiente que JSON)
            embedding_bytes = pickle.dumps(embedding.tolist() if isinstance(embedding, np.ndarray) else embedding)
            
            cache_data = {
                'text': text[:100],  # Primeiros 100 chars para debug
                'text_normalized': self._normalize_text(text)[:100],
                'embedding': embedding_bytes,
                'embedding_dim': len(embedding),
                'model': self.model_name,
                'created_at': datetime.utcnow().isoformat(),
                'cache_version': '1.0'
            }
            
            # Salvar no Redis
            self.redis.hset(cache_key, mapping=cache_data)
            self.redis.expire(cache_key, self.ttl_seconds)
            
            logger.debug(f"Cached embedding for text: {text[:30]}... (dim: {len(embedding)})")
            return True
            
        except Exception as e:
            logger.warning(f"Error caching embedding: {e}")
            return False
    
    def get_batch(self, texts: List[str]) -> List[Optional[np.ndarray]]:
        """
        Obtém múltiplos embeddings do cache
        
        Args:
            texts: Lista de textos
            
        Returns:
            Lista de embeddings (None para cache miss)
        """
        if not self.redis or not RedisClient.is_available():
            return [None] * len(texts)
        
        results = []
        for text in texts:
            embedding = self.get(text)
            results.append(embedding)
        
        return results
    
    def set_batch(self, texts: List[str], embeddings: List[np.ndarray]) -> List[bool]:
        """
        Salva múltiplos embeddings no cache
        
        Args:
            texts: Lista de textos
            embeddings: Lista de embeddings
            
        Returns:
            Lista de booleanos indicando sucesso
        """
        if not self.redis or not RedisClient.is_available():
            return [False] * len(texts)
        
        if len(texts) != len(embeddings):
            raise ValueError("texts and embeddings must have same length")
        
        results = []
        for text, embedding in zip(texts, embeddings):
            success = self.set(text, embedding)
            results.append(success)
        
        return results
    
    def delete(self, text: str) -> bool:
        """Remove embedding do cache"""
        if not self.redis or not RedisClient.is_available():
            return False
        
        try:
            cache_key = self._build_cache_key(text)
            deleted = self.redis.delete(cache_key)
            return deleted > 0
        except Exception as e:
            logger.warning(f"Error deleting cached embedding: {e}")
            return False
    
    def clear_all(self) -> int:
        """
        Remove todos os embeddings do modelo atual do cache
        
        Returns:
            Quantidade de chaves removidas
        """
        if not self.redis or not RedisClient.is_available():
            return 0
        
        try:
            pattern = f"embedding:{self.model_name}:*"
            keys = list(self.redis.scan_iter(match=pattern, count=100))
            
            if keys:
                deleted = self.redis.delete(*keys)
                logger.info(f"Cleared {deleted} cached embeddings for model {self.model_name}")
                return deleted
            
            return 0
            
        except Exception as e:
            logger.error(f"Error clearing cache: {e}")
            return 0
    
    def get_stats(self) -> dict:
        """Obtém estatísticas de cache"""
        if not self.redis or not RedisClient.is_available():
            return {"available": False}
        
        try:
            pattern = f"embedding:{self.model_name}:*"
            keys = list(self.redis.scan_iter(match=pattern, count=100))
            
            return {
                "available": True,
                "model": self.model_name,
                "cached_embeddings": len(keys),
                "ttl_days": self.ttl_seconds / (24 * 60 * 60)
            }
        except Exception as e:
            logger.error(f"Error getting cache stats: {e}")
            return {"available": False, "error": str(e)}
