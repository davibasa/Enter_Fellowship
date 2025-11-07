# Enter Fellowship – Extração Inteligente Progressiva de Documentos

Sistema híbrido de extração estruturada de PDFs que combina heurísticas rápidas, Machine Learning local e fallback inteligente via LLM, com forte foco em custo mínimo e latência previsível.

## Objetivo

Extrair campos estruturados (ex.: nome, CPF, data de validade, categoria, endereço) de documentos PDF heterogêneos com:

- Baixo custo (LLM só em último caso)
- Alta reutilização (cache multi-camada + reutilização por campo)
- Evolução incremental de schemas sem reprocessar tudo

## Desafios Mapeados

| Desafio                                            | Por que é difícil                                          | Decisão                                                 | Estado                                            |
| -------------------------------------------------- | ---------------------------------------------------------- | ------------------------------------------------------- | ------------------------------------------------- |
| Identificar campos com formato rígido (CPF, datas) | Fácil por regex, mas misturados em texto sujo              | Regex + normalização                                    | Resolvido                                         |
| Campos sem padrão (nome, endereço, órgão emissor)  | Não funciona bem com regex                                 | Embeddings + n-grams + similaridade                     | Resolvido                                         |
| Baixa precisão e lentidão do few/zero-shot puro    | Alto custo e baixa taxa de acerto em português misto       | Substituído por mini-modelo de embeddings multilíngue   | Resolvido                                         |
| Poluição textual (labels confundindo valores)      | Modelo retorna a própria label em vez do valor             | Remoção heurística + detecção semântica de labels       | Parcial (label detection integrada em background) |
| Evitar reprocessamento quando schema muda          | Novos campos sobre mesmo PDF                               | Cache por campo + merge incremental                     | Resolvido                                         |
| Reduzir custo GPT                                  | LLM devolvendo dados já óbvios                             | Pipeline progressivo (Cache → Regex → Embeddings → GPT) | Resolvido                                         |
| Escala em lote com feedback                        | Processar centenas de PDFs sem bloquear UI                 | SSE streaming + job state interno                       | Resolvido                                         |
| Persistência distinta: volátil vs histórica        | Misturar itens efêmeros com histórico degrada consistência | Dois Redis: cache (LRU) e storage (durável)             | Resolvido                                         |

## Soluções Aplicadas

- Hash de conteúdo (SHA256) para endereçar PDF de forma idempotente independentemente de nome de arquivo.
- Cache multi-camada (resultado completo, campo isolado, texto bruto).
- Remoção seletiva de labels/keywords para reduzir ruído sem apagar valores candidatos.
- N-grams adaptativos (1–5 tokens) gerando janelas semânticas leves para similaridade.
- Fallback GPT depois de regex com campos númericos, datas, padrões
- Detecção assíncrona de labels (fire-and-forget) preparando terreno para limpeza ainda mais precisa em futuras requisições.
- Stream SSE para batch jobs: progressivo, item-complete, resumo final.
- Versionamento automático de schemas para rastreabilidade de evolução e comparação de acurácia.

## Não solucionado

- Não consegui solucionar um problema em que quando procurava datas ou valores com o regex ele não pegava o referente ao campo mas o primeiro ao qual encontrava. Pensei em utilizar um sistema de embeddings para fazer proximidade da palavra chave com a data mas não deu eficaz.
- Algumas vezes na alucinação a LLM acaba direcionando valores para campos errados.
- Não consegui reduzir o máximo de número de tokens possíveis para a LLM, gostaria de fazer uma sugestão de chave-valor com embedding e zero-shot mas ficou com uma má acurácia.

## Arquitetura

```
Cliente (Next.js) → API .NET (8080) → Redis Cache (6379) / Redis Storage (6380)
															↘
															 Python FastAPI (5000) → Embeddings / NER / Fallback GPT
```

Dois Redis distintos:

- Cache (LRU, sem persistência) para velocidade;
- Storage (RDB + AOF) para histórico, schemas, estatísticas e rastreabilidade.

## Fluxo Simplificado

