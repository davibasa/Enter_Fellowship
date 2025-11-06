"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import {
  ArrowLeft,
  FileText,
  Clock,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Loader2,
  ChevronRight,
  Download,
  RefreshCw,
} from "lucide-react";
import { useRouter } from "next/navigation";

// Dados mockados de extrações
const extractions = [
  {
    id: 1,
    name: "Carteira_OAB_001.pdf",
    label: "Carteira OAB",
    status: "completed",
    date: "2025-11-06 14:32",
    totalTime: "3.2s",
    stages: [
      { name: "OCR & Text Extraction", status: "completed", time: "1.2s", order: 1 },
      { name: "Fase 1 - Regex Patterns", status: "completed", time: "0.3s", order: 2 },
      { name: "Fase 2 - Proximity Search", status: "completed", time: "0.5s", order: 3 },
      { name: "Validation", status: "completed", time: "0.2s", order: 4 },
      { name: "Fase 3 - GPT Fallback", status: "skipped", time: "0s", order: 5 },
    ],
    results: {
      nome: { value: "João Silva Santos", validated: true, confidence: 98 },
      cpf: { value: "123.456.789-10", validated: true, confidence: 100 },
      oab: { value: "SP123456", validated: true, confidence: 100 },
      data_inscricao: { value: "15/03/2020", validated: true, confidence: 95 },
      email: { value: "joao.silva@oab.org.br", validated: true, confidence: 90 },
      telefone: { value: "(11) 98765-4321", validated: true, confidence: 85 },
      secao: { value: "São Paulo", validated: true, confidence: 100 },
    },
  },
  {
    id: 2,
    name: "Extrato_Bancario_092.pdf",
    label: "Extrato Bancário",
    status: "completed",
    date: "2025-11-06 14:28",
    totalTime: "4.5s",
    stages: [
      { name: "OCR & Text Extraction", status: "completed", time: "1.5s", order: 1 },
      { name: "Fase 1 - Regex Patterns", status: "completed", time: "0.4s", order: 2 },
      { name: "Fase 2 - Proximity Search", status: "completed", time: "0.8s", order: 3 },
      { name: "Validation", status: "warning", time: "0.3s", order: 4 },
      { name: "Fase 3 - GPT Fallback", status: "completed", time: "1.5s", order: 5 },
    ],
    results: {
      banco: { value: "Banco do Brasil", validated: true, confidence: 100 },
      agencia: { value: "1234-5", validated: true, confidence: 100 },
      conta: { value: "12345-6", validated: true, confidence: 100 },
      titular: { value: "Maria Santos", validated: true, confidence: 95 },
      periodo: { value: "01/10/2025 a 31/10/2025", validated: true, confidence: 90 },
      saldo_inicial: { value: "R$ 5.432,10", validated: false, confidence: 45 },
    },
  },
  {
    id: 3,
    name: "RG_Documento_789.pdf",
    label: "Documento RG",
    status: "processing",
    date: "2025-11-06 14:25",
    totalTime: "-",
    stages: [
      { name: "OCR & Text Extraction", status: "completed", time: "1.8s", order: 1 },
      { name: "Fase 1 - Regex Patterns", status: "completed", time: "0.6s", order: 2 },
      { name: "Fase 2 - Proximity Search", status: "processing", time: "-", order: 3 },
      { name: "Validation", status: "pending", time: "-", order: 4 },
      { name: "Fase 3 - GPT Fallback", status: "pending", time: "-", order: 5 },
    ],
    results: null,
  },
  {
    id: 4,
    name: "Nota_Fiscal_345.pdf",
    label: "Nota Fiscal",
    status: "failed",
    date: "2025-11-06 14:20",
    totalTime: "2.1s",
    stages: [
      { name: "OCR & Text Extraction", status: "completed", time: "1.2s", order: 1 },
      { name: "Fase 1 - Regex Patterns", status: "completed", time: "0.4s", order: 2 },
      { name: "Fase 2 - Proximity Search", status: "failed", time: "0.5s", order: 3 },
      { name: "Validation", status: "failed", time: "0s", order: 4 },
      { name: "Fase 3 - GPT Fallback", status: "failed", time: "0s", order: 5 },
    ],
    results: {
      numero_nf: { value: null, validated: false, confidence: 0 },
      emitente: { value: "Empresa XYZ", validated: false, confidence: 30 },
      valor_total: { value: null, validated: false, confidence: 0 },
    },
  },
];

