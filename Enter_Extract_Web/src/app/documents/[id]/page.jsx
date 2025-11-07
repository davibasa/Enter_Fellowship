"use client";

import { useEffect, useState } from "react";
import { useRouter, useParams } from "next/navigation";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import {
  ArrowLeft,
  FileText,
  Calendar,
  Clock,
  CheckCircle2,
  XCircle,
  Download,
  Copy,
  TrendingUp,
  Zap,
  DollarSign,
  Hash,
  AlertCircle,
  Loader2,
  Edit,
  FileCheck,
} from "lucide-react";
import { useExtractionHistory } from "@/hooks/use-extraction-history";

export default function DocumentDetailPage() {
  const router = useRouter();
  const params = useParams();
  const documentId = params.id;

  const { fetchDocument } = useExtractionHistory("default-user");
  const [document, setDocument] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    async function loadDocument() {
      if (!documentId) return;

      setIsLoading(true);
      setError(null);

      try {
        const doc = await fetchDocument(documentId);
        setDocument(doc);
      } catch (err) {
        console.error("Error loading document:", err);
        setError(err.message);
      } finally {
        setIsLoading(false);
      }
    }

    loadDocument();
  }, [documentId, fetchDocument]);

  const copyToClipboard = (text) => {
    navigator.clipboard.writeText(text);
    // TODO: Adicionar toast notification
  };

  const exportResultAsJSON = () => {
    if (!document) return;

    const dataStr = JSON.stringify(document, null, 2);
    const dataBlob = new Blob([dataStr], { type: "application/json" });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${document.pdfFilename || "documento"}_${documentId}.json`;
    link.click();
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-[calc(100vh-4rem)]">
        <Card className="w-96">
          <CardContent className="pt-6">
            <div className="flex flex-col items-center justify-center py-8">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground mb-4" />
              <p className="text-muted-foreground">Carregando detalhes...</p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !document) {
    return (
      <div className="flex items-center justify-center h-[calc(100vh-4rem)]">
        <Card className="w-96">
          <CardContent className="pt-6">
            <div className="text-center py-8">
              <XCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
              <h2 className="text-lg font-semibold mb-2">Erro ao carregar documento</h2>
              <p className="text-sm text-muted-foreground mb-4">
                {error || "Documento não encontrado"}
              </p>
              <Button onClick={() => router.push("/documents")}>
                Voltar para Documentos
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const successRate = Math.round((document.successRate || 0) * 100);
  const processingTime = ((document.processingTimeMs || 0) / 1000).toFixed(2);

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => router.push("/documents")}
            className="h-8 w-8"
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">
              Detalhes do Documento
            </h1>
            <p className="text-muted-foreground mt-1">
              {document.pdfFilename || "Documento sem nome"}
            </p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={exportResultAsJSON}>
            <Download className="mr-2 h-4 w-4" />
            Exportar JSON
          </Button>
          {document.editedManually && (
            <Badge variant="outline" className="text-xs">
              <Edit className="mr-1 h-3 w-3" />
              Editado Manualmente
            </Badge>
          )}
        </div>
      </div>

      {/* Status Banner */}
      <Card
        className={
          document.status === "completed"
            ? "border-green-200 bg-green-50/50 dark:bg-green-900/10"
            : document.status === "failed"
            ? "border-red-200 bg-red-50/50 dark:bg-red-900/10"
            : ""
        }
      >
        <CardContent className="pt-6">
          <div className="flex items-center gap-3">
            {document.status === "completed" ? (
              <CheckCircle2 className="h-8 w-8 text-green-600" />
            ) : (
              <XCircle className="h-8 w-8 text-red-600" />
            )}
            <div className="flex-1">
              <h3 className="text-lg font-semibold">
                Status: {document.status === "completed" ? "Concluído" : "Falhou"}
              </h3>
              <p className="text-sm text-muted-foreground">
                Processado em{" "}
                {new Date(document.extractedAt).toLocaleString("pt-BR")}
              </p>
            </div>
            <div className="text-right">
              <p className="text-2xl font-bold text-green-600">{successRate}%</p>
              <p className="text-xs text-muted-foreground">Taxa de sucesso</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Metadata Grid */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Tempo de Processamento
            </CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{processingTime}s</div>
            <p className="text-xs text-muted-foreground">
              {document.processingTimeMs}ms
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Campos Extraídos
            </CardTitle>
            <FileCheck className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">
              {document.fieldsExtracted}/{document.fieldsTotal}
            </div>
            <p className="text-xs text-muted-foreground">
              {Math.round((document.fieldsExtracted / document.fieldsTotal) * 100)}%
              completude
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Tamanho do PDF
            </CardTitle>
            <FileText className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {(document.pdfSizeBytes / 1024).toFixed(1)}
            </div>
            <p className="text-xs text-muted-foreground">KB</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Tokens Usados
            </CardTitle>
            <Zap className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-purple-600">
              {document.tokensUsed || 0}
            </div>
            <p className="text-xs text-muted-foreground">
              ${(document.costUsd || 0).toFixed(4)} USD
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Document Info */}
      <Card>
        <CardHeader>
          <CardTitle>Informações do Documento</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm">
                <FileText className="h-4 w-4 text-muted-foreground" />
                <span className="font-medium">Nome do arquivo:</span>
              </div>
              <p className="text-sm text-muted-foreground pl-6 break-all">
                {document.pdfFilename}
              </p>
            </div>

            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm">
                <Hash className="h-4 w-4 text-muted-foreground" />
                <span className="font-medium">Label:</span>
              </div>
              <div className="pl-6">
                <Badge variant="outline">{document.label}</Badge>
              </div>
            </div>

            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm">
                <Calendar className="h-4 w-4 text-muted-foreground" />
                <span className="font-medium">Data de extração:</span>
              </div>
              <p className="text-sm text-muted-foreground pl-6">
                {new Date(document.extractedAt).toLocaleString("pt-BR")}
              </p>
            </div>

            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm">
                <Hash className="h-4 w-4 text-muted-foreground" />
                <span className="font-medium">PDF Hash:</span>
              </div>
              <div className="flex items-center gap-2 pl-6">
                <code className="text-xs bg-secondary px-2 py-1 rounded font-mono">
                  {document.pdfHash?.substring(0, 16)}...
                </code>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6"
                  onClick={() => copyToClipboard(document.pdfHash)}
                >
                  <Copy className="h-3 w-3" />
                </Button>
              </div>
            </div>

            {document.templateId && (
              <div className="space-y-2">
                <div className="flex items-center gap-2 text-sm">
                  <FileText className="h-4 w-4 text-muted-foreground" />
                  <span className="font-medium">Template ID:</span>
                </div>
                <p className="text-sm text-muted-foreground pl-6">
                  {document.templateId}
                </p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Extraction Strategies */}
      {document.strategies && Object.keys(document.strategies).length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Estratégias de Extração</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 md:grid-cols-3">
              {Object.entries(document.strategies).map(([key, value]) => (
                <div
                  key={key}
                  className="p-3 rounded-lg border bg-secondary/50"
                >
                  <p className="text-xs font-medium text-muted-foreground uppercase mb-1">
                    {key.replace(/_/g, " ")}
                  </p>
                  <p className="text-sm font-semibold">{value}</p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Extracted Data */}
      <Card>
        <CardHeader>
          <CardTitle>Dados Extraídos</CardTitle>
        </CardHeader>
        <CardContent>
          {document.result && Object.keys(document.result).length > 0 ? (
            <div className="space-y-3">
              {Object.entries(document.result).map(([key, value]) => (
                <div
                  key={key}
                  className="p-4 rounded-lg border hover:bg-accent/50 transition-colors"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-2">
                        <span className="text-xs font-mono text-muted-foreground uppercase">
                          {key.replace(/_/g, " ")}
                        </span>
                      </div>
                      <p className="text-sm font-medium break-words">
                        {value !== null && value !== undefined && value !== ""
                          ? String(value)
                          : (
                            <span className="text-muted-foreground italic">
                              (vazio)
                            </span>
                          )}
                      </p>
                    </div>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 shrink-0"
                      onClick={() => copyToClipboard(String(value))}
                    >
                      <Copy className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="text-center py-8">
              <AlertCircle className="h-8 w-8 mx-auto mb-2 text-muted-foreground" />
              <p className="text-sm text-muted-foreground">
                Nenhum dado extraído disponível
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Raw JSON */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>JSON Completo</CardTitle>
            <Button
              variant="outline"
              size="sm"
              onClick={() => copyToClipboard(JSON.stringify(document, null, 2))}
            >
              <Copy className="mr-2 h-3 w-3" />
              Copiar JSON
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="p-4 bg-secondary rounded-lg max-h-96 overflow-auto">
            <pre className="text-xs font-mono">
              {JSON.stringify(document, null, 2)}
            </pre>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
