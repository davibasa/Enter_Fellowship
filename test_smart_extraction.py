"""
Script de teste para Smart Extraction
"""
import requests
import json
import time

# Configuração
BASE_URL = "http://localhost:5001"

# Texto de exemplo
SAMPLE_TEXT = """
CADASTRO DE CLIENTE

Nome Completo: João da Silva Santos
CPF: 123.456.789-00
Data de Nascimento: 15/03/1985
E-mail: joao.silva@email.com
Telefone: (11) 98765-4321

Endereço
Rua das Flores, 123
Bairro Jardim Paulista
São Paulo - SP
CEP: 01234-567

Observações
Cliente preferencial com desconto de 10%
Ativo desde 2020
"""

# Schema
SCHEMA = {
    "nome_completo": "Nome completo do cliente",
    "cpf": "CPF do cliente",
    "data_nascimento": "Data de nascimento no formato dd/mm/yyyy",
    "email": "Endereço de e-mail do cliente",
    "telefone": "Número de telefone celular",
    "endereco_completo": "Endereço completo com rua, número, bairro, cidade e CEP",
    "observacoes": "Observações sobre o cliente"
}

def test_health():
    """Testa health check"""
    print("🏥 Testando health check...")
    response = requests.get(f"{BASE_URL}/health")
    print(f"Status: {response.status_code}")
    print(f"Response: {json.dumps(response.json(), indent=2)}")
    print()

def test_cache_stats():
    """Testa estatísticas do cache"""
    print("📊 Testando cache stats...")
    response = requests.get(f"{BASE_URL}/cache/stats")
    print(f"Status: {response.status_code}")
    print(f"Response: {json.dumps(response.json(), indent=2)}")
    print()

def test_smart_extract(label="test_cadastro"):
    """Testa smart extraction"""
    print(f"🚀 Testando Smart Extraction (label: {label})...")
    
    payload = {
        "text": SAMPLE_TEXT,
        "schema": SCHEMA,
        "label": label,
        "confidence_threshold": 0.6
    }
    
    start = time.time()
    response = requests.post(
        f"{BASE_URL}/smart-extract",
        json=payload,
        headers={"Content-Type": "application/json"}
    )
    elapsed = time.time() - start
    
    print(f"Status: {response.status_code}")
    print(f"Tempo: {elapsed:.2f}s")
    
    if response.status_code == 200:
        data = response.json()
        print(f"\n📊 Resultado:")
        print(f"  Cache Hit: {data.get('cache_hit')}")
        print(f"  Confiança Média: {data.get('avg_confidence', 0):.2f}")
        print(f"  Tempo Processamento: {data.get('processing_time_ms')}ms")
        print(f"  Métodos Usados: {data.get('methods_used')}")
        print(f"  GPT Fallback: {data.get('gpt_fallback_used')}")
        
        print(f"\n📝 Campos Extraídos:")
        for field_name, field_data in data.get('fields', {}).items():
            value = field_data.get('value', 'N/A')
            conf = field_data.get('confidence', 0)
            method = field_data.get('method', 'unknown')
            print(f"  • {field_name}:")
            print(f"    Valor: {value}")
            print(f"    Confiança: {conf:.2f}")
            print(f"    Método: {method}")
    else:
        print(f"❌ Erro: {response.text}")
    
    print()

def run_all_tests():
    """Executa todos os testes"""
    print("="*60)
    print("🧪 INICIANDO TESTES DO SMART EXTRACTION")
    print("="*60)
    print()
    
    try:
        # 1. Health check
        test_health()
        
        # 2. Cache stats
        test_cache_stats()
        
        # 3. Smart extract (primeira vez - MISS)
        test_smart_extract("test_v1")
        
        # 4. Smart extract (segunda vez - HIT)
        print("🔄 Testando novamente (deve vir do cache)...")
        test_smart_extract("test_v1")
        
        # 5. Cache stats após extrações
        test_cache_stats()
        
        print("="*60)
        print("✅ TESTES CONCLUÍDOS")
        print("="*60)
        
    except requests.exceptions.ConnectionError:
        print("❌ Erro: Não foi possível conectar à API")
        print("Certifique-se de que o serviço está rodando em", BASE_URL)
    except Exception as e:
        print(f"❌ Erro inesperado: {e}")

if __name__ == "__main__":
    run_all_tests()
