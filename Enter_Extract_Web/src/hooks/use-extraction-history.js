// hooks/use-extraction-history.js
import { useState, useEffect, useCallback } from "react";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5056";

/**
 * Hook para gerenciar histórico de extrações
 */
export function useExtractionHistory(userId = "default-user") {
  const [documents, setDocuments] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);
  const [pagination, setPagination] = useState({
    page: 0,
    pageSize: 20,
    totalCount: 0,
  });

  /**
   * Buscar histórico do usuário
   */
  const fetchHistory = useCallback(
    async (page = 0, pageSize = 20) => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${API_BASE_URL}/api/history/user/${userId}?page=${page}&pageSize=${pageSize}`
        );

        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();

        // Mapear dados da API para formato do frontend
        const mappedDocuments = data.items.map((item) => ({
          id: item.id,
          name: item.pdfFilename || "Documento sem nome",
          type: item.label || "Não categorizado",
          size: formatBytes(item.pdfSizeBytes),
          date: formatDate(item.extractedAt),
          time: formatTime(item.extractedAt),
          status: item.status || "completed",
          extracted: item.fieldsExtracted || 0,
          total: item.fieldsTotal || 0,
          processingTime: `${(item.processingTimeMs / 1000).toFixed(1)}s`,
          successRate: Math.round(item.successRate * 100),
          strategy: formatStrategies(item.strategies),
          templateId: item.templateId,
          pdfHash: item.pdfHash,
          result: item.result,
          tokensUsed: item.tokensUsed,
          costUsd: item.costUsd,
          editedManually: item.editedManually,
          extractedAt: item.extractedAt,
        }));

        setDocuments(mappedDocuments);
        setPagination({
          page: data.page,
          pageSize: data.pageSize,
          totalCount: data.count,
        });
      } catch (err) {
        console.error("Error fetching history:", err);
        setError(err.message);
      } finally {
        setIsLoading(false);
      }
    },
    [userId]
  );

  /**
   * Buscar documento específico por ID
   */
  const fetchDocument = useCallback(
    async (extractionId) => {
      try {
        const response = await fetch(
          `${API_BASE_URL}/api/history/${userId}/${extractionId}`
        );

        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        return await response.json();
      } catch (err) {
        console.error("Error fetching document:", err);
        throw err;
      }
    },
    [userId]
  );

  /**
   * Buscar estatísticas do cache
   */
  const fetchCacheStats = useCallback(async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/stats/cache`);

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      return await response.json();
    } catch (err) {
      console.error("Error fetching cache stats:", err);
      throw err;
    }
  }, []);

  // Carregar histórico inicial
  useEffect(() => {
    fetchHistory(0, 20);
  }, [fetchHistory]);

  return {
    documents,
    isLoading,
    error,
    pagination,
    fetchHistory,
    fetchDocument,
    fetchCacheStats,
    refresh: () => fetchHistory(pagination.page, pagination.pageSize),
  };
}

/**
 * Formatar bytes para formato legível
 */
function formatBytes(bytes) {
  if (!bytes || bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
}

/**
 * Formatar data (ISO para DD/MM/YYYY)
 */
function formatDate(isoDate) {
  if (!isoDate) return "-";
  const date = new Date(isoDate);
  return date.toLocaleDateString("pt-BR");
}

/**
 * Formatar hora (ISO para HH:MM)
 */
function formatTime(isoDate) {
  if (!isoDate) return "-";
  const date = new Date(isoDate);
  return date.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

/**
 * Formatar estratégias usadas
 */
function formatStrategies(strategies) {
  if (!strategies || Object.keys(strategies).length === 0) {
    return "Extração padrão";
  }

  const strategyNames = {
    regex: "Regex",
    enum: "Enum",
    proximity: "Proximidade",
    semantic: "Semântica",
    gpt: "GPT",
  };

  const used = Object.entries(strategies)
    .filter(([, count]) => count > 0)
    .map(([strategy]) => strategyNames[strategy] || strategy);

  return used.length > 0 ? used.join(" + ") : "Extração padrão";
}
