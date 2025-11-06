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
} from "lucide-react";
import { useRouter } from "next/navigation";

const documents = [
  {
    id: 1,
    name: "Carteira_OAB_001.pdf",
    type: "Carteira OAB",
    size: "1.2 MB",
    date: "2025-11-06",
    time: "14:32",
    status: "completed",
    extracted: 7,
    processingTime: "3.2s",
    successRate: 100,
    strategy: "Fase 1 (Regex) + Fase 2 (Proximidade)",
  },
  {
    id: 2,
    name: "Extrato_Bancario_092.pdf",
    type: "Extrato Bancário",
    size: "980 KB",
    date: "2025-11-06",
    time: "14:28",
    status: "completed",
    extracted: 6,
    processingTime: "2.8s",
    successRate: 100,
    strategy: "Fase 1 (Regex) + Fase 3 (GPT)",
  },
  {
    id: 3,
    name: "Nota_Fiscal_345.pdf",
    type: "Nota Fiscal",
    size: "2.4 MB",
    date: "2025-11-06",
    time: "14:25",
    status: "processing",
    extracted: 0,
    processingTime: "-",
    successRate: 0,
    strategy: "Processando...",
  },
  {
    id: 4,
    name: "Contrato_Prestacao_Servicos.pdf",
    type: "Contrato",
    size: "3.1 MB",
    date: "2025-11-06",
    time: "14:20",
    status: "completed",
    extracted: 12,
    processingTime: "4.5s",
    successRate: 92,
    strategy: "Fase 1 + Fase 2 + Fase 3 (GPT)",
  },
  {
    id: 5,
    name: "RG_Documento_789.pdf",
    type: "Documento RG",
    size: "1.5 MB",
    date: "2025-11-06",
    time: "14:15",
    status: "failed",
    extracted: 0,
    processingTime: "-",
    successRate: 0,
    strategy: "Erro no processamento",
  },
  {
    id: 6,
    name: "Carteira_OAB_002.pdf",
    type: "Carteira OAB",
    size: "1.1 MB",
    date: "2025-11-05",
    time: "18:45",
    status: "completed",
    extracted: 7,
    processingTime: "3.1s",
    successRate: 100,
    strategy: "Fase 1 (Regex) + Fase 2 (Proximidade)",
  },
  {
    id: 7,
    name: "Certidao_Nascimento_456.pdf",
    type: "Certidão",
    size: "890 KB",
    date: "2025-11-05",
    time: "16:20",
    status: "completed",
    extracted: 8,
    processingTime: "3.8s",
    successRate: 88,
    strategy: "Fase 2 (Proximidade) + Fase 3 (GPT)",
  },
  {
    id: 8,
    name: "Comprovante_Residencia_123.pdf",
    type: "Comprovante",
    size: "750 KB",
    date: "2025-11-05",
    time: "15:10",
    status: "completed",
    extracted: 5,
    processingTime: "2.5s",
    successRate: 100,
    strategy: "Fase 1 (Regex) + Fase 2 (Proximidade)",
  },
];

const stats = {
  total: 2845,
  completed: 2805,
  processing: 5,
  failed: 35,
};

export default function DocumentsPage() {
  const router = useRouter();
  
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
          <Button variant="outline">
            <Download className="mr-2 h-4 w-4" />
            Exportar Lista
          </Button>
          <Button onClick={() => router.push("/upload")}>
            <Upload className="mr-2 h-4 w-4" />
            Novo Upload
          </Button>
        </div>
      </div>

      {/* Stats Grid */}
      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Total de Documentos
            </CardTitle>
            <FileText className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.total}</div>
            <p className="text-xs text-muted-foreground">PDFs processados</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Concluídos
            </CardTitle>
            <CheckCircle2 className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">
              {stats.completed}
            </div>
            <p className="text-xs text-muted-foreground">
              {((stats.completed / stats.total) * 100).toFixed(1)}% de sucesso
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Em Processamento
            </CardTitle>
            <Loader2 className="h-4 w-4 text-blue-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">
              {stats.processing}
            </div>
            <p className="text-xs text-muted-foreground">Aguardando conclusão</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Falharam
            </CardTitle>
            <XCircle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{stats.failed}</div>
            <p className="text-xs text-muted-foreground">Necessitam revisão</p>
          </CardContent>
        </Card>
      </div>

      {/* Search and Filter */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex gap-4">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Buscar por nome, tipo ou data..."
                className="pl-10"
              />
            </div>
            <Button variant="outline">
              <Filter className="mr-2 h-4 w-4" />
              Filtrar
            </Button>
            <Button variant="outline">Ordenar</Button>
          </div>
        </CardContent>
      </Card>

      {/* Documents List */}
      <Card>
        <CardHeader>
          <CardTitle>Todos os Documentos</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            {documents.map((doc) => (
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
                      onClick={() => router.push("/extraction-results")}
                    >
                      <Eye className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      title="Baixar resultados"
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

      {/* Pagination */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              Mostrando 1 a 8 de {stats.total.toLocaleString("pt-BR")} documentos
            </p>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled>
                Anterior
              </Button>
              <Button variant="outline" size="sm">
                Próxima
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
