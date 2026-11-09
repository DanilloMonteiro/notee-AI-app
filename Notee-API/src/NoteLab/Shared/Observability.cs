using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NoteLab.Domain;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NoteLab.Application;

/// <summary>
/// Telemetria própria do Notee (M15). Tags carregam apenas IDs, estados e providers —
/// nunca texto de notas, perguntas ou respostas.
/// </summary>
public static class NoteeTelemetry
{
    public const string SourceName = "Notee.Research";

    public static readonly ActivitySource Research = new(SourceName);
    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> JobsEnqueued =
        Meter.CreateCounter<long>("notee.research.jobs.enqueued", description: "Jobs de pesquisa enfileirados");
    public static readonly Counter<long> JobsFinished =
        Meter.CreateCounter<long>("notee.research.jobs.finished", description: "Tentativas finalizadas por resultado");
    public static readonly Histogram<double> JobDuration =
        Meter.CreateHistogram<double>("notee.research.job.duration", unit: "ms", description: "Duração do processamento de um job");
}

/// <summary>Prontidão: o banco precisa responder.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly NoteDbContext _db;

    public DatabaseHealthCheck(NoteDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await _db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy("PostgreSQL acessível")
            : HealthCheckResult.Unhealthy("PostgreSQL inacessível");
}

public static class ObservabilityExtensions
{
    /// <summary>OpenTelemetry (traces + métricas). Exporta via OTLP só se OTEL_EXPORTER_OTLP_ENDPOINT estiver definido.</summary>
    public static IServiceCollection AddNoteeObservability(this IServiceCollection services, IConfiguration config)
    {
        bool otlp = !string.IsNullOrWhiteSpace(config["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("notee-api"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation(o => o.RecordException = true)
                    .AddHttpClientInstrumentation()
                    .AddSource(NoteeTelemetry.SourceName);
                if (otlp)
                {
                    t.AddOtlpExporter();
                }
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(NoteeTelemetry.SourceName);
                if (otlp)
                {
                    m.AddOtlpExporter();
                }
            });

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        // Problem Details sempre com traceId para correlacionar com logs/traces.
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);

        return services;
    }

    /// <summary>Limite global por usuário autenticado (ou IP), além do limite de /auth (M14).</summary>
    public static PartitionedRateLimiter<HttpContext> ApiLimiter(int perMinute) =>
        PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            string key = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { } id
                ? $"user:{id}"
                : $"ip:{ctx.Connection.RemoteIpAddress}";
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = perMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
        });

    public static IEndpointRouteBuilder MapNoteeHealth(this IEndpointRouteBuilder app)
    {
        // Liveness não consulta dependências; readiness verifica o banco.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
        return app;
    }

    /// <summary>Cabeçalhos de segurança para uma API JSON.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            ctx.Response.OnStarting(() =>
            {
                IHeaderDictionary h = ctx.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                if (!ctx.Request.Path.StartsWithSegments("/swagger"))
                {
                    h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                }

                return Task.CompletedTask;
            });
            await next();
        });
}
