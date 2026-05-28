using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Polly;
using Polly.Retry;
using TransactionalAuditLog.Configuration;
using TransactionalAuditLog.Exceptions;
using TransactionalAuditLog.Middleware;
using TransactionalAuditLog.Repositories;
using TransactionalAuditLog.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

builder.Services.AddHealthChecks();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(RateLimitPolicies.Fixed, limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromSeconds(10);
        limiterOptions.PermitLimit = 100;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.Configure<FeatureFlags>(builder.Configuration.GetSection("Features"));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var useStub = builder.Configuration.GetValue<bool>("Features:UseStubRepository");
if (useStub)
    builder.Services.AddSingleton<IAuditRepository, StubAuditRepository>();
else
    builder.Services.AddSingleton<IAuditRepository, AuditRepository>();

builder.Services.AddSingleton<DiffEngine>();
builder.Services.AddSingleton<LogPseudonymizer>();
builder.Services.AddSingleton<IDeadLetterStore, DeadLetterStore>();

// Retry pipeline for audit-store writes: one retry (two total attempts) satisfies FR-8's
// "retry at least once"; dead-letter is the durability backstop, not aggressive retrying.
builder.Services.AddResiliencePipeline(ResiliencePipelines.AuditSave, pipeline =>
{
    pipeline.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 1,
        Delay = TimeSpan.FromMilliseconds(200),
        BackoffType = DelayBackoffType.Constant
    });
});

builder.Services.AddScoped<IAuditService, AuditService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }

public static class RateLimitPolicies
{
    public const string Fixed = "fixed";
}

public static class ResiliencePipelines
{
    public const string AuditSave = "audit-save";
}
