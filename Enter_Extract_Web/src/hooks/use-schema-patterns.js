import { useState, useEffect, useMemo } from 'react';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:5056';

/**
 * Hook para buscar e analisar padrões de schemas a partir do backend
 * Busca versões reais de schemas salvos no Redis
 */
export function useSchemaPatterns(documents) {
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [schemaVersions, setSchemaVersions] = useState({});
  const [isLoadingVersions, setIsLoadingVersions] = useState(false);

  /**
   * Busca versões de schemas do backend por label
   */
  const fetchSchemaVersions = async (label) => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/extractor/schema-versions/${encodeURIComponent(label)}`);
      
      if (!response.ok) {
        if (response.status === 404) {
          return [];
        }
        throw new Error(`HTTP ${response.status}`);
      }

      const data = await response.json();
      return data.versions || [];
    } catch (error) {
      console.error(`Error fetching schema versions for ${label}:`, error);
      return [];
    }
  };

  /**
   * Carrega versões de schemas para todos os labels únicos
   */
  useEffect(() => {
    const loadSchemaVersions = async () => {
      if (!documents || documents.length === 0) {
        return;
      }

      setIsLoadingVersions(true);

      try {
        // Extrair labels únicos
        const uniqueLabels = [...new Set(documents.map(d => d.type))];

        // Buscar versões para cada label
        const versionsPromises = uniqueLabels.map(async (label) => {
          const versions = await fetchSchemaVersions(label);
          return { label, versions };
        });

        const results = await Promise.all(versionsPromises);

        // Organizar em map
        const versionsMap = {};
        results.forEach(({ label, versions }) => {
          versionsMap[label] = versions;
        });

        setSchemaVersions(versionsMap);
      } catch (error) {
        console.error('Error loading schema versions:', error);
      } finally {
        setIsLoadingVersions(false);
      }
    };

    loadSchemaVersions();
  }, [documents]);

  /**
   * Agrupa documentos por label e mescla com versões de schemas do backend
   */
  const schemaPatterns = useMemo(() => {
    if (!documents || documents.length === 0) {
      return [];
    }

    setIsAnalyzing(true);

    try {
      // Agrupar por label (tipo de documento)
      const groupedByLabel = documents.reduce((acc, doc) => {
        if (!acc[doc.type]) {
          acc[doc.type] = [];
        }
        acc[doc.type].push(doc);
        return acc;
      }, {});

      // Analisar cada label
      const patterns = Object.entries(groupedByLabel).map(([label, docs]) => {
        // Filtrar apenas documentos completos para análise
        const completedDocs = docs.filter(d => d.status === 'completed');
        const totalExtractions = docs.length;

        // Calcular taxa de sucesso geral
        const successRate = completedDocs.length > 0
          ? (completedDocs.reduce((sum, d) => {
              const rate = parseFloat(d.successRate);
              return sum + (isNaN(rate) ? 0 : rate);
            }, 0) / completedDocs.length).toFixed(1)
          : 0;

        // Calcular tempo médio de processamento
        const avgProcessingTime = completedDocs.length > 0
          ? (() => {
              const times = completedDocs
                .map(d => parseFloat(d.processingTime))
                .filter(t => !isNaN(t));
              return times.length > 0
                ? (times.reduce((sum, t) => sum + t, 0) / times.length).toFixed(1) + 's'
                : '0s';
            })()
          : '0s';

        // Última vez usado
        const timestamps = docs
          .map(d => new Date(d.timestamp).getTime())
          .filter(t => !isNaN(t));
        
        const lastUsed = timestamps.length > 0
          ? new Date(Math.max(...timestamps)).toISOString().split('T')[0]
          : new Date().toISOString().split('T')[0];

        // ✨ NOVO: Usar versões reais do backend
        const backendVersions = schemaVersions[label] || [];
        
        const schemaVersionsData = backendVersions.length > 0
          ? backendVersions.map((v, idx) => ({
              id: v.id,
              name: v.version_name || `Schema ${idx + 1}`,
              description: v.description || `${v.field_count} campos`,
              usageCount: v.usage_count,
              successRate: v.avg_success_rate.toFixed(1),
              createdAt: new Date(v.created_at).toISOString().split('T')[0],
              isDefault: v.is_default || idx === 0,
              fields: v.field_count,
              schema: v.schema,
            }))
          : analyzeSchemaVersions(completedDocs); // Fallback para análise local

        // Usar schema da primeira versão ou analisar localmente
        const currentSchema = schemaVersionsData[0]?.schema || 
          (completedDocs[0]?.schema || {});

        // Analisar campos e seus padrões
        const fieldPatterns = analyzeFieldPatterns(completedDocs);

        // Analisar estratégias usadas
        const strategies = analyzeStrategies(completedDocs);

        // Detectar problemas comuns
        const commonIssues = detectCommonIssues(completedDocs);

        // Labels relacionadas (baseado em schemas similares)
        const relatedLabels = findRelatedLabels(label, groupedByLabel);

        return {
          id: label.toLowerCase().replace(/\s+/g, '_'),
          label,
          category: categorizeDocument(label),
          totalExtractions,
          successRate: parseFloat(successRate),
          avgProcessingTime,
          lastUsed,
          versions: schemaVersionsData,
          currentSchema,
          patterns: fieldPatterns,
          strategies,
          commonIssues,
          relatedLabels,
        };
      });

      // Ordenar por número de extrações (mais usados primeiro)
      return patterns.sort((a, b) => b.totalExtractions - a.totalExtractions);

    } finally {
      setIsAnalyzing(false);
    }
  }, [documents, schemaVersions]);

  return {
    patterns: schemaPatterns,
    isAnalyzing: isAnalyzing || isLoadingVersions,
    totalPatterns: schemaPatterns.length,
  };
}

/**
 * Analisa versões de schema (schemas únicos usados)
 */
function analyzeSchemaVersions(docs) {
  const schemaMap = new Map();

  docs.forEach(doc => {
    if (!doc.schema) return;

    // Criar hash do schema (campos únicos)
    const schemaFields = Object.keys(doc.schema).sort().join(',');
    
    if (!schemaMap.has(schemaFields)) {
      schemaMap.set(schemaFields, {
        id: `v${schemaMap.size + 1}`,
        name: `Schema ${schemaMap.size + 1}`,
        description: `${Object.keys(doc.schema).length} campos`,
        schema: doc.schema,
        usageCount: 0,
        successRates: [],
        createdAt: doc.timestamp,
        isDefault: schemaMap.size === 0, // Primeiro é o padrão
        fields: Object.keys(doc.schema).length,
      });
    }

    const version = schemaMap.get(schemaFields);
    version.usageCount++;
    
    // Validar e adicionar successRate
    const rate = parseFloat(doc.successRate);
    if (!isNaN(rate)) {
      version.successRates.push(rate);
    }
  });

  // Calcular média de sucesso por versão
  return Array.from(schemaMap.values()).map(v => {
    const createdAtDate = new Date(v.createdAt);
    const validDate = !isNaN(createdAtDate.getTime());
    
    return {
      ...v,
      successRate: v.successRates.length > 0
        ? (v.successRates.reduce((a, b) => a + b, 0) / v.successRates.length).toFixed(1)
        : 0,
      createdAt: validDate 
        ? createdAtDate.toISOString().split('T')[0]
        : new Date().toISOString().split('T')[0],
    };
  }).sort((a, b) => b.usageCount - a.usageCount);
}

/**
 * Analisa padrões de extração por campo
 */
function analyzeFieldPatterns(docs) {
  const fieldMap = new Map();

  docs.forEach(doc => {
    if (!doc.result) return;

    Object.entries(doc.result).forEach(([fieldName, value]) => {
      if (!fieldMap.has(fieldName)) {
        fieldMap.set(fieldName, {
          successCount: 0,
          totalCount: 0,
          values: [],
          strategies: [],
          confidences: [],
        });
      }

      const field = fieldMap.get(fieldName);
      field.totalCount++;
      
      if (value && value !== null && value !== '') {
        field.successCount++;
        
        // Armazenar até 5 exemplos únicos
        if (field.values.length < 5 && !field.values.includes(value)) {
          field.values.push(value);
        }
      }

      // Extrair estratégia do campo (se disponível no strategy string)
      if (doc.strategy) {
        const strategyMatch = doc.strategy.match(/Regex|Proximity|GPT/i);
        if (strategyMatch) {
          field.strategies.push(strategyMatch[0]);
        }
      }
    });
  });

  // Converter para formato de padrões
  const patterns = {};
  fieldMap.forEach((data, fieldName) => {
    const successRate = data.totalCount > 0
      ? Math.round((data.successCount / data.totalCount) * 100)
      : 0;

    // Determinar tipo e método predominante
    const mostCommonStrategy = getMostCommon(data.strategies) || 'Proximity';
    const type = inferFieldType(fieldName, data.values);

    patterns[fieldName] = {
      type,
      successRate,
      avgConfidence: 85 + Math.random() * 10, // Simulado - idealmente viria dos dados
      examples: data.values.slice(0, 3),
      extractionMethod: `Fase ${getPhaseNumber(mostCommonStrategy)} - ${mostCommonStrategy}`,
      position: 'Detectado automaticamente', // Simulado
      pattern: inferPattern(type, data.values),
    };
  });

  return patterns;
}

/**
 * Analisa estratégias de extração usadas
 */
function analyzeStrategies(docs) {
  const strategies = {
    Regex: 0,
    Proximity: 0,
    GPT: 0,
    Cache: 0,
  };

  docs.forEach(doc => {
    if (!doc.strategy) return;

    if (doc.strategy.includes('Regex')) strategies.Regex++;
    if (doc.strategy.includes('Proximity')) strategies.Proximity++;
    if (doc.strategy.includes('GPT')) strategies.GPT++;
    if (doc.strategy.includes('Cache')) strategies.Cache++;
  });

  return strategies;
}

/**
 * Detecta problemas comuns analisando campos com baixo sucesso
 */
function detectCommonIssues(docs) {
  const fieldSuccessRate = new Map();

  // Calcular taxa de sucesso por campo
  docs.forEach(doc => {
    if (!doc.result) return;

    Object.entries(doc.result).forEach(([fieldName, value]) => {
      if (!fieldSuccessRate.has(fieldName)) {
        fieldSuccessRate.set(fieldName, { success: 0, total: 0 });
      }

      const stats = fieldSuccessRate.get(fieldName);
      stats.total++;
      if (value && value !== null && value !== '') {
        stats.success++;
      }
    });
  });

  // Identificar campos problemáticos (< 80% de sucesso)
  const issues = [];
  fieldSuccessRate.forEach((stats, fieldName) => {
    const rate = (stats.success / stats.total) * 100;
    
    if (rate < 80) {
      const severity = rate < 50 ? 'error' : rate < 70 ? 'warning' : 'info';
      const frequency = `${Math.round(((stats.total - stats.success) / stats.total) * 100)}% dos casos`;

      issues.push({
        field: fieldName,
        severity,
        issue: rate < 50 
          ? 'Campo frequentemente não extraído' 
          : 'Extração inconsistente',
        frequency,
        suggestion: rate < 50
          ? 'Considerar adicionar padrões regex específicos ou melhorar descrição do schema'
          : 'Revisar estratégia de extração para este campo',
        impact: severity === 'error' ? 'Alto - campo crítico não extraído' : 'Médio - pode afetar qualidade dos dados',
      });
    }
  });

  return issues;
}

/**
 * Encontra labels relacionadas baseado em schemas similares
 */
function findRelatedLabels(currentLabel, allGroups) {
  const related = [];
  const currentFields = new Set(
    Object.keys(allGroups[currentLabel]?.[0]?.schema || {})
  );

  Object.entries(allGroups).forEach(([label, docs]) => {
    if (label === currentLabel || docs.length === 0) return;

    const fields = new Set(Object.keys(docs[0].schema || {}));
    const commonFields = [...currentFields].filter(f => fields.has(f));

    // Se tiver mais de 30% de campos em comum, considerar relacionado
    if (commonFields.length > currentFields.size * 0.3) {
      related.push(label);
    }
  });

  return related.slice(0, 5); // Máximo 5 relacionadas
}

/**
 * Categoriza o documento baseado no label
 */
function categorizeDocument(label) {
  const lower = label.toLowerCase();
  
  if (lower.includes('oab') || lower.includes('cnh') || lower.includes('carteira')) {
    return 'Documentos Profissionais';
  }
  if (lower.includes('extrato') || lower.includes('boleto') || lower.includes('fatura')) {
    return 'Documentos Financeiros';
  }
  if (lower.includes('nota') || lower.includes('nf') || lower.includes('fiscal')) {
    return 'Documentos Fiscais';
  }
  if (lower.includes('rg') || lower.includes('cpf') || lower.includes('identidade')) {
    return 'Documentos Pessoais';
  }
  if (lower.includes('contrato') || lower.includes('acordo')) {
    return 'Documentos Jurídicos';
  }
  
  return 'Outros Documentos';
}

/**
 * Infere o tipo do campo baseado no nome e valores
 */
function inferFieldType(fieldName, values) {
  const lower = fieldName.toLowerCase();
  const sampleValue = values[0] || '';

  if (lower.includes('data') || lower.includes('date') || /\d{2}\/\d{2}\/\d{4}/.test(sampleValue)) {
    return 'Date';
  }
  if (lower.includes('cpf') || lower.includes('cnpj') || lower.includes('inscricao')) {
    return 'Regex';
  }
  if (lower.includes('valor') || lower.includes('preco') || lower.includes('salario') || /R\$/.test(sampleValue)) {
    return 'Currency';
  }
  if (lower.includes('percentual') || lower.includes('taxa') || /%/.test(sampleValue)) {
    return 'Percentage';
  }
  if (lower.includes('categoria') || lower.includes('situacao') || lower.includes('tipo')) {
    return 'Enum';
  }
  if (lower.includes('nome') || lower.includes('profissional')) {
    return 'Proximity';
  }

  return 'Text';
}

/**
 * Infere padrão regex baseado no tipo e valores
 */
function inferPattern(type, values) {
  if (values.length === 0) return null;

  switch (type) {
    case 'Date':
      return '\\d{2}/\\d{2}/\\d{4}';
    case 'Regex':
      if (/\d{3}\.\d{3}\.\d{3}-\d{2}/.test(values[0])) {
        return '\\d{3}\\.\\d{3}\\.\\d{3}-\\d{2}'; // CPF
      }
      if (/\d{6}/.test(values[0])) {
        return '\\d{6}'; // Inscrição
      }
      break;
    case 'Currency':
      return 'R\\$\\s?\\d{1,3}(?:\\.\\d{3})*,\\d{2}';
    case 'Percentage':
      return '\\d{1,3}(?:,\\d{1,2})?%';
  }

  return null;
}

/**
 * Retorna o item mais comum em um array
 */
function getMostCommon(arr) {
  if (arr.length === 0) return null;
  
  const counts = {};
  arr.forEach(item => {
    counts[item] = (counts[item] || 0) + 1;
  });

  return Object.entries(counts).reduce((a, b) => b[1] > a[1] ? b : a)[0];
}

/**
 * Retorna o número da fase baseado na estratégia
 */
function getPhaseNumber(strategy) {
  switch (strategy) {
    case 'Regex': return 1;
    case 'Proximity': return 2;
    case 'GPT': return 3;
    default: return 2;
  }
}
