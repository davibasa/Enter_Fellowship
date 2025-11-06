"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Upload,
  FileText,
  Tag,
  Code,
  CheckCircle,
  ArrowLeft,
  Loader2,
} from "lucide-react";
import { useRouter } from "next/navigation";

export default function UploadPage() {
  const router = useRouter();
  const [isLoading, setIsLoading] = useState(false);
  const [uploadedFile, setUploadedFile] = useState(null);
  const [formData, setFormData] = useState({
    label: "",
    extractionSchema: "",
    validationSchema: "",
  });

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file && file.type === "application/pdf") {
      setUploadedFile(file);
    } else {
      alert("Por favor, selecione um arquivo PDF válido");
    }
  };

  const handleInputChange = (field, value) => {
    setFormData((prev) => ({
      ...prev,
      [field]: value,
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!uploadedFile) {
      alert("Por favor, faça upload de um arquivo PDF");
      return;
    }

    if (!formData.label.trim()) {
      alert("Por favor, informe um label");
      return;
    }

    setIsLoading(true);

    try {
      // Aqui você vai implementar a chamada para a API
      const formDataToSend = new FormData();
      formDataToSend.append("file", uploadedFile);
      formDataToSend.append("label", formData.label);
      formDataToSend.append("extractionSchema", formData.extractionSchema);
      formDataToSend.append("validationSchema", formData.validationSchema);

      // TODO: Implementar chamada para API
      // const response = await fetch('/api/extract', {
      //   method: 'POST',
      //   body: formDataToSend,
      // });

      // Simulação de upload
      await new Promise((resolve) => setTimeout(resolve, 2000));

      // Redireciona para a tela de resultados
      router.push("/extraction-results");
    } catch (error) {
      console.error("Erro ao fazer upload:", error);
      alert("Erro ao processar o arquivo");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen space-y-6 animate-fade-in">
      {/* Header */}
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
          <h1 className="text-3xl font-bold tracking-tight">Novo Upload</h1>
          <p className="text-muted-foreground mt-1">
            Envie um documento PDF para extração de dados
          </p>
        </div>
      </div>

      {/* Form Card */}
      <Card className="max-w-4xl mx-auto">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Upload className="h-5 w-5" />
            Configuração da Extração
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-6">
            {/* Label Field */}
            <div className="space-y-2">
              <Label htmlFor="label" className="flex items-center gap-2">
                <Tag className="h-4 w-4" />
                Label do Documento
              </Label>
              <Input
                id="label"
                placeholder="Ex: Carteira OAB, Extrato Bancário, RG..."
                value={formData.label}
                onChange={(e) => handleInputChange("label", e.target.value)}
                className="font-mono"
              />
              <p className="text-xs text-muted-foreground">
                Identificação do tipo de documento para processamento
              </p>
            </div>

            {/* Extraction Schema */}
            <div className="space-y-2">
              <Label htmlFor="extractionSchema" className="flex items-center gap-2">
                <Code className="h-4 w-4" />
                Schema de Extração (JSON)
              </Label>
              <Textarea
                id="extractionSchema"
                placeholder={`{
  "nome": "string",
  "cpf": "string",
  "data_nascimento": "date",
  "endereco": "string"
}`}
                value={formData.extractionSchema}
                onChange={(e) =>
                  handleInputChange("extractionSchema", e.target.value)
                }
                className="font-mono text-sm min-h-[150px]"
              />
              <p className="text-xs text-muted-foreground">
                Estrutura JSON com os campos a serem extraídos do documento
              </p>
            </div>

            {/* Validation Schema */}
            <div className="space-y-2">
              <Label htmlFor="validationSchema" className="flex items-center gap-2">
                <CheckCircle className="h-4 w-4" />
                Schema de Validação (JSON)
              </Label>
              <Textarea
                id="validationSchema"
                placeholder={`{
  "cpf": {
    "type": "regex",
    "pattern": "^\\d{3}\\.\\d{3}\\.\\d{3}-\\d{2}$"
  },
  "nome": {
    "type": "required",
    "minLength": 3
  }
}`}
                value={formData.validationSchema}
                onChange={(e) =>
                  handleInputChange("validationSchema", e.target.value)
                }
                className="font-mono text-sm min-h-[150px]"
              />
              <p className="text-xs text-muted-foreground">
                Regras de validação para os campos extraídos (opcional)
              </p>
            </div>

            {/* File Upload */}
            <div className="space-y-2">
              <Label htmlFor="file" className="flex items-center gap-2">
                <FileText className="h-4 w-4" />
                Arquivo PDF
              </Label>
              <div className="relative">
                <Input
                  id="file"
                  type="file"
                  accept="application/pdf"
                  onChange={handleFileChange}
                  className="cursor-pointer"
                />
                {uploadedFile && (
                  <div className="mt-2 flex items-center gap-2 text-sm text-muted-foreground">
                    <CheckCircle className="h-4 w-4 text-green-500" />
                    <span>{uploadedFile.name}</span>
                    <span className="text-xs">
                      ({(uploadedFile.size / 1024 / 1024).toFixed(2)} MB)
                    </span>
                  </div>
                )}
              </div>
              <p className="text-xs text-muted-foreground">
                Selecione o arquivo PDF para processamento
              </p>
            </div>

            {/* Action Buttons */}
            <div className="flex gap-3 pt-4">
              <Button
                type="button"
                variant="outline"
                onClick={() => router.back()}
                className="flex-1"
                disabled={isLoading}
              >
                Cancelar
              </Button>
              <Button
                type="submit"
                className="flex-1"
                disabled={isLoading || !uploadedFile}
              >
                {isLoading ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Processando...
                  </>
                ) : (
                  <>
                    <Upload className="mr-2 h-4 w-4" />
                    Iniciar Extração
                  </>
                )}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {/* Info Cards */}
      <div className="max-w-4xl mx-auto grid gap-4 md:grid-cols-3">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <div className="p-2 bg-blue-500/10 rounded-lg">
                <Tag className="h-4 w-4 text-blue-500" />
              </div>
              <div>
                <h3 className="font-semibold text-sm">Label</h3>
                <p className="text-xs text-muted-foreground mt-1">
                  Identifica o tipo de documento para processamento adequado
                </p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <div className="p-2 bg-green-500/10 rounded-lg">
                <Code className="h-4 w-4 text-green-500" />
              </div>
              <div>
                <h3 className="font-semibold text-sm">Schema de Extração</h3>
                <p className="text-xs text-muted-foreground mt-1">
                  Define quais campos serão extraídos do documento
                </p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <div className="p-2 bg-purple-500/10 rounded-lg">
                <CheckCircle className="h-4 w-4 text-purple-500" />
              </div>
              <div>
                <h3 className="font-semibold text-sm">Validação</h3>
                <p className="text-xs text-muted-foreground mt-1">
                  Garante a qualidade dos dados extraídos
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
