"""
GPT Fallback for low-confidence extractions
"""
import logging
import json
import os
from typing import Dict, Any, Optional

logger = logging.getLogger(__name__)

# Cliente OpenAI (será inicializado se API key disponível)
openai_client = None
openai_available = False


def initialize_openai():
    """Inicializa cliente OpenAI se API key disponível"""
    global openai_client, openai_available
    
    api_key = os.getenv("OPENAI_API_KEY")
    
    if not api_key:
        logger.warning("⚠️ OPENAI_API_KEY não configurada. Fallback GPT desabilitado.")
        openai_available = False
        return False
    
    try:
        from openai import OpenAI
        openai_client = OpenAI(api_key=api_key)
        openai_available = True
        logger.info("✅ Cliente OpenAI inicializado")
        return True
        
    except ImportError:
        logger.error("❌ openai não instalado. Execute: pip install openai")
        openai_available = False
        return False
    except Exception as e:
        logger.error(f"❌ Erro ao inicializar OpenAI: {e}")
        openai_available = False
        return False


def call_gpt_fallback(
    schema: Dict[str, str],
    text: str,
    model: str = "gpt-5-mini"
) -> Dict[str, Any]:
    """
    Chama GPT para extrair campos quando confiança local é baixa
    
    Args:
        schema: Dict {campo: descrição}
        text: Texto extraído
        model: Modelo GPT a usar (padrão: gpt-5-mini)
        
    Returns:
        Dict com campos extraídos
    """
    if not openai_available or openai_client is None:
        return {}
    
    try:
        # Construir prompt estruturado
        schema_text = "\n".join([f"- {k}: {v}" for k, v in schema.items()])
        
        prompt = f"""Extraia os seguintes campos do texto abaixo.

CAMPOS A EXTRAIR:
{schema_text}

TEXTO:
{text}

INSTRUÇÕES:
1. Retorne APENAS um JSON válido
2. Formato: {{"campo": "valor ou null"}}

JSON:"""

        response = openai_client.chat.completions.create(
            model=model,
            messages=[
                {
                    "role": "system",
                    "content": "Você é um assistente especializado em extração de dados de documentos."
                },
                {
                    "role": "user",
                    "content": prompt
                }
            ],
            response_format={"type": "json_object"}
        )
        
        # Parse resposta
        content = response.choices[0].message.content
        result = json.loads(content)
        
        # Padronizar formato - retornar valores diretamente
        formatted_result = {}
        for field_name in schema.keys():
            value = result.get(field_name)
            # Retornar None se valor for "null" string ou vazio
            if value and value.lower() != "null":
                formatted_result[field_name] = value
            else:
                formatted_result[field_name] = None
        
        return formatted_result
        
    except json.JSONDecodeError as e:
        logger.error(f"❌ Erro ao parsear JSON do GPT: {e}")
        return {}
    except Exception as e:
        logger.error(f"❌ Erro no GPT fallback Foda: {e}")
        return {}


def call_gpt_for_field(
    field_name: str,
    field_description: str,
    text: str,
    context: Optional[str] = None
) -> Optional[Dict[str, Any]]:
    """
    Chama GPT para extrair um único campo específico
    
    Args:
        field_name: Nome do campo
        field_description: Descrição do campo
        text: Texto completo
        context: Contexto adicional (opcional)
        
    Returns:
        Dict com valor extraído ou None
    """
    if not openai_available or openai_client is None:
        return None
    
    try:
        context_text = f"\nCONTEXTO: {context}" if context else ""
        
        prompt = f"""Extraia o valor do campo "{field_name}" do texto abaixo.

CAMPO: {field_name}
DESCRIÇÃO: {field_description}{context_text}

TEXTO:
{text}

Retorne apenas o valor extraído (sem label, sem formatação extra).
Se não encontrar, retorne "null".

VALOR:"""

        response = openai_client.chat.completions.create(
            model="gpt-5-mini",
            messages=[
                {
                    "role": "system",
                    "content": "Extraia apenas o valor solicitado, sem texto adicional."
                },
                {
                    "role": "user",
                    "content": prompt
                }
            ],
            max_tokens=200
        )
        
        value = response.choices[0].message.content.strip()
        
        if value.lower() == "null" or not value:
            return None
        
        return {
            "value": value,
            "confidence": 0.80,
            "method": "gpt_single_field",
            "line_index": -1
        }
        
    except Exception as e:
        logger.error(f"❌ Erro ao extrair campo com GPT: {e}")
        return None


def estimate_gpt_cost(num_fields: int, text_length: int) -> Dict[str, Any]:
    """
    Estima custo de usar GPT fallback
    
    Args:
        num_fields: Número de campos
        text_length: Tamanho do texto em caracteres
        
    Returns:
        Dict com estimativa de custo
    """
    # gpt-5-mini pricing (Nov 2024)
    # Input: $0.15 / 1M tokens
    # Output: $0.60 / 1M tokens
    
    # Estimativa de tokens (aprox 4 chars = 1 token)
    input_tokens = text_length / 4 + num_fields * 20  # texto + schema
    output_tokens = num_fields * 10  # resposta JSON
    
    input_cost = (input_tokens / 1_000_000) * 0.15
    output_cost = (output_tokens / 1_000_000) * 0.60
    total_cost = input_cost + output_cost
    
    return {
        "estimated_input_tokens": int(input_tokens),
        "estimated_output_tokens": int(output_tokens),
        "estimated_cost_usd": round(total_cost, 6),
        "model": "gpt-5-mini"
    }