1. Recebe requisição com label + schema + PDF (base64).
2. Calcula pdfHash → tenta cache completo.
3. Se falhar, busca valores já extraídos campo a campo (cache parcial).
4. Aplica Regex/Enum para campos formais → remove valores do texto residual.
5. Limpa labels/keywords → gera n-grams → similaridade embeddings.
6. Campos abaixo do threshold → Fallback GPT (modelo econômico).
7. Merge final + salvamento multi-camada + histórico.
8. Dispara detecção semântica de labels em background.

## Como Usar (Quick Start)

OBS: Pode demorar subir pois há alguns modelos de NLP que são instalados durante o processo

### 1. Pré-requisitos

- Docker
- Docker Compose
- Git

### 2. Clonar

```bash
git clone https://github.com/davibasa/Enter_Fellowship.git
cd Enter_Fellowship
```

### 3. Subir stack

Alterar OPENAI_API_KEY no docker-compose.yml na seção do BACKEND PYTHON:

```dockerfile
environment:
      - REDIS_CACHE_URL=redis://redis-cache:6379/0
      - REDIS_STORAGE_URL=redis://redis-storage:6379/0
      - OPENAI_API_KEY=API_KEY
```

```powershell
docker compose up -d
```

Serviços principais:

- API .NET: http://localhost:8080
- Frontend: http://localhost:3000
- Python FastAPI: http://localhost:5000
- Redis Commander: http://localhost:8082

### 5. Verificar saúde

```
GET http://localhost:5000/health
GET http://localhost:8080/health (se exposto)
```

### 6. Extração simples

```http
POST http://localhost:8080/api/extractor
Content-Type: application/json

{
	"label": "CNH",
	"pdfBase64": "JVBERi0xLjQK...",
	"extractionSchema": {
		"nome": "Nome completo",
		"cpf": "Cadastro de Pessoa Física",
		"categoria": "Categoria da CNH (pode ser A, B, C, D, E, AB)"
	}
}
```

### 7. Exemplo de resposta

```json
{
  "schema": {
    "nome": "João Silva",
    "cpf": "123.456.789-10",
    "categoria": "AB"
  },
  "processingTimeMs": 274,
  "confidence": 0.93,
  "cache": {
    "hitType": "partial"
  }
}
```

### 8. Processamento em lote (SSE)

1. `POST /api/extractor/batch` → retorna `jobId`.
2. Abrir stream: `GET /api/extractor/batch/{jobId}/stream`.
3. Eventos: `progress`, `item-complete`, `complete`.

### 9. Usando a Plataforma Web

1. Acesse o http://localhost:3000/ e pela sidebar vá para Documentos e clique no botão preto que fica ao lado superior direito com a descrição Upload.
2. Na tela que abrir você poderá dar upload do pdf, esquema do json de extração, label do pdf, esquema do json de validação para avaliar resultado (opcional)
3. você poderá adicionar quantos pdfs quiser.
4. Ao final clique em extrair e você verá uma tela que dará os feedbacks de cada extração.
5. Na tela padrões de PDF você poderá ver alguns insights referentes aos esquemas de JSON de acordo com cada label de documento

## Estratégia de Qualidade

- Reutilização de valores evita divergência entre execuções.
- Hash garante idempotência.
- Próximo passo: testes unitários de classificadores e módulo de merge.

## Decisões de Design

- “Progressive Intelligence”: cada camada só acontece se a anterior falhar → evita desperdício.
- Separação cache vs storage: performance vs consistência histórica.
- Embeddings mini multilíngue: balanço ideal entre velocidade e semântica em PT/EN/ES.
- Fire-and-forget para label detection: prepara futuras otimizações sem adicionar latência ao caminho crítico.
- Remoção de ruído antes de ML reduz tokens e eleva precisão evitando eco de labels.

## Conceitos Aplicados

- Similaridade coseno em espaço de embeddings.
- N-grams dinâmicos para granularidade variável.
- Content-based addressing via hash SHA256.
- SSE para feedback contínuo sem WebSockets.
- Multi-cache: compartilhamento de fragmentos de resultado entre schemas evolutivos.
