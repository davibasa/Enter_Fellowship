"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import {
  ArrowLeft,
  Search,
  FileText,
  Database,
  TrendingUp,
  CheckCircle2,
  AlertCircle,
  Clock,
  Code,
  Brain,
  Target,
  Activity,
  ChevronRight,
  Download,
  Star,
  Layers,
  AlertTriangle,
  Link2,
} from "lucide-react";
import { useRouter } from "next/navigation";

// Dados mockados - Múltiplas versões de schema por label
const schemaPatterns = [
  {
    id: 1,
    label: "Carteira OAB",
    category: "Documentos Profissionais",
    totalExtractions: 156,
    successRate: 98.5,
    avgProcessingTime: "3.2s",
    lastUsed: "2025-11-06",
    versions: [
      {
        id: "v1",
        name: "Schema Completo",
        description: "Versão padrão com todos os campos disponíveis",
        usageCount: 120,
        successRate: 98.5,
        createdAt: "2025-09-15",
        isDefault: true,
        fields: 7,
      },
      {
        id: "v2",
        name: "Schema Básico",
        description: "Apenas dados essenciais de identificação",
        usageCount: 36,
        successRate: 99.2,
        createdAt: "2025-10-01",
        isDefault: false,
        fields: 4,
      },
    ],
    currentSchema: {
      nome: "Nome do profissional, normalmente no canto superior esquerdo",
      inscricao: "Número de inscrição do profissional",
      seccional: "Seccional do profissional (sigla do estado)",
      subsecao: "Subseção à qual o profissional faz parte",
      categoria: "Categoria, pode ser ADVOGADO, ADVOGADA, SUPLEMENTAR, ESTAGIARIO, ESTAGIARIA",
      situacao: "Situação do profissional, normalmente no canto inferior direito",
      telefone_profissional: "Telefone do profissional",
    },
    patterns: {
      inscricao: {
        type: "Regex",
        pattern: "\\d{6}",
        successRate: 100,
        avgConfidence: 98,
        examples: ["101943", "234567", "345678"],
        position: "Linha 2, após 'Inscrição'",
        extractionMethod: "Fase 1 - Regex",
      },
      seccional: {
        type: "Enum",
        values: ["PR", "SP", "RJ", "MG", "RS", "SC", "BA", "CE", "PE"],
        successRate: 100,
        avgConfidence: 100,
        examples: ["PR", "SP", "RJ"],
        position: "Mesma linha da inscrição, coluna 2",
        extractionMethod: "Fase 1 - Enum",
      },
      nome: {
        type: "Proximity",
        strategy: "First line before 'Inscrição'",
        successRate: 95,
        avgConfidence: 92,
        examples: ["JOANA D'ARC", "MARIA SILVA", "JOÃO SANTOS"],
        position: "Primeira linha do documento",
        extractionMethod: "Fase 2 - Proximidade",
      },
    },
    commonIssues: [
      {
        field: "telefone_profissional",
        severity: "warning",
        issue: "Campo frequentemente vazio",
        frequency: "40% dos casos",
        suggestion: "Considerar campo opcional ou remover do schema básico",
        impact: "Baixo - não afeta extração de outros campos",
      },
      {
        field: "subsecao",
        severity: "info",
        issue: "Nomes longos podem ser truncados",
        frequency: "15% dos casos",
        suggestion: "Usar campo MultiLine ou aumentar limite de caracteres",
        impact: "Médio - pode perder informação completa",
      },
    ],
    relatedLabels: ["Carteira OAB Digital", "Carteirinha OAB Estudante", "Comprovante OAB"],
  },
  {
    id: 2,
    label: "Extrato Bancário",
    category: "Documentos Financeiros",
    totalExtractions: 89,
    successRate: 92.3,
    avgProcessingTime: "4.1s",
    lastUsed: "2025-11-05",
    versions: [
      {
        id: "v1",
        name: "Schema Detalhado",
        description: "Inclui todos os campos e detalhes de saldo",
        usageCount: 67,
        successRate: 92.3,
        createdAt: "2025-08-20",
        isDefault: true,
        fields: 6,
      },
      {
        id: "v2",
        name: "Schema Resumido",
        description: "Apenas datas e total",
        usageCount: 22,
        successRate: 95.1,
        createdAt: "2025-09-10",
        isDefault: false,
        fields: 2,
      },
    ],
    currentSchema: {
      data_base: "Data base da operação selecionada",
      data_vencimento: "Data de vencimento da operação selecionada",
      sistema: "Sistema da operação selecionada",
      saldo_vencido: "Saldo vencido da operação",
      saldo_a_vencer: "Saldo a vencer",
      total_geral: "Total geral",
    },
    patterns: {
      data_base: {
        type: "Regex",
        pattern: "\\d{2}/\\d{2}/\\d{4}",
        successRate: 98,
        avgConfidence: 97,
        examples: ["05/09/2025", "12/10/2025"],
        position: "Após 'Data Referência:'",
        extractionMethod: "Fase 1 - Regex",
      },
      sistema: {
        type: "Enum",
        values: ["CONSIGNADO", "CRÉDITO DIRETO", "EMPRÉSTIMO", "FINANCIAMENTO"],
        successRate: 94,
        avgConfidence: 92,
        examples: ["CONSIGNADO", "CRÉDITO DIRETO"],
        position: "Coluna 'Sistema' em estrutura tabular",
        extractionMethod: "Fase 2 - Tabular",
      },
      total_geral: {
        type: "Currency",
        pattern: "\\d{1,3}(?:\\.\\d{3})*,\\d{2}",
        successRate: 93,
        avgConfidence: 90,
        examples: ["76.871,20", "45.632,10"],
        position: "Última coluna da tabela",
        extractionMethod: "Fase 2 - Tabular",
      },
    },
    commonIssues: [
      {
        field: "saldo_vencido",
        severity: "warning",
        issue: "Confusão com outros valores monetários",
        frequency: "12% dos casos",
        suggestion: "Usar contexto da coluna 'Saldo Vencido' para maior precisão",
        impact: "Alto - pode extrair valor incorreto",
      },
      {
        field: "sistema",
        severity: "info",
        issue: "Variações de nomenclatura entre bancos",
        frequency: "8% dos casos",
        suggestion: "Adicionar mais variações ao enum (CREDITO, EMPRESTIMO, etc)",
        impact: "Médio - fallback para GPT aumenta custo",
      },
    ],
    relatedLabels: ["Extrato Consolidado", "Extrato por Período", "Boleto Bancário"],
  },
  {
    id: 3,
    label: "Nota Fiscal",
    category: "Documentos Fiscais",
    totalExtractions: 234,
    successRate: 89.5,
    avgProcessingTime: "5.3s",
    lastUsed: "2025-11-06",
    versions: [
      {
        id: "v1",
        name: "Schema NFe Completo",
        description: "Todos os campos principais da NF-e",
        usageCount: 180,
        successRate: 89.5,
        createdAt: "2025-07-10",
        isDefault: true,
        fields: 7,
      },
      {
        id: "v2",
        name: "Schema Fiscal Simplificado",
        description: "Apenas identificação e valores",
        usageCount: 54,
        successRate: 93.2,
        createdAt: "2025-08-22",
        isDefault: false,
        fields: 4,
      },
    ],
    currentSchema: {
      numero_nf: "Número da nota fiscal",
      emitente: "Nome do emitente",
      cnpj_emitente: "CNPJ do emitente",
      destinatario: "Nome do destinatário",
      valor_total: "Valor total da nota fiscal",
      data_emissao: "Data de emissão",
      chave_acesso: "Chave de acesso da NFe",
    },
    patterns: {
      cnpj_emitente: {
        type: "Regex",
        pattern: "\\d{2}\\.\\d{3}\\.\\d{3}/\\d{4}-\\d{2}",
        successRate: 98,
        avgConfidence: 99,
        examples: ["12.345.678/0001-90", "98.765.432/0001-12"],
        position: "Seção 'Emitente', após nome",
        extractionMethod: "Fase 1 - Regex",
      },
      chave_acesso: {
        type: "Regex",
        pattern: "\\d{44}",
        successRate: 88,
        avgConfidence: 85,
        examples: ["12345678901234567890123456789012345678901234"],
        position: "Código de barras ou seção 'Chave de Acesso'",
        extractionMethod: "Fase 1 - Regex",
      },
    },
    commonIssues: [
      {
        field: "emitente",
        severity: "error",
        issue: "Nomes de empresa complexos requerem GPT",
        frequency: "35% dos casos",
        suggestion: "Tentar extrair via proximidade do CNPJ antes do GPT",
        impact: "Alto - aumenta custo e tempo de processamento",
      },
      {
        field: "chave_acesso",
        severity: "warning",
        issue: "Chave pode estar em formato de código de barras",
        frequency: "20% dos casos",
        suggestion: "Aplicar OCR especializado em códigos de barras",
        impact: "Médio - pode não detectar chave",
      },
      {
        field: "valor_total",
        severity: "info",
        issue: "Múltiplos valores no documento causam ambiguidade",
        frequency: "10% dos casos",
        suggestion: "Buscar especificamente por 'Valor Total da NF-e'",
        impact: "Alto - valor incorreto invalida documento",
      },
    ],
    relatedLabels: ["NFe Eletrônica", "Nota Fiscal de Serviço", "Cupom Fiscal"],
  },
  {
    id: 4,
    label: "RG",
    category: "Documentos Pessoais",
    totalExtractions: 67,
    successRate: 85.7,
    avgProcessingTime: "4.8s",
    lastUsed: "2025-11-04",
    versions: [
      {
        id: "v1",
        name: "Schema Completo",
        description: "Todos os dados do RG incluindo filiação",
        usageCount: 45,
        successRate: 85.7,
        createdAt: "2025-06-15",
        isDefault: true,
        fields: 7,
      },
      {
        id: "v2",
        name: "Schema Identificação",
        description: "Apenas dados de identificação básicos",
        usageCount: 22,
        successRate: 92.8,
        createdAt: "2025-07-20",
        isDefault: false,
        fields: 4,
      },
    ],
    currentSchema: {
      nome: "Nome completo",
      rg: "Número do RG",
      cpf: "CPF",
      data_nascimento: "Data de nascimento",
      naturalidade: "Naturalidade",
      nome_pai: "Nome do pai",
      nome_mae: "Nome da mãe",
    },
    patterns: {
      cpf: {
        type: "Regex",
        pattern: "\\d{3}\\.\\d{3}\\.\\d{3}-\\d{2}",
        successRate: 98,
        avgConfidence: 99,
        examples: ["123.456.789-00", "987.654.321-11"],
        position: "Campo 'CPF'",
        extractionMethod: "Fase 1 - Regex",
      },
      rg: {
        type: "Regex",
        pattern: "\\d{1,2}\\.?\\d{3}\\.?\\d{3}-?[0-9Xx]",
        successRate: 94,
        avgConfidence: 92,
        examples: ["12.345.678-9", "1234567X"],
        position: "Campo 'RG' ou 'Registro Geral'",
        extractionMethod: "Fase 1 - Regex",
      },
    },
    commonIssues: [
      {
        field: "nome_pai",
        severity: "error",
        issue: "Layout varia muito entre estados",
        frequency: "65% dos casos",
        suggestion: "Criar schemas específicos por estado (SSP-SP, SSP-RJ, etc)",
        impact: "Alto - frequentemente requer GPT",
      },
      {
        field: "nome_mae",
        severity: "error",
        issue: "Layout varia muito entre estados",
        frequency: "65% dos casos",
        suggestion: "Criar schemas específicos por estado",
        impact: "Alto - frequentemente requer GPT",
      },
      {
        field: "naturalidade",
        severity: "warning",
        issue: "Formato inconsistente (Cidade-UF vs apenas Cidade)",
        frequency: "30% dos casos",
        suggestion: "Normalizar formato após extração",
        impact: "Baixo - dado é extraído mas formato varia",
      },
    ],
    relatedLabels: ["CNH", "Carteira de Identidade", "Documento de Identificação"],
  },
];

