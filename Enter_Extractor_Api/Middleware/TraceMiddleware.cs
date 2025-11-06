namespace Enter_Extractor_Api.Middleware;

/// <summary>
/// Middleware que adiciona trace_id a todas as requisições
/// </summary>
public class TraceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TraceMiddleware> _logger;
    private const string TraceIdHeader = "X-Trace-ID";
    private const string RequestIdHeader = "X-Request-ID";

    public TraceMiddleware(RequestDelegate next, ILogger<TraceMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Gera ou recupera trace_id
        var traceId = context.Request.Headers[TraceIdHeader].FirstOrDefault()
                      ?? Guid.NewGuid().ToString("N")[..16];

        var requestId = context.Request.Headers[RequestIdHeader].FirstOrDefault()
                        ?? Guid.NewGuid().ToString("N")[..12];

        // Adiciona aos headers de resposta
        context.Response.Headers[TraceIdHeader] = traceId;
        context.Response.Headers[RequestIdHeader] = requestId;

        // Adiciona ao HttpContext para uso posterior
        context.Items["TraceId"] = traceId;
        context.Items["RequestId"] = requestId;

        // Log início da requisição
        _logger.LogInformation(
            "🚀 [TraceId: {TraceId}] [RequestId: {RequestId}] {Method} {Path}",
            traceId, requestId, context.Request.Method, context.Request.Path
        );

        var startTime = DateTime.UtcNow;

        try
        {
            await _next(context);
        }
        finally
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation(
                "✅ [TraceId: {TraceId}] [RequestId: {RequestId}] {StatusCode} - {Duration}ms",
                traceId, requestId, context.Response.StatusCode, duration
            );
        }
    }
}

/// <summary>
/// Extension method para registrar o middleware
/// </summary>
public static class TraceMiddlewareExtensions
{
    public static IApplicationBuilder UseTraceMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TraceMiddleware>();
    }
}
