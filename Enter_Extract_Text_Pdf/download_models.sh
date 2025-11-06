#!/bin/bash
# Script para baixar modelos necessários

echo "📦 Instalando modelos de NLP..."

# Modelo spaCy para português
echo "⬇️ Baixando modelo spaCy pt_core_news_lg..."
python -m spacy download pt_core_news_lg

echo "✅ Modelos instalados com sucesso!"
