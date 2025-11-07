"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import {
  Upload,
  FileText,
  Tag,
  Code,
  CheckCircle,
  ArrowLeft,
  Loader2,
  Plus,
  Trash2,
  List,
} from "lucide-react";
import { useRouter } from "next/navigation";

export default function UploadPage() {
  const router = useRouter();
  const [isLoading, setIsLoading] = useState(false);
  const [uploadList, setUploadList] = useState([]);
  const [currentForm, setCurrentForm] = useState({
    file: null,
    label: "",
    extractionSchema: "",
    validationSchema: "",
  });

  // Converte arquivo para Base64
  const fileToBase64 = (file) => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.readAsDataURL(file);
      reader.onload = () => {
        const base64 = reader.result.split(",")[1];
        resolve(base64);
      };
      reader.onerror = (error) => reject(error);
    });
  };

  // Gera hash SHA-256 do schema (campos ordenados alfabeticamente)
  const generateSchemaHash = async (schema) => {
    const sortedFields = Object.keys(schema).sort().join(',');
    const encoder = new TextEncoder();
    const data = encoder.encode(sortedFields);
    const hashBuffer = await crypto.subtle.digest('SHA-256', data);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
    return hashHex.substring(0, 16); // 16 caracteres (mesmo padrão do backend)
  };

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file && file.type === "application/pdf") {
      setCurrentForm((prev) => ({ ...prev, file }));
    } else {
      alert("Por favor, selecione um arquivo PDF válido");
    }
  };

  const handleInputChange = (field, value) => {
    setCurrentForm((prev) => ({
      ...prev,
      [field]: value,
    }));
  };

  const handleAddToList = (keepFile = false) => {
    if (!currentForm.file) {
      alert("Por favor, selecione um arquivo PDF");
      return;
    }

    if (!currentForm.label.trim()) {
      alert("Por favor, informe um label");
      return;
    }

    if (!currentForm.extractionSchema.trim()) {
      alert("Por favor, informe o schema de extração");
      return;
    }

    // Validar JSONs
    try {
      JSON.parse(currentForm.extractionSchema);
    } catch {
      alert("Schema de extração inválido (JSON malformado)");
      return;
    }

    if (currentForm.validationSchema.trim()) {
      try {
        JSON.parse(currentForm.validationSchema);
      } catch {
        alert("Schema de validação inválido (JSON malformado)");
        return;
      }
    }

    // Adicionar à lista
    setUploadList((prev) => [
      ...prev,
      {
        id: Date.now(),
        file: currentForm.file,
        label: currentForm.label,
        extractionSchema: currentForm.extractionSchema,
        validationSchema: currentForm.validationSchema,
      },
    ]);

    // Limpar formulário (mantém arquivo se keepFile = true)
    setCurrentForm({
      file: keepFile ? currentForm.file : null,
      label: "",
      extractionSchema: "",
      validationSchema: "",
    });

    // Limpar input de arquivo se não mantiver
    if (!keepFile) {
      const fileInput = document.getElementById("file");
      if (fileInput) fileInput.value = "";
    }
  };

  const handleRemoveFromList = (id) => {
    setUploadList((prev) => prev.filter((item) => item.id !== id));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (uploadList.length === 0) {
      alert("Por favor, adicione pelo menos um item à lista");
      return;
    }

    setIsLoading(true);

    try {
      // Converter PDFs para Base64 e preparar batch request
      const pdfItems = await Promise.all(
        uploadList.map(async (item, index) => {
          const schema = JSON.parse(item.extractionSchema);
          const schemaHash = await generateSchemaHash(schema);
          
          return {
            // Usar nome único para cada item (adiciona index se houver duplicatas)
            fileId: `${item.file.name}_${index}`,
            pdfBase64: await fileToBase64(item.file),
            label: item.label,
            extractionSchema: schema,
            schemaHash: schemaHash, // ✨ Hash do schema
            validationData: item.validationSchema
              ? JSON.parse(item.validationSchema)
              : null,
          };
        })
      );

      // Criar batch request agrupado por label
      const batchRequests = {};
      pdfItems.forEach((item) => {
        if (!batchRequests[item.label]) {
          batchRequests[item.label] = {
            label: item.label,
            pdfItems: [],
          };
        }

        // Cada PDF mantém seu próprio schema individual
        batchRequests[item.label].pdfItems.push({
          fileId: item.fileId,
          pdfBase64: item.pdfBase64,
          extractionSchema: item.extractionSchema, // ✨ Schema individual por PDF
          schemaHash: item.schemaHash, // ✨ Hash individual por PDF
          validationData: item.validationData,
        });
      });

      // Enviar todos os batches
      const jobIds = [];
      for (const batch of Object.values(batchRequests)) {
        const response = await fetch("http://localhost:5056/api/extractor/batch", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify(batch),
        });

        if (!response.ok) {
          throw new Error("Erro ao criar batch job");
        }

        const result = await response.json();
        jobIds.push(result.jobId);
      }

      console.log("✅ Batch jobs created:", jobIds);

      // Redirecionar para tela de resultados com o primeiro jobId
      // (pode melhorar depois para suportar múltiplos jobs)
      router.push(`/extract/results?jobId=${jobIds[0]}`);
    } catch (error) {
      console.error("❌ Erro ao processar:", error);
      alert(`Erro ao processar arquivos: ${error.message}`);
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
          <h1 className="text-3xl font-bold tracking-tight">Nova Extração em Lote</h1>
          <p className="text-muted-foreground mt-1">
            Crie uma lista de documentos para processar com seus schemas
          </p>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Form Card - Adicionar à lista */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Plus className="h-5 w-5" />
              Adicionar Documento
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* File Upload */}
            <div className="space-y-2">
              <Label htmlFor="file" className="flex items-center gap-2">
                <FileText className="h-4 w-4" />
                Arquivo PDF
              </Label>
              <Input
                id="file"
                type="file"
                accept="application/pdf"
                onChange={handleFileChange}
                className="cursor-pointer"
              />
              {currentForm.file && (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <CheckCircle className="h-4 w-4 text-green-500" />
                  <span className="truncate">{currentForm.file.name}</span>
                  <span className="text-xs">
                    ({(currentForm.file.size / 1024 / 1024).toFixed(2)} MB)
                  </span>
                </div>
              )}
            </div>

            {/* Label Field */}
            <div className="space-y-2">
              <Label htmlFor="label" className="flex items-center gap-2">
                <Tag className="h-4 w-4" />
                Label do Documento
              </Label>
              <Input
                id="label"
                placeholder="Ex: Carteira OAB, Extrato Bancário..."
                value={currentForm.label}
                onChange={(e) => handleInputChange("label", e.target.value)}
                className="font-mono"
              />
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
  "data_nascimento": "date"
}`}
                value={currentForm.extractionSchema}
                onChange={(e) =>
                  handleInputChange("extractionSchema", e.target.value)
                }
                className="font-mono text-xs min-h-[120px]"
              />
            </div>

            {/* Validation Schema */}
            <div className="space-y-2">
              <Label htmlFor="validationSchema" className="flex items-center gap-2">
                <CheckCircle className="h-4 w-4" />
                Schema de Validação (JSON - Opcional)
              </Label>
              <Textarea
                id="validationSchema"
                placeholder={`{
  "nome": "Joana D'Arc",
  "inscricao": "101943",
  "categoria": "SUPLEMENTAR"
}

