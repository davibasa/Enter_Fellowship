import { useEffect, useRef, useState, useCallback } from "react";

/**
 * Hook customizado para gerenciar conexão SSE (Server-Sent Events)
 * com reconexão automática e tratamento de erros
 */
export function useSSE(url, options = {}) {
  const {
    enabled = true,
    reconnect = true,
    reconnectInterval = 3000,
    onMessage,
    onError,
    onOpen,
    onComplete,
  } = options;

  const [status, setStatus] = useState("idle"); // idle, connecting, connected, error, completed
  const [error, setError] = useState(null);
  const eventSourceRef = useRef(null);
  const reconnectTimeoutRef = useRef(null);
  const isManualClose = useRef(false);
  const connectRef = useRef(null);

  // Limpar reconexão pendente
  const clearReconnectTimeout = useCallback(() => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }
  }, []);

  // Desconectar do SSE
  const disconnect = useCallback(() => {
    console.log("🔌 Disconnecting SSE");
    isManualClose.current = true;
    clearReconnectTimeout();

    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }

    setStatus("idle");
  }, [clearReconnectTimeout]);

  // Conectar ao SSE
  const connect = useCallback(() => {
    if (!url || !enabled) return;

    console.log(`🔌 Connecting to SSE: ${url}`);
    setStatus("connecting");
    setError(null);

    try {
      const eventSource = new EventSource(url);
      eventSourceRef.current = eventSource;

      // Evento: Conexão aberta
      eventSource.onopen = () => {
        console.log("✅ SSE connected");
        setStatus("connected");
        setError(null);
        clearReconnectTimeout();
        onOpen?.();
      };

      // Evento: Progresso
      eventSource.addEventListener("progress", (e) => {
        const data = JSON.parse(e.data);
        console.log("📊 Progress:", data);
        onMessage?.({ type: "progress", data });
      });

      // Evento: Resultado
      eventSource.addEventListener("result", (e) => {
        const data = JSON.parse(e.data);
        console.log("✅ Result:", data);
        onMessage?.({ type: "result", data });
      });

      // Evento: Erro
      eventSource.addEventListener("error", (e) => {
        const data = JSON.parse(e.data);
        console.log("❌ Error:", data);
        onMessage?.({ type: "error", data });
      });

      // Evento: Completo
      eventSource.addEventListener("complete", (e) => {
        const data = JSON.parse(e.data);
        console.log("🎉 Complete:", data);
        setStatus("completed");
        onMessage?.({ type: "complete", data });
        onComplete?.(data);
        
        // Fechar conexão
        if (eventSourceRef.current) {
          eventSourceRef.current.close();
          eventSourceRef.current = null;
        }
      });

      // Evento: Erro genérico
      eventSource.onerror = (e) => {
        console.error("❌ SSE error:", e);

        // Se foi fechamento manual, não reconectar
        if (isManualClose.current) {
          console.log("🔌 Manual disconnect, not reconnecting");
          return;
        }

        setStatus("error");
        const errorMsg = "Connection error";
        setError(errorMsg);
        onError?.(errorMsg);

        // Fechar conexão atual
        eventSource.close();

        // Reconectar se habilitado
        if (reconnect && !isManualClose.current) {
          console.log(`🔄 Reconnecting in ${reconnectInterval}ms...`);
          reconnectTimeoutRef.current = setTimeout(() => {
            connectRef.current?.();
          }, reconnectInterval);
        }
      };
    } catch (err) {
      console.error("❌ Failed to create EventSource:", err);
      setStatus("error");
      setError(err.message);
      onError?.(err.message);
    }
  }, [url, enabled, reconnect, reconnectInterval, onMessage, onError, onOpen, onComplete, clearReconnectTimeout]);

  // Efeito: Conectar/desconectar
  useEffect(() => {
    // Salvar referência para uso no timeout de reconexão
    connectRef.current = connect;
    
    if (enabled && url) {
      isManualClose.current = false;
      // Usar timeout para evitar setState durante render
      const timeoutId = setTimeout(() => connect(), 0);
      return () => {
        clearTimeout(timeoutId);
        disconnect();
      };
    }

    return undefined;
  }, [url, enabled, connect, disconnect]);

  return {
    status,
    error,
    isConnected: status === "connected",
    isConnecting: status === "connecting",
    isCompleted: status === "completed",
    hasError: status === "error",
    disconnect,
    reconnect: connect,
  };
}
