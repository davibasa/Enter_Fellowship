// hooks/use-stats.js
import { useState, useCallback } from "react";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5056";

/**
 * Hook para buscar estatísticas da aplicação
 */
export function useStats() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);

  /**
   * Buscar estatísticas de cache
   */
  const getCacheStats = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(`${API_BASE_URL}/api/stats/cache`);

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();
      return data;
    } catch (err) {
      console.error("Error fetching cache stats:", err);
      setError(err.message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Buscar métricas de cache por dia
   */
  const getCacheMetrics = useCallback(async (date = null) => {
    setIsLoading(true);
    setError(null);

    try {
      const dateParam = date || new Date().toISOString().split("T")[0];
      const response = await fetch(`${API_BASE_URL}/api/cache/metrics/${dateParam}`);

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();
      return data;
    } catch (err) {
      console.error("Error fetching cache metrics:", err);
      setError(err.message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Buscar resumo de métricas de N dias
   */
  const getCacheMetricsSummary = useCallback(async (days = 7) => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/cache/metrics/summary?days=${days}`
      );

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();
      return data;
    } catch (err) {
      console.error("Error fetching cache metrics summary:", err);
      setError(err.message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Buscar métricas agregadas de N dias
   */
  const getAggregateMetrics = useCallback(async (days = 7) => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/cache/metrics/aggregate?days=${days}`
      );

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();
      return data;
    } catch (err) {
      console.error("Error fetching aggregate metrics:", err);
      setError(err.message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Buscar estatísticas globais de um dia
   */
  const getGlobalStats = useCallback(async (date = null) => {
    setIsLoading(true);
    setError(null);

    try {
      const dateParam = date || new Date().toISOString().split("T")[0];
      const response = await fetch(`${API_BASE_URL}/api/stats/global/${dateParam}`);

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();
      return data;
    } catch (err) {
      console.error("Error fetching global stats:", err);
      setError(err.message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Buscar estatísticas globais de múltiplos dias
   */
  const getGlobalStatsRange = useCallback(async (dates) => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(`${API_BASE_URL}/api/stats/global/range`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(dates),
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();
      return data;
    } catch (err) {
      console.error("Error fetching global stats range:", err);
      setError(err.message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    isLoading,
    error,
    getCacheStats,
    getCacheMetrics,
    getCacheMetricsSummary,
    getAggregateMetrics,
    getGlobalStats,
    getGlobalStatsRange,
  };
}