`}
                value={currentForm.validationSchema}
                onChange={(e) =>
                  handleInputChange("validationSchema", e.target.value)
                }
                className="font-mono text-xs min-h-[100px]"
              />
            </div>

            {/* Add Buttons */}
            <div className="space-y-2">
              <Button
                type="button"
                onClick={() => handleAddToList(true)}
                className="w-full"
                disabled={!currentForm.file}
              >
                <Plus className="mr-2 h-4 w-4" />
                Adicionar e Manter PDF
              </Button>
              <Button
                type="button"
                onClick={() => handleAddToList(false)}
                variant="outline"
                className="w-full"
                disabled={!currentForm.file}
              >
                <Plus className="mr-2 h-4 w-4" />
                Adicionar e Limpar
              </Button>
              <p className="text-xs text-muted-foreground text-center">
                Use &quot;Manter PDF&quot; para adicionar o mesmo documento com schemas diferentes
              </p>
            </div>
          </CardContent>
        </Card>

        {/* Upload List Card */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <List className="h-5 w-5" />
                Lista de Uploads
              </div>
              <Badge variant="secondary">{uploadList.length} itens</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent>
            {uploadList.length === 0 ? (
              <div className="text-center py-12 text-muted-foreground">
                <FileText className="h-12 w-12 mx-auto mb-3 opacity-20" />
                <p className="text-sm">Nenhum documento adicionado ainda</p>
                <p className="text-xs mt-1">
                  Preencha o formulário ao lado para começar
                </p>
              </div>
            ) : (
              <div className="space-y-3 max-h-[600px] overflow-y-auto pr-2">
                {uploadList.map((item, index) => (
                  <Card key={item.id} className="border-2">
                    <CardContent className="p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div className="flex-1 min-w-0 space-y-2">
                          {/* File Name */}
                          <div className="flex items-center gap-2">
                            <FileText className="h-4 w-4 text-blue-500 shrink-0" />
                            <span className="font-mono text-sm truncate">
                              {item.file.name}
                            </span>
                          </div>

                          {/* Label */}
                          <div className="flex items-center gap-2">
                            <Badge variant="outline" className="text-xs">
                              {item.label}
                            </Badge>
                            <span className="text-xs text-muted-foreground">
                              {Object.keys(JSON.parse(item.extractionSchema)).length}{" "}
                              campos
                            </span>
                            {item.validationSchema && (
                              <CheckCircle className="h-3 w-3 text-green-500" />
                            )}
                          </div>

                          {/* Schema Preview */}
                          <details className="text-xs">
                            <summary className="cursor-pointer text-blue-600 hover:text-blue-700">
                              Ver schemas
                            </summary>
                            <div className="mt-2 space-y-2">
                              <div>
                                <p className="font-semibold mb-1">Extração:</p>
                                <pre className="p-2 bg-gray-50 rounded text-xs overflow-x-auto">
                                  {item.extractionSchema}
                                </pre>
                              </div>
                              {item.validationSchema && (
                                <div>
                                  <p className="font-semibold mb-1">Validação:</p>
                                  <pre className="p-2 bg-gray-50 rounded text-xs overflow-x-auto">
                                    {item.validationSchema}
                                  </pre>
                                </div>
                              )}
                            </div>
                          </details>
                        </div>

                        {/* Remove Button */}
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          onClick={() => handleRemoveFromList(item.id)}
                          className="h-8 w-8 shrink-0 text-red-500 hover:text-red-700"
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                ))}
              </div>
            )}

            {/* Action Buttons */}
            {uploadList.length > 0 && (
              <div className="mt-4 space-y-2">
                <Button
                  onClick={handleSubmit}
                  className="w-full"
                  disabled={isLoading}
                  size="lg"
                >
                  {isLoading ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Processando {uploadList.length} documento(s)...
                    </>
                  ) : (
                    <>
                      <Upload className="mr-2 h-4 w-4" />
                      Processar {uploadList.length} Documento(s)
                    </>
                  )}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setUploadList([])}
                  className="w-full"
                  disabled={isLoading}
                >
                  Limpar Lista
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Info Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <div className="p-2 bg-blue-500/10 rounded-lg">
                <List className="h-4 w-4 text-blue-500" />
              </div>
              <div>
                <h3 className="font-semibold text-sm">Processamento em Lote</h3>
                <p className="text-xs text-muted-foreground mt-1">
                  Adicione múltiplos documentos com diferentes schemas
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
                <h3 className="font-semibold text-sm">Validação Automática</h3>
                <p className="text-xs text-muted-foreground mt-1">
                  Compare resultados com schemas de validação
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
