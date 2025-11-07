"use client";

import { useState, useMemo, useEffect } from "react";
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
  Loader2,
} from "lucide-react";
import { useRouter } from "next/navigation";
import { useExtractionHistory } from "@/hooks/use-extraction-history";
import { useSchemaPatterns } from "@/hooks/use-schema-patterns";

export default function SchemaPatternsPage() {
  const router = useRouter();
  const { documents, isLoading, error } = useExtractionHistory("default-user");
  const { patterns: schemaPatterns, isAnalyzing } = useSchemaPatterns(documents);
  
  const [searchTerm, setSearchTerm] = useState("");
  const [expandedField, setExpandedField] = useState(null);
  const [selectedPatternId, setSelectedPatternId] = useState(null);
  const [selectedVersionId, setSelectedVersionId] = useState(null);

  // Derivar padrão e versão selecionados
  const selectedPattern = selectedPatternId 
    ? schemaPatterns.find(p => p.id === selectedPatternId)
    : schemaPatterns[0];
    
  const selectedVersion = selectedPattern && selectedVersionId
    ? selectedPattern.versions.find(v => v.id === selectedVersionId)
    : selectedPattern?.versions?.[0];

  const filteredPatterns = schemaPatterns.filter(
    (pattern) =>
      pattern.label.toLowerCase().includes(searchTerm.toLowerCase()) ||
      pattern.category.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const handlePatternSelect = (pattern) => {
    setSelectedPatternId(pattern.id);
    const defaultVersion = pattern.versions.find((v) => v.isDefault) || pattern.versions[0];
    setSelectedVersionId(defaultVersion?.id);
  };

  // Estados de carregamento e erro
  if (isLoading || isAnalyzing) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center space-y-4">
          <Loader2 className="h-8 w-8 animate-spin mx-auto text-primary" />
          <p className="text-muted-foreground">
            {isAnalyzing ? "Analisando padrões..." : "Carregando dados..."}
          </p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center space-y-4">
          <AlertCircle className="h-12 w-12 mx-auto text-red-500" />
          <h2 className="text-xl font-semibold">Erro ao carregar padrões</h2>
          <p className="text-muted-foreground">{error}</p>
          <Button onClick={() => window.location.reload()}>Tentar Novamente</Button>
        </div>
      </div>
    );
  }

  if (schemaPatterns.length === 0) {
    return (
      <div className="min-h-screen space-y-4">
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
        </div>
        <Card>
          <CardContent className="pt-6 text-center space-y-4">
            <Database className="h-12 w-12 mx-auto text-muted-foreground" />
            <div>
              <h3 className="font-semibold mb-2">Nenhum padrão disponível</h3>
              <p className="text-sm text-muted-foreground mb-4">
                Comece fazendo extrações de documentos para o sistema aprender padrões.
              </p>
              <Button onClick={() => router.push("/upload")}>
                Fazer Upload de Documentos
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Dados mockados removidos - agora usamos dados reais

  const getTypeColor = (type) => {
    const colors = {
      Regex: "text-blue-600 bg-blue-50 border-blue-200",
      Date: "text-blue-600 bg-blue-50 border-blue-200",
      Enum: "text-green-600 bg-green-50 border-green-200",
      Proximity: "text-purple-600 bg-purple-50 border-purple-200",
      Currency: "text-yellow-600 bg-yellow-50 border-yellow-200",
      Percentage: "text-orange-600 bg-orange-50 border-orange-200",
      Text: "text-gray-600 bg-gray-50 border-gray-200",
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
                        onClick={() => setSelectedVersionId(version.id)}
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