export default function ExtractionResultsPage() {
  const router = useRouter();
  const [selectedExtraction, setSelectedExtraction] = useState(extractions[0]);

  const getStatusIcon = (status) => {
    switch (status) {
      case "completed":
        return <CheckCircle2 className="h-4 w-4 text-green-500" />;
      case "processing":
        return <Loader2 className="h-4 w-4 text-blue-500 animate-spin" />;
      case "failed":
        return <XCircle className="h-4 w-4 text-red-500" />;
      case "warning":
        return <AlertCircle className="h-4 w-4 text-yellow-500" />;
      case "skipped":
        return <ChevronRight className="h-4 w-4 text-gray-400" />;
      case "pending":
        return <Clock className="h-4 w-4 text-gray-400" />;
      default:
        return null;
    }
  };

  const getStatusBadge = (status) => {
    const variants = {
      completed: "default",
      processing: "secondary",
      failed: "destructive",
      warning: "outline",
    };
    
    const labels = {
      completed: "Concluído",
      processing: "Processando",
      failed: "Falhou",
      warning: "Aviso",
    };

    return (
      <Badge variant={variants[status]} className="text-xs">
        {labels[status]}
      </Badge>
    );
  };

  const calculateSuccessRate = (results) => {
    if (!results) return 0;
    const total = Object.keys(results).length;
    const validated = Object.values(results).filter((r) => r.validated).length;
    return Math.round((validated / total) * 100);
  };

  return (
    <div className="h-[calc(100vh-4rem)] space-y-4 animate-fade-in">
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
            <h1 className="text-2xl font-bold tracking-tight">Resultados das Extrações</h1>
            <p className="text-sm text-muted-foreground">
              Acompanhe o progresso e resultados em tempo real
            </p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm">
            <RefreshCw className="mr-2 h-4 w-4" />
            Atualizar
          </Button>
          <Button variant="outline" size="sm">
            <Download className="mr-2 h-4 w-4" />
            Exportar
          </Button>
        </div>
      </div>

      {/* Main Layout: Sidebar + Content */}
      <div className="grid grid-cols-12 gap-4 h-[calc(100vh-12rem)]">
        {/* Sidebar - Lista de Extrações */}
        <Card className="col-span-4 flex flex-col">
          <CardHeader className="pb-3">
            <CardTitle className="text-base flex items-center gap-2">
              <FileText className="h-4 w-4" />
              Documentos ({extractions.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="flex-1 p-0">
            <ScrollArea className="h-full px-4 pb-4">
              <div className="space-y-2">
                {extractions.map((extraction) => (
                  <div
                    key={extraction.id}
                    onClick={() => setSelectedExtraction(extraction)}
                    className={`p-3 rounded-lg border cursor-pointer transition-all hover:shadow-md ${
                      selectedExtraction?.id === extraction.id
                        ? "border-primary bg-primary/5 shadow-sm"
                        : "border-border hover:border-primary/50"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          {getStatusIcon(extraction.status)}
                          <span className="font-medium text-sm truncate">
                            {extraction.name}
                          </span>
                        </div>
                        <div className="flex items-center gap-2 text-xs text-muted-foreground">
                          <Badge variant="outline" className="text-xs px-1.5 py-0">
                            {extraction.label}
                          </Badge>
                          <span>{extraction.date}</span>
                        </div>
                      </div>
                      <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0" />
                    </div>
                    {extraction.status === "completed" && extraction.results && (
                      <div className="mt-2 pt-2 border-t">
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-muted-foreground">Taxa de Sucesso</span>
                          <span className="font-semibold text-green-600">
                            {calculateSuccessRate(extraction.results)}%
                          </span>
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>

        {/* Main Content - Roadmap e Resultados */}
        <div className="col-span-8 space-y-4 overflow-auto">
          {selectedExtraction && (
            <>
              {/* Document Info Header */}
              <Card>
                <CardContent className="pt-6">
                  <div className="flex items-start justify-between">
                    <div className="space-y-1">
                      <h2 className="text-xl font-bold">{selectedExtraction.name}</h2>
                      <div className="flex items-center gap-3 text-sm text-muted-foreground">
                        <span className="flex items-center gap-1">
                          <FileText className="h-3 w-3" />
                          {selectedExtraction.label}
                        </span>
                        <span className="flex items-center gap-1">
                          <Clock className="h-3 w-3" />
                          {selectedExtraction.totalTime}
                        </span>
                      </div>
                    </div>
                    {getStatusBadge(selectedExtraction.status)}
                  </div>
                </CardContent>
              </Card>

              {/* Roadmap - Process Stages */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-base">Pipeline de Processamento</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3">
                    {selectedExtraction.stages.map((stage, index) => (
                      <div key={index}>
                        <div className="flex items-center gap-3">
                          {/* Icon */}
                          <div className="shrink-0">
                            {getStatusIcon(stage.status)}
                          </div>

                          {/* Stage Info */}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between">
                              <span className="text-sm font-medium">{stage.name}</span>
                              <div className="flex items-center gap-2">
                                <Badge
                                  variant={
                                    stage.status === "completed"
                                      ? "default"
                                      : stage.status === "processing"
                                      ? "secondary"
                                      : stage.status === "failed"
                                      ? "destructive"
                                      : "outline"
                                  }
                                  className="text-xs"
                                >
                                  {stage.status === "completed"
                                    ? "Completo"
                                    : stage.status === "processing"
                                    ? "Processando"
                                    : stage.status === "failed"
                                    ? "Falhou"
                                    : stage.status === "skipped"
                                    ? "Ignorado"
                                    : "Pendente"}
                                </Badge>
                                <span className="text-xs text-muted-foreground font-mono">
                                  {stage.time}
                                </span>
                              </div>
                            </div>

                            {/* Progress Bar */}
                            {stage.status === "processing" && (
                              <div className="mt-2 h-1 bg-secondary rounded-full overflow-hidden">
                                <div className="h-full bg-primary animate-pulse w-2/3" />
                              </div>
                            )}
                          </div>
                        </div>

                        {/* Connector Line */}
                        {index < selectedExtraction.stages.length - 1 && (
                          <div className="ml-2 h-4 w-px bg-border my-1" />
                        )}
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>

              {/* Extraction Results */}
              {selectedExtraction.results && (
                <Card>
                  <CardHeader>
                    <div className="flex items-center justify-between">
                      <CardTitle className="text-base">Resultados da Extração</CardTitle>
                      <div className="flex items-center gap-2 text-sm">
                        <span className="text-muted-foreground">Taxa de Sucesso:</span>
                        <span className="font-bold text-green-600">
                          {calculateSuccessRate(selectedExtraction.results)}%
                        </span>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-3">
                      {Object.entries(selectedExtraction.results).map(([key, data]) => (
                        <div
                          key={key}
                          className={`p-3 rounded-lg border ${
                            data.validated
                              ? "border-green-200 bg-green-50 dark:border-green-900 dark:bg-green-950/20"
                              : "border-red-200 bg-red-50 dark:border-red-900 dark:bg-red-950/20"
                          }`}
                        >
                          <div className="flex items-start justify-between gap-3">
                            <div className="flex-1 min-w-0">
                              <div className="flex items-center gap-2 mb-1">
                                {data.validated ? (
                                  <CheckCircle2 className="h-4 w-4 text-green-600 shrink-0" />
                                ) : (
                                  <XCircle className="h-4 w-4 text-red-600 shrink-0" />
                                )}
                                <span className="text-xs font-mono text-muted-foreground uppercase">
                                  {key.replace(/_/g, " ")}
                                </span>
                              </div>
                              <div className="ml-6">
                                {data.value ? (
                                  <p className="text-sm font-medium">{data.value}</p>
                                ) : (
                                  <p className="text-sm text-muted-foreground italic">
                                    Não extraído
                                  </p>
                                )}
                              </div>
                            </div>
                            <div className="flex flex-col items-end gap-1">
                              <Badge
                                variant={data.validated ? "default" : "destructive"}
                                className="text-xs"
                              >
                                {data.validated ? "Válido" : "Inválido"}
                              </Badge>
                              <span
                                className={`text-xs font-mono ${
                                  data.confidence >= 80
                                    ? "text-green-600"
                                    : data.confidence >= 50
                                    ? "text-yellow-600"
                                    : "text-red-600"
                                }`}
                              >
                                {data.confidence}%
                              </span>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>

                    {/* JSON Export */}
                    <Separator className="my-4" />
                    <div className="space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-medium">JSON de Resultados</span>
                        <Button variant="outline" size="sm">
                          <Download className="mr-2 h-3 w-3" />
                          Exportar JSON
                        </Button>
                      </div>
                      <div className="p-3 bg-secondary rounded-lg">
                        <pre className="text-xs font-mono overflow-x-auto">
                          {JSON.stringify(selectedExtraction.results, null, 2)}
                        </pre>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              )}

              {/* No Results Message */}
              {!selectedExtraction.results &&
                selectedExtraction.status === "processing" && (
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

              {!selectedExtraction.results && selectedExtraction.status === "failed" && (
                <Card>
                  <CardContent className="pt-6">
                    <div className="text-center py-8">
                      <XCircle className="h-8 w-8 mx-auto mb-4 text-red-500" />
                      <p className="text-sm font-medium mb-1">Falha no Processamento</p>
                      <p className="text-xs text-muted-foreground">
                        Não foi possível extrair dados deste documento
                      </p>
                    </div>
                  </CardContent>
                </Card>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
