"use client";

import { useState, useEffect, useRef } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import {
  CheckCircle2,
  XCircle,
  Loader2,
  AlertCircle,
  Clock,
  ChevronRight,
  FileText,
  ArrowLeft,
  Download,
} from "lucide-react";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5056";

export default function ExtractResultsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const jobId = searchParams.get("jobId");
  const eventSourceRef = useRef(null);
  const jobStatusRef = useRef(null);

  const [jobStatus, setJobStatus] = useState({
    jobId: jobId || "",
    status: "connecting",
    totalItems: 0,
    processedItems: 0,
    successCount: 0,
    errorCount: 0,
  });

  const [results, setResults] = useState([]);
  const [selectedResult, setSelectedResult] = useState(null);
  const [connectionStatus, setConnectionStatus] = useState("connecting");
  const [error, setError] = useState(null);

  // Conectar ao SSE Stream
  useEffect(() => {
    if (!jobId) {
      return;
    }

    const connectSSE = () => {
      try {
        setConnectionStatus("connecting");
        const eventSource = new EventSource(`${API_BASE_URL}/api/extractor/batch/${jobId}/stream`);
        eventSourceRef.current = eventSource;

        eventSource.addEventListener("progress", (event) => {
          const data = JSON.parse(event.data);
          console.log("📊 Progress:", data);
          
          setJobStatus((prev) => {
            const newStatus = {
              ...prev,
              status: data.status,
              processedItems: data.processed,
              totalItems: data.total,
              successCount: data.successCount,
              errorCount: data.errorCount,
            };
            jobStatusRef.current = newStatus;
            return newStatus;
          });
          setConnectionStatus("connected");
        });

        eventSource.addEventListener("result", (event) => {
          const data = JSON.parse(event.data);
          console.log("📄 Result:", data);

          setResults((prev) => {
            const existingIndex = prev.findIndex((r) => r.fileId === data.fileId);
            if (existingIndex >= 0) {
              const updated = [...prev];
              updated[existingIndex] = {
                ...updated[existingIndex],
                status: data.status,
                data: data.data,
                error: data.error,
                processingTimeMs: data.processingTimeMs,
                usedCache: data.usedCache,
                cacheType: data.cacheType,
                validationData: data.validationData, // Dados de validação específicos deste item
              };
              return updated;
            } else {
              return [
                ...prev,
                {
                  fileId: data.fileId,
                  status: data.status,
                  data: data.data,
                  error: data.error,
                  processingTimeMs: data.processingTimeMs,
                  usedCache: data.usedCache,
                  cacheType: data.cacheType,
                  validationData: data.validationData, // Dados de validação específicos deste item
                },
              ];
            }
          });
        });

        eventSource.addEventListener("complete", (event) => {
          const data = JSON.parse(event.data);
          console.log("✅ Complete:", data);
          
          setJobStatus((prev) => ({
            ...prev,
            status: "completed",
            processedItems: data.totalItems,
            totalItems: data.totalItems,
            successCount: data.successCount,
            errorCount: data.errorCount,
          }));
          setConnectionStatus("completed");
          eventSource.close();
        });

        eventSource.addEventListener("error", (event) => {
          console.error("❌ SSE Error:", event);
          const errorData = event.data ? JSON.parse(event.data) : { message: "Erro de conexão" };
          setError(errorData.message || "Erro ao processar job");
          setConnectionStatus("error");
          eventSource.close();
        });

        eventSource.onerror = (err) => {
          console.error("❌ EventSource Error:", err);
          setConnectionStatus("error");
          setError("Erro na conexão com servidor. Tentando reconectar...");
          
          // Tentar reconectar após 3 segundos
          setTimeout(() => {
            const currentStatus = jobStatusRef.current?.status;
            if (currentStatus !== "completed") {
              connectSSE();
            }
          }, 3000);
        };

        eventSource.onopen = () => {
          console.log("✅ SSE Connection opened");
          setConnectionStatus("connected");
          setError(null);
        };
      } catch (err) {
        console.error("❌ Error creating EventSource:", err);
        setError("Erro ao conectar ao servidor");
        setConnectionStatus("error");
      }
    };

    connectSSE();

    return () => {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
      }
    };
  }, [jobId]);

  // Selecionar primeiro resultado automaticamente
  useEffect(() => {
    if (results.length > 0 && !selectedResult) {
      // Usar setTimeout para evitar setState síncrono no effect
      const timer = setTimeout(() => {
        setSelectedResult(results[0]);
      }, 0);
      return () => clearTimeout(timer);
    }
  }, [results, selectedResult]);

  const getStatusIcon = (status) => {
    switch (status) {
      case "success":
        return <CheckCircle2 className="h-4 w-4 text-green-500" />;
      case "processing":
        return <Loader2 className="h-4 w-4 text-blue-500 animate-spin" />;
      case "error":
        return <XCircle className="h-4 w-4 text-red-500" />;
      case "pending":
        return <Clock className="h-4 w-4 text-gray-400" />;
      default:
        return null;
    }
  };

  const getStatusBadge = (status) => {
    const variants = {
      success: "default",
      processing: "secondary",
      error: "destructive",
      pending: "outline",
    };

    const labels = {
      success: "Sucesso",
      processing: "Processando",
      error: "Erro",
      pending: "Pendente",
    };

    return (
      <Badge variant={variants[status]} className="text-xs">
        {labels[status]}
      </Badge>
    );
  };

  const getCacheBadge = (usedCache, cacheType) => {
    if (!usedCache) return null;

    const cacheLabels = {
      exact: "Cache Completo",
      partial_complete: "Cache Parcial",
      partial_hybrid: "Cache Híbrido",
    };

    return (
      <Badge variant="outline" className="text-xs bg-blue-50 text-blue-700 border-blue-200">
        ⚡ {cacheLabels[cacheType] || "Cache"}
      </Badge>
    );
  };

  const calculateSuccessRate = (extractedData, validationData) => {
    if (!extractedData || !extractedData.schema) return 0;
    
    const total = Object.keys(extractedData.schema).length;
    if (total === 0) return 0;
    
    let successCount = 0;

    // Função para normalizar valores (mesma lógica usada na UI)
    const normalizeValue = (val) => {
      if (val === null || val === undefined) return null;
      
      let normalized = String(val)
        .toLowerCase()
        .trim()
        // Remover acentos
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        // Remover pontuação e caracteres especiais (mantém letras, números e espaços)
        .replace(/[^\w\s]/g, '')
        // Substituir múltiplos espaços por um único espaço
        .replace(/\s+/g, ' ')
        .trim();
      
      return normalized;
    };

    // Se não houver dados de validação, considerar qualquer valor não-vazio como sucesso
    if (!validationData) {
      Object.values(extractedData.schema).forEach((value) => {
        if (value !== null && value !== undefined && value !== "") {
          successCount++;
        }
      });
      return Math.round((successCount / total) * 100);
    }

    // Validar comparando valores extraídos com valores esperados
    Object.entries(extractedData.schema).forEach(([key, extractedValue]) => {
      const expectedValue = validationData[key];
      
      const normalizedExpected = normalizeValue(expectedValue);
      const normalizedExtracted = normalizeValue(extractedValue);
      
      // Se ambos são null/undefined = sucesso
      if (normalizedExpected === null && normalizedExtracted === null) {
        successCount++;
      }
      // Se valores são iguais (após normalização) = sucesso
      else if (normalizedExpected !== null && normalizedExpected === normalizedExtracted) {
        successCount++;
      }
      // Caso contrário = erro (não incrementa)
    });

    return Math.round((successCount / total) * 100);
  };

  // Calcular estatísticas gerais
  const calculateStats = () => {
    const completedResults = results.filter(r => r.status === 'success');
    const totalTime = results.reduce((acc, r) => acc + (r.processingTimeMs || 0), 0);
    const cacheHits = results.filter(r => r.usedCache).length;
    
    let totalSuccessRate = 0;
    completedResults.forEach(result => {
      if (result.data) {
        totalSuccessRate += calculateSuccessRate(result.data, result.validationData);
      }
    });
    
    const avgSuccessRate = completedResults.length > 0 
      ? Math.round(totalSuccessRate / completedResults.length)
      : 0;
    
    const avgTime = results.length > 0
      ? Math.round(totalTime / results.length)
      : 0;

    return {
      avgSuccessRate,
      avgTime,
      cacheHits,
      cacheHitRate: results.length > 0 ? Math.round((cacheHits / results.length) * 100) : 0
    };
  };

  const stats = calculateStats();

  const exportResults = () => {
    const dataStr = JSON.stringify(results, null, 2);
    const dataBlob = new Blob([dataStr], { type: "application/json" });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `extraction-results-${jobId}.json`;
    link.click();
  };

  if (!jobId) {
    return (
      <div className="flex items-center justify-center h-[calc(100vh-4rem)]">
        <Card className="w-96">
          <CardContent className="pt-6">
            <div className="text-center">
              <XCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
              <h2 className="text-lg font-semibold mb-2">Job ID não fornecido</h2>
              <p className="text-sm text-muted-foreground mb-4">
                Não foi possível identificar o job de extração.
              </p>
              <Button onClick={() => router.push("/upload")}>
                Voltar para Upload
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="h-[calc(100vh-4rem)] space-y-4 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => router.push("/upload")}
            className="h-8 w-8"
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold tracking-tight">Resultados da Extração</h1>
            <p className="text-sm text-muted-foreground">
              Job ID: <span className="font-mono">{jobId}</span>
            </p>
          </div>
        </div>
        <div className="flex gap-2">
          {connectionStatus === "error" && (
            <Badge variant="destructive" className="text-xs">
              ⚠️ Erro de Conexão
            </Badge>
          )}
          {connectionStatus === "connecting" && (
            <Badge variant="secondary" className="text-xs">
              <Loader2 className="mr-1 h-3 w-3 animate-spin" />
              Conectando...
            </Badge>
          )}
          {connectionStatus === "connected" && jobStatus.status === "processing" && (
            <Badge variant="secondary" className="text-xs">
              <Loader2 className="mr-1 h-3 w-3 animate-spin" />
              Processando {jobStatus.processedItems}/{jobStatus.totalItems}
            </Badge>
          )}
          {connectionStatus === "completed" && (
            <Badge variant="default" className="text-xs">
              ✅ Concluído
            </Badge>
          )}
          <Button
            variant="outline"
            size="sm"
            onClick={exportResults}
            disabled={results.length === 0}
          >
            <Download className="mr-2 h-4 w-4" />
            Exportar
          </Button>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-red-600 shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-red-900">Erro</p>
                <p className="text-sm text-red-700">{error}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Statistics Cards */}
      {jobStatus.totalItems > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {/* Card 1: Progresso */}
          <Card>
            <CardContent className="pt-6">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-medium text-muted-foreground">Progresso</p>
                  <FileText className="h-4 w-4 text-muted-foreground" />
                </div>
                <div className="space-y-1">
                  <p className="text-2xl font-bold">
                    {jobStatus.processedItems}/{jobStatus.totalItems}
                  </p>
                  <div className="h-2 bg-secondary rounded-full overflow-hidden">
                    <div
                      className="h-full bg-primary transition-all duration-500"
                      style={{
                        width: `${(jobStatus.processedItems / jobStatus.totalItems) * 100}%`,
                      }}
                    />
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {Math.round((jobStatus.processedItems / jobStatus.totalItems) * 100)}% concluído
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Card 2: Taxa de Sucesso */}
          <Card>
            <CardContent className="pt-6">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-medium text-muted-foreground">Taxa de Sucesso</p>
                  <CheckCircle2 className="h-4 w-4 text-green-500" />
                </div>
                <div className="space-y-1">
                  <p className="text-2xl font-bold text-green-600">
                    {stats.avgSuccessRate}%
                  </p>
                  <p className="text-xs text-muted-foreground">
                    ✅ {jobStatus.successCount} sucesso | ❌ {jobStatus.errorCount} erro
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Card 3: Performance */}
          <Card>
            <CardContent className="pt-6">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-medium text-muted-foreground">Tempo Médio</p>
                  <Clock className="h-4 w-4 text-blue-500" />
                </div>
                <div className="space-y-1">
                  <p className="text-2xl font-bold text-blue-600">
                    {(stats.avgTime / 1000).toFixed(1)}s
                  </p>
                  <p className="text-xs text-muted-foreground">
                    por documento
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Card 4: Cache Performance */}
          <Card>
            <CardContent className="pt-6">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-medium text-muted-foreground">Cache Hits</p>
                  <span className="text-lg">⚡</span>
                </div>
                <div className="space-y-1">
                  <p className="text-2xl font-bold text-purple-600">
                    {stats.cacheHitRate}%
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {stats.cacheHits}/{results.length} documentos
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Main Layout: Sidebar + Content */}
      <div className="grid grid-cols-12 gap-4 h-[calc(100vh-20rem)]">
        {/* Sidebar - Lista de Resultados */}
        <Card className="col-span-4 flex flex-col">
          <CardHeader className="pb-3">
            <CardTitle className="text-base flex items-center gap-2">
              <FileText className="h-4 w-4" />
              Documentos ({results.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="flex-1 p-0">
            <ScrollArea className="h-full px-4 pb-4">
              <div className="space-y-2">
                {results.map((result) => (
                  <div
                    key={result.fileId}
                    onClick={() => setSelectedResult(result)}
                    className={`p-3 rounded-lg border cursor-pointer transition-all hover:shadow-md ${
                      selectedResult?.fileId === result.fileId
                        ? "border-primary bg-primary/5 shadow-sm"
                        : "border-border hover:border-primary/50"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          {getStatusIcon(result.status)}
                          <span className="font-medium text-sm truncate">
                            {result.fileId}
                          </span>
                        </div>
                        <div className="flex items-center gap-2 flex-wrap">
                          {getStatusBadge(result.status)}
                          {getCacheBadge(result.usedCache, result.cacheType)}
                        </div>
                        {result.processingTimeMs > 0 && (
                          <p className="text-xs text-muted-foreground mt-1">
                            ⏱️ {(result.processingTimeMs / 1000).toFixed(2)}s
                          </p>
                        )}
                      </div>
                      <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0" />
                    </div>
                    {result.status === "success" && result.data && (
                      <div className="mt-2 pt-2 border-t">
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-muted-foreground">Taxa de Sucesso</span>
                          <span className="font-semibold text-green-600">
                            {calculateSuccessRate(result.data, result.validationData)}%
                          </span>
                        </div>
                      </div>
                    )}
                  </div>
                ))}
                {results.length === 0 && jobStatus.status !== "completed" && (
                  <div className="text-center py-8">
                    <Loader2 className="h-8 w-8 animate-spin mx-auto mb-4 text-muted-foreground" />
                    <p className="text-sm text-muted-foreground">
                      Aguardando resultados...
                    </p>
                  </div>
                )}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>

        {/* Main Content - Resultados Detalhados */}
        <div className="col-span-8 space-y-4 overflow-auto">
          {selectedResult ? (
            <>
              {/* Document Info Header */}
              <Card>
                <CardContent className="pt-6">
                  <div className="flex items-start justify-between">
                    <div className="space-y-1">
                      <h2 className="text-xl font-bold">{selectedResult.fileId}</h2>
                      <div className="flex items-center gap-3 text-sm text-muted-foreground flex-wrap">
                        <span className="flex items-center gap-1">
                          <Clock className="h-3 w-3" />
                          {(selectedResult.processingTimeMs / 1000).toFixed(2)}s
                        </span>
                        {selectedResult.usedCache && (
                          <span className="flex items-center gap-1 text-blue-600">
                            ⚡ Cache utilizado ({selectedResult.cacheType})
                          </span>
                        )}
                      </div>
                    </div>
                    {getStatusBadge(selectedResult.status)}
                  </div>
                </CardContent>
              </Card>

              {/* Extraction Results */}
              {selectedResult.status === "success" && selectedResult.data && (
                <Card>
                  <CardHeader>
                    <div className="flex items-center justify-between">
                      <CardTitle className="text-base">Dados Extraídos</CardTitle>
                      <div className="flex items-center gap-2 text-sm">
                        <span className="text-muted-foreground">Taxa de Sucesso:</span>
                        <span className="font-bold text-green-600">
                          {calculateSuccessRate(selectedResult.data, selectedResult.validationData)}%
                        </span>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-3">
                      {Object.entries(selectedResult.data.schema || {}).map(([key, extractedValue]) => {
                        // Validar contra os dados esperados
                        const expectedValue = selectedResult.validationData?.[key];
                        
                        // Função para normalizar valores para comparação (ignora pontuação, espaços extras, acentos, case)
                        const normalizeValue = (val) => {
                          if (val === null || val === undefined) return null;
                          
                          let normalized = String(val)
                            .toLowerCase()
                            .trim()
                            // Remover acentos
                            .normalize('NFD')
                            .replace(/[\u0300-\u036f]/g, '')
                            // Remover pontuação e caracteres especiais (mantém letras, números e espaços)
                            .replace(/[^\w\s]/g, '')
                            // Substituir múltiplos espaços por um único espaço
                            .replace(/\s+/g, ' ')
                            .trim();
                          
                          return normalized;
                        };
                        
                        const normalizedExpected = normalizeValue(expectedValue);
                        const normalizedExtracted = normalizeValue(extractedValue);
                        
                        // Determinar se está correto
                        let isValid = false;
                        let statusMessage = "";
                        let badgeLabel = "";
                        let isExpectedEmpty = normalizedExpected === null;
                        
                        if (!selectedResult.validationData) {
                          // Sem dados de validação - considera válido se tiver valor
                          isValid = normalizedExtracted !== null;
                          badgeLabel = isValid ? "Extraído" : "Não Encontrado";
                        } else {
                          // Com dados de validação - compara valores
                          if (normalizedExpected === null && normalizedExtracted === null) {
                            // Ambos null = correto
                            isValid = true;
                            statusMessage = "Valor correto (vazio esperado)";
                            badgeLabel = "Correto";
                          } else if (normalizedExpected === normalizedExtracted) {
                            // Valores iguais = correto
                            isValid = true;
                            statusMessage = null; // Mostrar o valor
                            badgeLabel = "Correto";
                          } else if (normalizedExpected !== null && normalizedExtracted === null) {
                            // Esperava valor mas veio null = erro
                            isValid = false;
                            statusMessage = `Esperado: "${expectedValue}" mas não foi encontrado`;
                            badgeLabel = "Incorreto";
                          } else if (normalizedExpected === null && normalizedExtracted !== null) {
                            // Esperava null mas veio valor = erro
                            isValid = false;
                            statusMessage = `Não deveria ter valor, esperado vazio`;
                            badgeLabel = "Incorreto";
                          } else {
                            // Valores diferentes = erro
                            isValid = false;
                            statusMessage = `Valor incorreto`;
                            badgeLabel = "Incorreto";
                          }
                        }
                        
                        return (
                          <div
                            key={key}
                            className={`p-3 rounded-lg border ${
                              isValid
                                ? "border-green-200 bg-green-50 dark:border-green-900 dark:bg-green-950/20"
                                : "border-red-200 bg-red-50 dark:border-red-900 dark:bg-red-950/20"
                            }`}
                          >
                            <div className="flex items-start justify-between gap-3">
                              <div className="flex-1 min-w-0">
                                <div className="flex items-center gap-2 mb-1">
                                  {isValid ? (
                                    <CheckCircle2 className="h-4 w-4 text-green-600 shrink-0" />
                                  ) : (
                                    <XCircle className="h-4 w-4 text-red-600 shrink-0" />
                                  )}
                                  <span className="text-xs font-mono text-muted-foreground uppercase">
                                    {key.replace(/_/g, " ")}
                                  </span>
                                </div>
                                <div className="ml-6">
                                  {statusMessage ? (
                                    <p className="text-sm text-muted-foreground italic">
                                      {statusMessage}
                                    </p>
                                  ) : (
                                    <>
                                      <p className="text-sm font-medium">
                                        {extractedValue || <span className="text-muted-foreground italic">(vazio)</span>}
                                      </p>
                                    </>
                                  )}
                                  {/* Mostrar valor esperado se houver validationData */}
                                  {selectedResult.validationData && expectedValue !== undefined && (
                                    <p className="text-xs text-muted-foreground mt-1">
                                      Esperado: {expectedValue ? `"${expectedValue}"` : "(vazio)"}
                                    </p>
                                  )}
                                </div>
                              </div>
                              <div className="flex flex-col items-end gap-1">
                                <Badge
                                  variant={isValid ? "default" : "destructive"}
                                  className="text-xs"
                                >
                                  {badgeLabel}
                                </Badge>
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>

                    {/* JSON Export */}
                    <Separator className="my-4" />
                    <div className="space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-medium">JSON de Resultados</span>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => {
                            const dataStr = JSON.stringify(selectedResult.data, null, 2);
                            const dataBlob = new Blob([dataStr], { type: "application/json" });
                            const url = URL.createObjectURL(dataBlob);
                            const link = document.createElement("a");
                            link.href = url;
                            link.download = `${selectedResult.fileId}.json`;
                            link.click();
                          }}
                        >
                          <Download className="mr-2 h-3 w-3" />
                          Exportar JSON
                        </Button>
                      </div>
                      <div className="p-3 bg-secondary rounded-lg max-h-64 overflow-auto">
                        <pre className="text-xs font-mono">
                          {JSON.stringify(selectedResult.data, null, 2)}
                        </pre>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              )}

              {/* Error Message */}
              {selectedResult.status === "error" && (
                <Card>
                  <CardContent className="pt-6">
                    <div className="text-center py-8">
                      <XCircle className="h-8 w-8 mx-auto mb-4 text-red-500" />
                      <p className="text-sm font-medium mb-1">Falha no Processamento</p>
                      <p className="text-xs text-muted-foreground">
                        {selectedResult.error || "Erro desconhecido"}
                      </p>
                    </div>
                  </CardContent>
                </Card>
              )}

              {/* Processing */}
              {selectedResult.status === "processing" && (
                <Card>
                  <CardContent className="pt-6">
                    <div className="text-center py-8">
                      <Loader2 className="h-8 w-8 animate-spin mx-auto mb-4 text-muted-foreground" />
                      <p className="text-sm text-muted-foreground">
                        Processamento em andamento...
                      </p>
                    </div>
                  </CardContent>
                </Card>
              )}
            </>
          ) : (
            <Card>
              <CardContent className="pt-6">
                <div className="text-center py-12">
                  <FileText className="h-12 w-12 mx-auto mb-4 text-muted-foreground" />
                  <p className="text-sm text-muted-foreground">
                    Selecione um documento para ver os detalhes
                  </p>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
