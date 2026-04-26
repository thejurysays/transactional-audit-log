namespace TransactionalAuditLog.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger, IWebHostEnvironment environment)
{
    internal const string HeaderName = "X-Correlation-ID";

    private static readonly string AppVersion =
        typeof(CorrelationIdMiddleware).Assembly.GetName().Version?.ToString() ?? "unknown";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["MachineName"] = Environment.MachineName,
            ["Environment"] = environment.EnvironmentName,
            ["AppVersion"] = AppVersion
        }))
        {
            await next(context);
        }
    }
}
