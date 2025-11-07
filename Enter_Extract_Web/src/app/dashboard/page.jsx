"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  FileText,
  TrendingUp,
  CheckCircle2,
  Clock,
  Download,
  Upload,
  Calendar,
  Database,
  Loader2,
  AlertCircle,
  Eye,
} from "lucide-react";
import { useRouter } from "next/navigation";
import { useExtractionHistory } from "@/hooks/use-extraction-history";
import { useMemo } from "react";

export default function DashboardPage() {
  const router = useRouter();
  const { documents, isLoading, error } = useExtractionHistory("default-user");

  // Calcular estatísticas gerais
  const stats = useMemo(() => {
    if (documents.length === 0) {
      return {
        successRate: 0,
        totalProcessed: 0,
        totalSuccessful: 0,
        avgProcessingTime: 0,
        avgSuccessRatePercent: 0,
      };
    }

    const completed = documents.filter((d) => d.status === "completed");
    const totalProcessed = documents.length;
    const totalSuccessful = completed.length;
    const successRate = totalProcessed > 0 ? (totalSuccessful / totalProcessed) * 100 : 0;

    // Taxa média de sucesso dos campos extraídos
    const avgSuccessRatePercent = completed.length > 0
      ? Math.round(completed.reduce((sum, doc) => sum + doc.successRate, 0) / completed.length)
      : 0;

    // Tempo médio de processamento
    const avgProcessingTime = completed.length > 0
      ? (completed.reduce((sum, doc) => sum + parseFloat(doc.processingTime), 0) / completed.length).toFixed(1)
      : 0;

    return {
      successRate: successRate.toFixed(1),
      totalProcessed,
      totalSuccessful,
      avgProcessingTime,
      avgSuccessRatePercent,
    };
  }, [documents]);

  // Agrupar documentos por tipo (label)
  const documentsByType = useMemo(() => {
    const grouped = documents.reduce((acc, doc) => {
      const type = doc.type || "Não categorizado";
      if (!acc[type]) {
        acc[type] = {
          type,
          count: 0,
          successCount: 0,
          totalSuccessRate: 0,
        };
      }
      acc[type].count++;
      if (doc.status === "completed") {
        acc[type].successCount++;
        acc[type].totalSuccessRate += doc.successRate;
      }
      return acc;
    }, {});

    // Converter para array e calcular taxa média
    return Object.values(grouped)
      .map((item) => ({
        ...item,
        avgSuccessRate: item.successCount > 0
          ? Math.round(item.totalSuccessRate / item.successCount)
          : 0,
      }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 5); // Top 5 tipos
  }, [documents]);

  // Pegar últimos 6 documentos
  const recentDocuments = useMemo(() => {
    return documents.slice(0, 6);
  }, [documents]);

  // Exportar relatório CSV
  const exportReport = () => {
    const csvData = documents.map((doc) => ({
      Nome: doc.name,
      Tipo: doc.type,
      Status: doc.status,
      Data: doc.date,
      Hora: doc.time,
      "Campos Extraídos": doc.extracted,
      "Taxa de Sucesso": `${doc.successRate}%`,
      "Tempo de Processamento": doc.processingTime,
    }));

    const headers = Object.keys(csvData[0]);
    const csv = [
      headers.join(","),
      ...csvData.map((row) =>
        headers.map((header) => `"${row[header]}"`).join(",")
      ),
    ].join("\n");

    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = `relatorio_dashboard_${new Date().toISOString().split("T")[0]}.csv`;
    link.click();
  };

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Dashboard</h1>
          <p className="text-muted-foreground mt-1">
            Visão geral das extrações de dados de PDFs
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={exportReport} disabled={documents.length === 0 || isLoading}>
            <Download className="mr-2 h-4 w-4" />
            Exportar
          </Button>
          <Button onClick={() => router.push("/upload")}>
            <Upload className="mr-2 h-4 w-4" />
            Novo Upload
          </Button>
        </div>
      </div>

      {/* Error Alert */}
      {error && (
        <Card className="border-red-200 bg-red-50 dark:bg-red-900/10">
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-red-600 dark:text-red-400">
              <AlertCircle className="h-5 w-5" />
              <div>
                <p className="font-semibold">Erro ao carregar dados</p>
                <p className="text-sm">{error}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Loading State */}
      {isLoading && documents.length === 0 ? (
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
              <span className="ml-2 text-muted-foreground">Carregando dados...</span>
            </div>
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Grid de 3 Cards no Topo */}
          <div className="grid gap-4 md:grid-cols-3">
            {/* Card 1 - Taxa de Sucesso */}
            <Card>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Taxa de Sucesso Geral
                </CardTitle>
                <CheckCircle2 className="h-4 w-4 text-green-600" />
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-bold text-green-600">{stats.successRate}%</div>
                <p className="text-xs text-muted-foreground mt-2">
                  Documentos processados com sucesso
                </p>
                <div className="mt-4 space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Bem-sucedidos</span>
                    <span className="font-medium">{stats.totalSuccessful}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Total processado</span>
                    <span className="font-medium">{stats.totalProcessed}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Taxa de extração</span>
                    <span className="font-medium text-green-600">{stats.avgSuccessRatePercent}%</span>
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Card 2 - Total de Documentos */}
            <Card>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Total de Documentos
                </CardTitle>
                <FileText className="h-4 w-4 text-primary" />
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-bold text-primary">{stats.totalProcessed}</div>
                <p className="text-xs text-muted-foreground mt-2">
                  PDFs processados no total
                </p>
                <div className="mt-4 space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Tipos diferentes</span>
                    <span className="font-medium">{documentsByType.length}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Concluídos</span>
                    <span className="font-medium text-green-600">{stats.totalSuccessful}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Com falha</span>
                    <span className="font-medium text-red-600">{stats.totalProcessed - stats.totalSuccessful}</span>
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Card 3 - Tempo Médio */}
            <Card>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Tempo Médio
                </CardTitle>
                <Clock className="h-4 w-4 text-blue-600" />
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-bold text-blue-600">{stats.avgProcessingTime}s</div>
                <p className="text-xs text-muted-foreground mt-2">
                  Por documento processado
                </p>
                <div className="mt-4 space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Cache rápido</span>
                    <span className="font-medium text-green-600">&lt;1s</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Extração normal</span>
                    <span className="font-medium">2-4s</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Com GPT</span>
                    <span className="font-medium">4-6s</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Grid de 2 Colunas - Documentos Recentes + Padrões */}
          <div className="grid gap-4 md:grid-cols-2">
            {/* Card - Documentos Recentes */}
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle>Documentos Recentes</CardTitle>
                    <p className="text-sm text-muted-foreground mt-1">
                      Últimos PDFs processados pelo sistema
                    </p>
                  </div>
                  <Button 
                    variant="outline" 
                    size="sm"
                    onClick={() => router.push("/documents")}
                  >
                    Ver Todos
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                {recentDocuments.length === 0 ? (
                  <div className="text-center py-8">
                    <FileText className="h-12 w-12 mx-auto mb-4 text-muted-foreground" />
                    <p className="text-sm text-muted-foreground">
                      Nenhum documento processado ainda
                    </p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {recentDocuments.map((doc) => (
                      <div
                        key={doc.id}
                        className="flex items-center justify-between p-4 rounded-lg border hover:bg-accent/50 transition-colors cursor-pointer"
                        onClick={() => router.push(`/documents/${doc.id}`)}
                      >
                        <div className="flex items-center gap-4 flex-1 min-w-0">
                          <div
                            className={`h-10 w-10 rounded-lg flex items-center justify-center shrink-0 ${
                              doc.status === "completed"
                                ? "bg-green-100 dark:bg-green-900/20"
                                : doc.status === "processing"
                                ? "bg-blue-100 dark:bg-blue-900/20"
                                : "bg-red-100 dark:bg-red-900/20"
                            }`}
                          >
                            <FileText
                              className={`h-5 w-5 ${
                                doc.status === "completed"
                                  ? "text-green-600"
                                  : doc.status === "processing"
                                  ? "text-blue-600"
                                  : "text-red-600"
                              }`}
                            />
                          </div>
                          <div className="flex-1 min-w-0">
                            <p className="font-medium truncate">{doc.name}</p>
                            <div className="flex items-center gap-2 text-sm text-muted-foreground mt-0.5">
                              <Badge variant="outline" className="text-xs">
                                {doc.type}
                              </Badge>
                              <span>•</span>
                              <span className="flex items-center gap-1">
                                <Calendar className="h-3 w-3" />
                                {doc.date} às {doc.time}
                              </span>
                            </div>
                          </div>
                        </div>

                        <div className="flex items-center gap-4 shrink-0">
                          {doc.status === "completed" && (
                            <div className="text-right">
                              <p className="text-sm font-medium">
                                {doc.extracted} campos
                              </p>
                              <p className="text-xs text-muted-foreground">
                                {doc.processingTime}
                              </p>
                            </div>
                          )}

                          <Badge
                            variant={
                              doc.status === "completed"
                                ? "default"
                                : doc.status === "processing"
                                ? "secondary"
                                : "destructive"
                            }
                            className="min-w-[90px] justify-center"
                          >
                            {doc.status === "completed"
                              ? "Concluído"
                              : doc.status === "processing"
                              ? "Processando"
                              : "Falhou"}
                          </Badge>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Card - Padrões Aprendidos (Tipos de Documentos) */}
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle>Tipos de Documentos</CardTitle>
                    <p className="text-sm text-muted-foreground mt-1">
                      Categorias mais processadas
                    </p>
                  </div>
                  <Button 
                    variant="ghost" 
                    size="sm"
                    onClick={() => router.push("/documents")}
                  >
                    Ver Todos
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                {documentsByType.length === 0 ? (
                  <div className="text-center py-8">
                    <Database className="h-12 w-12 mx-auto mb-4 text-muted-foreground" />
                    <p className="text-sm text-muted-foreground">
                      Nenhum padrão identificado ainda
                    </p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {documentsByType.map((pattern, index) => (
                      <div 
                        key={index}
                        className="p-3 border rounded-lg hover:bg-secondary/50 cursor-pointer transition-colors"
                        onClick={() => router.push("/documents")}
                      >
                        <div className="flex items-center justify-between mb-2">
                          <div className="flex items-center gap-2">
                            <Database className="h-4 w-4 text-primary" />
                            <span className="font-medium text-sm">{pattern.type}</span>
                          </div>
                          <Badge variant="outline" className="text-xs">
                            {pattern.count} doc{pattern.count !== 1 ? "s" : ""}
                          </Badge>
                        </div>
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-muted-foreground">Taxa de Sucesso</span>
                          <span className={`font-semibold ${
                            pattern.avgSuccessRate >= 90 ? "text-green-600" :
                            pattern.avgSuccessRate >= 70 ? "text-yellow-600" :
                            "text-red-600"
                          }`}>
                            {pattern.avgSuccessRate}%
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </>
      )}
    </div>
  );
}