export default function SchemaPatternsPage() {
  const router = useRouter();
  const [selectedPattern, setSelectedPattern] = useState(schemaPatterns[0]);
  const [selectedVersion, setSelectedVersion] = useState(schemaPatterns[0].versions[0]);
  const [searchTerm, setSearchTerm] = useState("");
  const [expandedField, setExpandedField] = useState(null);

  const filteredPatterns = schemaPatterns.filter(
    (pattern) =>
      pattern.label.toLowerCase().includes(searchTerm.toLowerCase()) ||
      pattern.category.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const handlePatternSelect = (pattern) => {
    setSelectedPattern(pattern);
    setSelectedVersion(pattern.versions.find((v) => v.isDefault) || pattern.versions[0]);
  };

  const getTypeColor = (type) => {
    const colors = {
      Regex: "text-blue-600 bg-blue-50 border-blue-200",
      Enum: "text-green-600 bg-green-50 border-green-200",
      Proximity: "text-purple-600 bg-purple-50 border-purple-200",
      Currency: "text-yellow-600 bg-yellow-50 border-yellow-200",
    };
    return colors[type] || "text-gray-600 bg-gray-50 border-gray-200";
  };

  const getSuccessRateColor = (rate) => {
    if (rate >= 95) return "text-green-600";
    if (rate >= 85) return "text-yellow-600";
    return "text-red-600";
  };

  const getSeverityColor = (severity) => {
    const colors = {
      error: "border-red-200 bg-red-50",
      warning: "border-yellow-200 bg-yellow-50",
      info: "border-blue-200 bg-blue-50",
    };
    return colors[severity] || "border-gray-200 bg-gray-50";
  };

  const getSeverityIcon = (severity) => {
    if (severity === "error") return <AlertTriangle className="h-4 w-4 text-red-600" />;
    if (severity === "warning") return <AlertCircle className="h-4 w-4 text-yellow-600" />;
    return <CheckCircle2 className="h-4 w-4 text-blue-600" />;
  };

  return (
    <div className="min-h-screen space-y-4 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => router.back()}
            className="h-8 w-8"
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold tracking-tight">Padrões de Schemas</h1>
            <p className="text-sm text-muted-foreground">
              Explore padrões aprendidos e versões de schema por tipo de documento
            </p>
          </div>
        </div>
        <Button variant="outline" size="sm">
          <Download className="mr-2 h-4 w-4" />
          Exportar Padrões
        </Button>
      </div>

      {/* Main Layout */}
      <div className="grid grid-cols-12 gap-4 h-[calc(100vh-10rem)]">
        {/* Sidebar - Lista de Labels */}
        <Card className="col-span-4 flex flex-col">
          <CardHeader className="pb-3">
            <div className="space-y-3">
              <CardTitle className="text-base flex items-center gap-2">
                <Database className="h-4 w-4" />
                Tipos de Documentos ({schemaPatterns.length})
              </CardTitle>
              <div className="relative">
                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Buscar schemas..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-8 h-9"
                />
              </div>
            </div>
          </CardHeader>
          <CardContent className="flex-1 p-0">
            <ScrollArea className="h-full px-4 pb-4">
              <div className="space-y-2">
                {filteredPatterns.map((pattern) => (
                  <div
                    key={pattern.id}
                    onClick={() => handlePatternSelect(pattern)}
                    className={`p-3 rounded-lg border cursor-pointer transition-all hover:shadow-md ${
                      selectedPattern?.id === pattern.id
                        ? "border-primary bg-primary/5 shadow-sm"
                        : "border-border hover:border-primary/50"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <FileText className="h-4 w-4 text-primary shrink-0" />
                          <span className="font-medium text-sm truncate">
                            {pattern.label}
                          </span>
                        </div>
                        <Badge variant="outline" className="text-xs mb-2">
                          {pattern.category}
                        </Badge>
                        <div className="flex items-center gap-1 mb-2">
                          <Layers className="h-3 w-3 text-muted-foreground" />
                          <span className="text-xs text-muted-foreground">
                            {pattern.versions.length} versões
                          </span>
                        </div>
                        <div className="space-y-1 text-xs text-muted-foreground">
                          <div className="flex items-center justify-between">
                            <span>Extrações:</span>
                            <span className="font-semibold">{pattern.totalExtractions}</span>
                          </div>
                          <div className="flex items-center justify-between">
                            <span>Taxa de Sucesso:</span>
                            <span
                              className={`font-semibold ${getSuccessRateColor(
                                pattern.successRate
                              )}`}
                            >
                              {pattern.successRate}%
                            </span>
                          </div>
                        </div>
                      </div>
                      <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0" />
                    </div>
                  </div>
                ))}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>

        {/* Main Content */}
        <div className="col-span-8 space-y-4 overflow-auto">
          {selectedPattern && (
            <>
              {/* Header Card */}
              <Card>
                <CardContent className="pt-6">
                  <div className="flex items-start justify-between mb-4">
                    <div>
                      <h2 className="text-xl font-bold mb-1">{selectedPattern.label}</h2>
                      <Badge variant="outline">{selectedPattern.category}</Badge>
                    </div>
                    <Button variant="outline" size="sm">
                      <Star className="mr-2 h-4 w-4" />
                      Usar Template
                    </Button>
                  </div>

                  {/* Stats Grid */}
                  <div className="grid grid-cols-4 gap-4">
                    <div className="text-center p-3 bg-secondary rounded-lg">
                      <Activity className="h-5 w-5 mx-auto mb-1 text-muted-foreground" />
                      <div className="text-2xl font-bold">
                        {selectedPattern.totalExtractions}
                      </div>
                      <div className="text-xs text-muted-foreground">Extrações</div>
                    </div>
                    <div className="text-center p-3 bg-secondary rounded-lg">
                      <TrendingUp className="h-5 w-5 mx-auto mb-1 text-green-600" />
                      <div className="text-2xl font-bold text-green-600">
                        {selectedPattern.successRate}%
                      </div>
                      <div className="text-xs text-muted-foreground">Sucesso</div>
                    </div>
                    <div className="text-center p-3 bg-secondary rounded-lg">
                      <Clock className="h-5 w-5 mx-auto mb-1 text-muted-foreground" />
                      <div className="text-2xl font-bold">
                        {selectedPattern.avgProcessingTime}
                      </div>
                      <div className="text-xs text-muted-foreground">Tempo Médio</div>
                    </div>
                    <div className="text-center p-3 bg-secondary rounded-lg">
                      <Layers className="h-5 w-5 mx-auto mb-1 text-purple-600" />
                      <div className="text-2xl font-bold">
                        {selectedPattern.versions.length}
                      </div>
                      <div className="text-xs text-muted-foreground">Versões</div>
                    </div>
                  </div>
                </CardContent>
              </Card>

              {/* Schema Versions */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-base flex items-center gap-2">
                    <Layers className="h-4 w-4" />
                    Versões de Schema ({selectedPattern.versions.length})
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3">
                    {selectedPattern.versions.map((version) => (
                      <div
                        key={version.id}
                        onClick={() => setSelectedVersion(version)}
                        className={`p-4 rounded-lg border cursor-pointer transition-all ${
                          selectedVersion?.id === version.id
                            ? "border-primary bg-primary/5"
                            : "border-border hover:border-primary/50"
                        }`}
                      >
                        <div className="flex items-start justify-between mb-2">
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <span className="font-semibold text-sm">{version.name}</span>
                              {version.isDefault && (
                                <Badge variant="default" className="text-xs">
                                  Padrão
                                </Badge>
                              )}
                            </div>
                            <p className="text-xs text-muted-foreground">
                              {version.description}
                            </p>
                          </div>
                        </div>
                        <div className="grid grid-cols-4 gap-3 text-xs">
                          <div>
                            <span className="text-muted-foreground">Uso:</span>
                            <div className="font-semibold">{version.usageCount}x</div>
                          </div>
                          <div>
                            <span className="text-muted-foreground">Sucesso:</span>
                            <div
                              className={`font-semibold ${getSuccessRateColor(
                                version.successRate
                              )}`}
                            >
                              {version.successRate}%
                            </div>
                          </div>
                          <div>
                            <span className="text-muted-foreground">Campos:</span>
                            <div className="font-semibold">{version.fields}</div>
                          </div>
                          <div>
                            <span className="text-muted-foreground">Criado:</span>
                            <div className="font-semibold">{version.createdAt}</div>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>

              {/* Schema Definition */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-base flex items-center gap-2">
                    <Code className="h-4 w-4" />
                    Campos do Schema ({Object.keys(selectedPattern.currentSchema).length})
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-2">
                    {Object.entries(selectedPattern.currentSchema).map(([key, description]) => (
                      <div
                        key={key}
                        className="p-3 bg-secondary rounded-lg hover:bg-secondary/80 transition-colors"
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div className="flex-1">
                            <code className="text-sm font-mono text-primary">{key}</code>
                            <p className="text-xs text-muted-foreground mt-1">
                              {description}
                            </p>
                          </div>
                          {selectedPattern.patterns[key] && (
                            <Badge
                              variant="outline"
                              className={getTypeColor(selectedPattern.patterns[key].type)}
                            >
                              {selectedPattern.patterns[key].type}
                            </Badge>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>

              {/* Patterns Learned */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-base flex items-center gap-2">
                    <Brain className="h-4 w-4" />
                    Padrões Identificados ({Object.keys(selectedPattern.patterns).length})
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3">
                    {Object.entries(selectedPattern.patterns).map(([field, pattern]) => (
                      <div key={field} className="border rounded-lg">
                        <div
                          onClick={() =>
                            setExpandedField(expandedField === field ? null : field)
                          }
                          className="p-3 cursor-pointer hover:bg-secondary/50 transition-colors"
                        >
                          <div className="flex items-center justify-between">
                            <div className="flex items-center gap-3">
                              <code className="text-sm font-mono font-semibold">
                                {field}
                              </code>
                              <Badge
                                variant="outline"
                                className={`${getTypeColor(pattern.type)} text-xs`}
                              >
                                {pattern.type}
                              </Badge>
                            </div>
                            <div className="flex items-center gap-3">
                              <Badge variant="secondary" className="text-xs">
                                {pattern.extractionMethod}
                              </Badge>
                              <div className="text-xs text-muted-foreground">
                                {pattern.successRate}%
                              </div>
                              <ChevronRight
                                className={`h-4 w-4 transition-transform ${
                                  expandedField === field ? "rotate-90" : ""
                                }`}
                              />
                            </div>
                          </div>
                        </div>

                        {expandedField === field && (
                          <div className="p-4 pt-0 space-y-3">
                            <Separator />

                            {/* Pattern Details */}
                            {pattern.pattern && (
                              <div>
                                <div className="text-xs font-semibold mb-1">
                                  Padrão Regex:
                                </div>
                                <code className="text-xs bg-secondary px-2 py-1 rounded">
                                  {pattern.pattern}
                                </code>
                              </div>
                            )}

                            {pattern.values && (
                              <div>
                                <div className="text-xs font-semibold mb-1">
                                  Valores Possíveis ({pattern.values.length}):
                                </div>
                                <div className="flex flex-wrap gap-1">
                                  {pattern.values.map((value) => (
                                    <Badge key={value} variant="secondary" className="text-xs">
                                      {value}
                                    </Badge>
                                  ))}
                                </div>
                              </div>
                            )}

                            {pattern.strategy && (
                              <div>
                                <div className="text-xs font-semibold mb-1">
                                  Estratégia:
                                </div>
                                <p className="text-xs text-muted-foreground">
                                  {pattern.strategy}
                                </p>
                              </div>
                            )}

                            {/* Stats */}
                            <div className="grid grid-cols-2 gap-3">
                              <div className="bg-secondary p-2 rounded">
                                <div className="text-xs text-muted-foreground">
                                  Taxa de Sucesso
                                </div>
                                <div
                                  className={`text-lg font-bold ${getSuccessRateColor(
                                    pattern.successRate
                                  )}`}
                                >
                                  {pattern.successRate}%
                                </div>
                              </div>
                              <div className="bg-secondary p-2 rounded">
                                <div className="text-xs text-muted-foreground">
                                  Confiança Média
                                </div>
                                <div className="text-lg font-bold">
                                  {pattern.avgConfidence}%
                                </div>
                              </div>
                            </div>

                            {/* Examples */}
                            <div>
                              <div className="text-xs font-semibold mb-1">
                                Exemplos Extraídos:
                              </div>
                              <div className="space-y-1">
                                {pattern.examples.map((example, idx) => (
                                  <div
                                    key={idx}
                                    className="text-xs bg-green-50 border border-green-200 px-2 py-1 rounded font-mono"
                                  >
                                    {example}
                                  </div>
                                ))}
                              </div>
                            </div>

                            {/* Position */}
                            <div>
                              <div className="text-xs font-semibold mb-1">
                                Posição no Documento:
                              </div>
                              <div className="flex items-center gap-2">
                                <Target className="h-3 w-3 text-muted-foreground" />
                                <p className="text-xs text-muted-foreground">
                                  {pattern.position}
                                </p>
                              </div>
                            </div>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>

              {/* Common Issues */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-base flex items-center gap-2">
                    <AlertTriangle className="h-4 w-4" />
                    Problemas Comuns ({selectedPattern.commonIssues.length})
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3">
                    {selectedPattern.commonIssues.map((issue, idx) => (
                      <div
                        key={idx}
                        className={`p-4 rounded-lg border ${getSeverityColor(
                          issue.severity
                        )}`}
                      >
                        <div className="flex items-start gap-3">
                          {getSeverityIcon(issue.severity)}
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <code className="text-sm font-mono font-semibold">
                                {issue.field}
                              </code>
                              <Badge variant="outline" className="text-xs">
                                {issue.frequency}
                              </Badge>
                            </div>
                            <p className="text-sm font-medium mb-2">{issue.issue}</p>
                            <div className="space-y-2 text-xs">
                              <div>
                                <span className="text-muted-foreground">Sugestão: </span>
                                <span>{issue.suggestion}</span>
                              </div>
                              <div>
                                <span className="text-muted-foreground">Impacto: </span>
                                <span>{issue.impact}</span>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>

              {/* Related Labels */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-base flex items-center gap-2">
                    <Link2 className="h-4 w-4" />
                    Labels Relacionadas ({selectedPattern.relatedLabels.length})
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="flex flex-wrap gap-2">
                    {selectedPattern.relatedLabels.map((label, idx) => (
                      <Badge key={idx} variant="outline" className="cursor-pointer hover:bg-secondary">
                        {label}
                      </Badge>
                    ))}
                  </div>
                </CardContent>
              </Card>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
