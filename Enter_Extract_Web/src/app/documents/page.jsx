"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  FileText,
  Search,
  Upload,
  Download,
  Eye,
  Filter,
  Calendar,
  Clock,
  CheckCircle2,
  XCircle,
  Loader2,
  TrendingUp,
  FileCheck,
  RefreshCw,
  AlertCircle,
} from "lucide-react";
import { useRouter } from "next/navigation";
import { useExtractionHistory } from "@/hooks/use-extraction-history";
import { useStats } from "@/hooks/use-stats";
import { useState, useMemo } from "react";

export default function DocumentsPage() {
  const router = useRouter();
  const {
    documents,
    isLoading,
    error,
    pagination,
    fetchHistory,
    refresh,
  } = useExtractionHistory("default-user");

  const { getCacheStats } = useStats();
  const [searchTerm, setSearchTerm] = useState("");

  // Calcular estatísticas dos documentos usando useMemo
  const stats = useMemo(() => {
    if (documents.length === 0) {
      return {
        total: 0,
        completed: 0,
        processing: 0,
        failed: 0,
        avgSuccessRate: 0,
        avgProcessingTime: 0,
        totalFieldsExtracted: 0,
        cacheHitRate: 0,
      };
    }

    const completed = documents.filter((d) => d.status === "completed").length;
    const processing = documents.filter((d) => d.status === "processing").length;
    const failed = documents.filter((d) => d.status === "failed").length;

    // Calcular taxa média de sucesso (apenas documentos completos)
    const completedDocs = documents.filter((d) => d.status === "completed");
    const avgSuccessRate = completedDocs.length > 0
      ? Math.round(completedDocs.reduce((sum, doc) => sum + doc.successRate, 0) / completedDocs.length)
      : 0;

    // Calcular tempo médio de processamento (remover 's' e converter para número)
    const avgProcessingTime = completedDocs.length > 0
      ? (completedDocs.reduce((sum, doc) => sum + parseFloat(doc.processingTime), 0) / completedDocs.length).toFixed(1)
      : 0;

    // Total de campos extraídos
    const totalFieldsExtracted = completedDocs.reduce((sum, doc) => sum + doc.extracted, 0);

    // Taxa de cache hit (documentos que usaram cache)
    const cacheHits = completedDocs.filter((doc) => 
      doc.strategy && (doc.strategy.includes("Cache") || doc.strategy.includes("cache"))
    ).length;
    const cacheHitRate = completedDocs.length > 0
      ? Math.round((cacheHits / completedDocs.length) * 100)
      : 0;

    return {
      total: pagination.totalCount || documents.length,
      completed,
      processing,
      failed,
      avgSuccessRate,
      avgProcessingTime,
      totalFieldsExtracted,
      cacheHitRate,
    };
  }, [documents, pagination.totalCount]);

  // Filtrar documentos baseado no termo de busca usando useMemo
  const filteredDocuments = useMemo(() => {
    if (!searchTerm.trim()) {
      return documents;
    }

    const term = searchTerm.toLowerCase();
    return documents.filter(
      (doc) =>
        doc.name.toLowerCase().includes(term) ||
        doc.type.toLowerCase().includes(term) ||
        doc.date.includes(term)
    );
  }, [searchTerm, documents]);

  const handleNextPage = () => {
    const nextPage = pagination.page + 1;
    const maxPage = Math.ceil(pagination.totalCount / pagination.pageSize) - 1;
    if (nextPage <= maxPage) {
      fetchHistory(nextPage, pagination.pageSize);
    }
  };

  const handlePreviousPage = () => {
    if (pagination.page > 0) {
      fetchHistory(pagination.page - 1, pagination.pageSize);
    }
  };

  const exportDocumentsList = () => {
    // Preparar dados para exportação
    const csvData = documents.map((doc) => ({
      Nome: doc.name,
      Tipo: doc.type,
      Tamanho: doc.size,
      Data: doc.date,
      Hora: doc.time,
      Status: doc.status,
      "Campos Extraídos": doc.extracted,
      "Taxa de Sucesso": `${doc.successRate}%`,
      "Tempo de Processamento": doc.processingTime,
      Estratégia: doc.strategy,
    }));

    // Converter para CSV
    const headers = Object.keys(csvData[0]);
    const csv = [
      headers.join(","),
      ...csvData.map((row) =>
        headers.map((header) => `"${row[header]}"`).join(",")
      ),
    ].join("\n");

    // Download
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = `documentos_${new Date().toISOString().split("T")[0]}.csv`;
    link.click();
  };
  
  return (
    <div className="space-y-6 animate-fade-in">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Documentos</h1>
          <p className="text-muted-foreground mt-1">
            Gerencie e visualize todos os PDFs processados
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={refresh} disabled={isLoading}>
            <RefreshCw className={`mr-2 h-4 w-4 ${isLoading ? "animate-spin" : ""}`} />
            Atualizar
          </Button>
          <Button variant="outline" onClick={exportDocumentsList} disabled={documents.length === 0}>
            <Download className="mr-2 h-4 w-4" />
            Exportar Lista
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
                <p className="font-semibold">Erro ao carregar documentos</p>
                <p className="text-sm">{error}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Stats Grid */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {/* Card 1: Total de Documentos */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Total de Documentos
            </CardTitle>
            <FileText className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.total}</div>
            <div className="flex items-center gap-2 text-xs text-muted-foreground mt-1">
              <span className="flex items-center gap-1">
                <CheckCircle2 className="h-3 w-3 text-green-600" />
                {stats.completed} concluídos
              </span>
              {stats.failed > 0 && (
                <>
                  <span>•</span>
                  <span className="flex items-center gap-1 text-red-600">
                    <XCircle className="h-3 w-3" />
                    {stats.failed} falhas
                  </span>
                </>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Card 2: Taxa Média de Sucesso */}
        <Card className={stats.avgSuccessRate >= 90 ? "border-green-200 bg-green-50/50 dark:bg-green-900/10" : ""}>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Taxa Média de Sucesso
            </CardTitle>
            <TrendingUp className={`h-4 w-4 ${stats.avgSuccessRate >= 90 ? "text-green-600" : "text-muted-foreground"}`} />
          </CardHeader>
          <CardContent>
            <div className={`text-2xl font-bold ${
              stats.avgSuccessRate >= 90 ? "text-green-600" : 
              stats.avgSuccessRate >= 70 ? "text-yellow-600" : 
              "text-red-600"
            }`}>
              {stats.avgSuccessRate}%
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Precisão na extração de campos
            </p>
          </CardContent>
        </Card>

        {/* Card 3: Total de Campos Extraídos */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Campos Extraídos
            </CardTitle>
            <FileCheck className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">
              {stats.totalFieldsExtracted.toLocaleString("pt-BR")}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              {stats.completed > 0 
                ? `~${Math.round(stats.totalFieldsExtracted / stats.completed)} campos/documento`
                : "Nenhum documento processado"}
            </p>
          </CardContent>
        </Card>

        {/* Card 4: Performance de Cache */}
        <Card className={stats.cacheHitRate >= 50 ? "border-purple-200 bg-purple-50/50 dark:bg-purple-900/10" : ""}>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Cache Hit Rate
            </CardTitle>
            <span className="text-xl">⚡</span>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-purple-600">
              {stats.cacheHitRate}%
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              ⏱️ {stats.avgProcessingTime}s tempo médio
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Alert para documentos em processamento */}
      {stats.processing > 0 && (
        <Card className="border-blue-200 bg-blue-50/50 dark:bg-blue-900/10">
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <Loader2 className="h-5 w-5 text-blue-600 animate-spin shrink-0" />
              <div className="flex-1">
                <p className="font-semibold text-blue-900 dark:text-blue-100">
                  {stats.processing} {stats.processing === 1 ? "documento em processamento" : "documentos em processamento"}
                </p>
                <p className="text-sm text-blue-700 dark:text-blue-300 mt-1">
                  A página será atualizada automaticamente quando concluírem. Ou clique em &ldquo;Atualizar&rdquo; acima.
                </p>
              </div>
              <Button 
                variant="outline" 
                size="sm"
                onClick={refresh}
                className="shrink-0"
              >
                <RefreshCw className="mr-2 h-4 w-4" />
                Atualizar Agora
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Search and Filter */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex gap-4">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Buscar por nome, tipo ou data..."
                className="pl-10"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            <Button variant="outline">
              <Filter className="mr-2 h-4 w-4" />
              Filtrar
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Loading State */}
      {isLoading && documents.length === 0 && (
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
              <span className="ml-2 text-muted-foreground">Carregando documentos...</span>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Empty State */}
      {!isLoading && documents.length === 0 && !error && (
        <Card>
          <CardContent className="pt-6">
            <div className="flex flex-col items-center justify-center py-12">
              <FileText className="h-12 w-12 text-muted-foreground mb-4" />
              <h3 className="text-lg font-semibold mb-2">Nenhum documento encontrado</h3>
              <p className="text-muted-foreground mb-4">
                Comece fazendo o upload do seu primeiro PDF
              </p>
              <Button onClick={() => router.push("/upload")}>
                <Upload className="mr-2 h-4 w-4" />
                Fazer Upload
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Documents List */}
      {!isLoading && filteredDocuments.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>
              Todos os Documentos
              {searchTerm && (
                <span className="text-sm font-normal text-muted-foreground ml-2">
                  ({filteredDocuments.length} de {documents.length} documentos)
                </span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {filteredDocuments.map((doc) => (
              <div
                key={doc.id}
                className="flex items-center justify-between p-4 rounded-lg border hover:bg-accent/50 transition-colors"
              >
                <div className="flex items-center gap-4 flex-1 min-w-0">
                  {/* Icon com status */}
                  <div
                    className={`h-12 w-12 rounded-lg flex items-center justify-center shrink-0 ${
                      doc.status === "completed"
                        ? "bg-green-100 dark:bg-green-900/20"
                        : doc.status === "processing"
                        ? "bg-blue-100 dark:bg-blue-900/20"
                        : "bg-red-100 dark:bg-red-900/20"
                    }`}
                  >
                    <FileText
                      className={`h-6 w-6 ${
                        doc.status === "completed"
                          ? "text-green-600"
                          : doc.status === "processing"
                          ? "text-blue-600"
                          : "text-red-600"
                      }`}
                    />
                  </div>

                  {/* Info do documento */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <h3 className="font-semibold truncate">{doc.name}</h3>
                      <Badge variant="outline" className="text-xs shrink-0">
                        {doc.type}
                      </Badge>
                    </div>
                    <div className="flex items-center gap-3 text-sm text-muted-foreground mt-1">
                      <span className="flex items-center gap-1">
                        <Calendar className="h-3 w-3" />
                        {doc.date} às {doc.time}
                      </span>
                      <span>•</span>
                      <span>{doc.size}</span>
                      {doc.status === "completed" && (
                        <>
                          <span>•</span>
                          <span className="flex items-center gap-1">
                            <FileCheck className="h-3 w-3" />
                            {doc.extracted} campos extraídos
                          </span>
                        </>
                      )}
                    </div>
                    {doc.status === "completed" && (
                      <div className="flex items-center gap-2 text-xs text-muted-foreground mt-1">
                        <Clock className="h-3 w-3" />
                        <span>{doc.processingTime}</span>
                        <span>•</span>
                        <span className="flex items-center gap-1">
                          <TrendingUp className="h-3 w-3" />
                          {doc.successRate}% sucesso
                        </span>
                      </div>
                    )}
                    {doc.status === "completed" && (
                      <p className="text-xs text-muted-foreground mt-1">
                        {doc.strategy}
                      </p>
                    )}
                  </div>
                </div>

                {/* Actions */}
                <div className="flex items-center gap-3 shrink-0">
                  {doc.status === "processing" && (
                    <div className="flex items-center gap-2 text-blue-600">
                      <Loader2 className="h-4 w-4 animate-spin" />
                      <span className="text-sm font-medium">Processando...</span>
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

                  <div className="flex gap-1">
                    <Button 
                      variant="ghost" 
                      size="icon" 
                      title="Ver Detalhes"
                      onClick={() => router.push(`/documents/${doc.id}`)}
                    >
                      <Eye className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      title="Baixar resultados"
                      onClick={() => {
                        const dataStr = JSON.stringify(doc.result, null, 2);
                        const dataBlob = new Blob([dataStr], { type: "application/json" });
                        const url = URL.createObjectURL(dataBlob);
                        const link = document.createElement("a");
                        link.href = url;
                        link.download = `${doc.name}_${doc.id}.json`;
                        link.click();
                      }}
                    >
                      <Download className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
      )}

      {/* Pagination */}
      {!isLoading && documents.length > 0 && (
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">
                Mostrando {pagination.page * pagination.pageSize + 1} a{" "}
                {Math.min(
                  (pagination.page + 1) * pagination.pageSize,
                  pagination.totalCount
                )}{" "}
                de {pagination.totalCount.toLocaleString("pt-BR")} documentos
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={pagination.page === 0 || isLoading}
                  onClick={handlePreviousPage}
                >
                  Anterior
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={
                    (pagination.page + 1) * pagination.pageSize >=
                      pagination.totalCount || isLoading
                  }
                  onClick={handleNextPage}
                >
                  Próxima
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
