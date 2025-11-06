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
} from "lucide-react";
import { useRouter } from "next/navigation";

export default function DashboardPage() {
  const router = useRouter();
  const recentDocuments = [
    {
      name: "Carteira_OAB_001.pdf",
      type: "Carteira OAB",
      status: "completed",
      date: "2025-11-06",
      time: "14:32",
      fieldsExtracted: 7,
      processingTime: "3.2s",
    },
    {
      name: "Extrato_Bancario_092.pdf",
      type: "Extrato Bancário",
      status: "completed",
      date: "2025-11-06",
      time: "14:28",
      fieldsExtracted: 6,
      processingTime: "2.8s",
    },
    {
      name: "Nota_Fiscal_345.pdf",
      type: "Nota Fiscal",
      status: "processing",
      date: "2025-11-06",
      time: "14:25",
      fieldsExtracted: 0,
      processingTime: "-",
    },
    {
      name: "Contrato_Prestacao_Servicos.pdf",
      type: "Contrato",
      status: "completed",
      date: "2025-11-06",
      time: "14:20",
      fieldsExtracted: 12,
      processingTime: "4.5s",
    },
    {
      name: "RG_Documento_789.pdf",
      type: "Documento RG",
      status: "failed",
      date: "2025-11-06",
      time: "14:15",
      fieldsExtracted: 0,
      processingTime: "-",
    },
    {
      name: "Carteira_OAB_002.pdf",
      type: "Carteira OAB",
      status: "completed",
      date: "2025-11-05",
      time: "18:45",
      fieldsExtracted: 7,
      processingTime: "3.1s",
    },
  ];

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
          <Button variant="outline">
            <Download className="mr-2 h-4 w-4" />
            Exportar
          </Button>
          <Button onClick={() => router.push("/upload")}>
            <Upload className="mr-2 h-4 w-4" />
            Novo Upload
          </Button>
        </div>
      </div>

      {/* Grid de 3 Cards no Topo */}
      <div className="grid gap-4 md:grid-cols-3">
        {/* Card 1 - Taxa de Sucesso */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Taxa de Sucesso
            </CardTitle>
            <CheckCircle2 className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold text-green-600">98.5%</div>
            <p className="text-xs text-muted-foreground mt-2">
              <span className="text-green-600 font-medium">+2.1%</span> desde a
              última semana
            </p>
            <div className="mt-4 space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Extrações bem-sucedidas</span>
                <span className="font-medium">2,805</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Total processado</span>
                <span className="font-medium">2,845</span>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Card 2 - Gráfico Central */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Extrações (30 dias)
            </CardTitle>
            <TrendingUp className="h-4 w-4 text-primary" />
          </CardHeader>
          <CardContent>
            <div className="h-[140px] flex items-end justify-between gap-1">
              {[65, 82, 75, 90, 88, 95, 100, 92, 85, 78, 88, 96, 94, 89].map(
                (height, index) => (
                  <div
                    key={index}
                    className="flex-1 bg-primary/20 hover:bg-primary/40 transition-colors rounded-t"
                    style={{ height: `${height}%` }}
                  />
                )
              )}
            </div>
            <p className="text-xs text-muted-foreground mt-4 text-center">
              Últimas 2 semanas
            </p>
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
            <div className="text-3xl font-bold text-blue-600">3.2s</div>
            <p className="text-xs text-muted-foreground mt-2">
              <span className="text-green-600 font-medium">-0.5s</span> mais
              rápido que antes
            </p>
            <div className="mt-4 space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Fase 1 (Regex)</span>
                <span className="font-medium">0.8s</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Fase 2 (Proximidade)</span>
                <span className="font-medium">1.2s</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Fase 3 (GPT)</span>
                <span className="font-medium">1.2s</span>
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
            <Button variant="outline" size="sm">
              Ver Todos
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            {recentDocuments.map((doc, index) => (
              <div
                key={index}
                className="flex items-center justify-between p-4 rounded-lg border hover:bg-accent/50 transition-colors"
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

                <div className="flex items-center gap-6 shrink-0">
                  {doc.status === "completed" && (
                    <div className="text-right">
                      <p className="text-sm font-medium">
                        {doc.fieldsExtracted} campos
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

                  <Button variant="ghost" size="sm">
                    Ver Detalhes
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Card - Padrões Aprendidos */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Padrões Aprendidos</CardTitle>
              <p className="text-sm text-muted-foreground mt-1">
                Tipos de documentos identificados
              </p>
            </div>
            <Button 
              variant="ghost" 
              size="sm"
              onClick={() => router.push("/schema-patterns")}
            >
              Ver Todos
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            <div 
              className="p-3 border rounded-lg hover:bg-secondary/50 cursor-pointer transition-colors"
              onClick={() => router.push("/schema-patterns")}
            >
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-2">
                  <Database className="h-4 w-4 text-primary" />
                  <span className="font-medium text-sm">Carteira OAB</span>
                </div>
                <Badge variant="outline" className="text-xs">156 docs</Badge>
              </div>
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Taxa de Sucesso</span>
                <span className="font-semibold text-green-600">98.5%</span>
              </div>
            </div>

            <div 
              className="p-3 border rounded-lg hover:bg-secondary/50 cursor-pointer transition-colors"
              onClick={() => router.push("/schema-patterns")}
            >
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-2">
                  <Database className="h-4 w-4 text-primary" />
                  <span className="font-medium text-sm">Nota Fiscal</span>
                </div>
                <Badge variant="outline" className="text-xs">234 docs</Badge>
              </div>
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Taxa de Sucesso</span>
                <span className="font-semibold text-green-600">89.5%</span>
              </div>
            </div>

            <div 
              className="p-3 border rounded-lg hover:bg-secondary/50 cursor-pointer transition-colors"
              onClick={() => router.push("/schema-patterns")}
            >
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-2">
                  <Database className="h-4 w-4 text-primary" />
                  <span className="font-medium text-sm">Extrato Bancário</span>
                </div>
                <Badge variant="outline" className="text-xs">89 docs</Badge>
              </div>
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Taxa de Sucesso</span>
                <span className="font-semibold text-yellow-600">92.3%</span>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
    </div>
  );
}
