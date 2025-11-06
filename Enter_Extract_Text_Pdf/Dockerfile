FROM python:3.11-slim

WORKDIR /app

# Instalar dependências do sistema necessárias para PyMuPDF e curl para healthcheck
RUN apt-get update && apt-get install -y \
    gcc \
    g++ \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copiar requirements e instalar dependências Python
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Baixar modelo spaCy
RUN python -m spacy download pt_core_news_lg

# Copiar o código da aplicação
COPY . .

# Expor a porta
EXPOSE 5000

# Comando para executar a aplicação em produção
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "5000", "--workers", "2"]
