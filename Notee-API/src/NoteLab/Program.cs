using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using NoteLab.Application;
using NoteLab.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NoteLab.Infrastructure;
using Microsoft.Extensions.Logging.Console;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;


var builder = WebApplication.CreateBuilder(args);

// Segredos vêm da configuração (appsettings.json local, variáveis de ambiente ou user-secrets).
// Sem configuração, valores de DESENVOLVIMENTO são usados com aviso no log.
string connectionString = ConnectionStrings.Resolve(builder.Configuration, out bool devConnection);
JwtSettings jwt = JwtSettings.From(builder.Configuration);
builder.Services.AddSingleton(jwt);
string[] corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:5173", "http://localhost:4200"];

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "[HH:mm:ss] ";
    options.ColorBehavior = LoggerColorBehavior.Enabled;
    options.IncludeScopes = false;
});

builder.Services.AddSerilog((services, config) => config
    .MinimumLevel.Warning()

    // Permite logs informativos da aplicação
    .MinimumLevel.Override(
        "Notee",
        LogEventLevel.Information
    )

    // Permite resumos informativos das requisições
    .MinimumLevel.Override(
        "Serilog.AspNetCore.RequestLoggingMiddleware",
        LogEventLevel.Information
    )
    .MinimumLevel.Override("NoteLab", LogEventLevel.Information)
    .MinimumLevel.Override("Program", LogEventLevel.Information)

    .Enrich.FromLogContext()

    .WriteTo.Console(
        theme: AnsiConsoleTheme.Code,
        outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] " +
            "{Message:lj}{NewLine}{Exception}"
    )
);

builder.Services.AddScoped<NoteRepository>();
builder.Services.AddScoped<INoteRepository>(sp => sp.GetRequiredService<NoteRepository>());
builder.Services.AddScoped<IDoubtRepository, DoubtRepository>();
builder.Services.AddScoped<BoardService>();
builder.Services.AddScoped<ResearchService>();
builder.Services.AddScoped<GraphContextService>();
builder.Services.AddDbContext<NoteDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<NoteDbContext>().AddDefaultTokenProviders();
builder.Services.AddNoteeObservability(builder.Configuration); // OpenTelemetry, health checks, ProblemDetails com traceId
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization(o => o.AddPolicy(AdminEndpoints.Policy, p => p.RequireRole(AdminEndpoints.AdminRole)));
builder.Services.AddNoteeMessaging(builder.Configuration); // Outbox + (opcional) SQS no LocalStack
builder.Services.AddHostedService<DoubtProcessingWorker>(); // É assim que se regista um BackgroundService

// Providers de IA: Fake determinístico é o padrão; externo só com consentimento do dono (ADR-019).
builder.Services.AddSingleton<FakeAiService>();
// Adaptadores de IA (ADR-030). O ativo é escolhido no painel admin; chaves só por configuração.
builder.Services.AddSingleton<AiSettings>();
builder.Services.AddHttpClient<GeminiAiService>();
builder.Services.AddHttpClient<ChatGptAiService>();
builder.Services.AddHttpClient<GrokAiService>();
builder.Services.AddHttpClient<DeepSeekAiService>();
builder.Services.AddScoped<ClaudeAiService>();
builder.Services.AddScoped<AiProviderSelector>();
builder.Services.AddScoped<PreferencesService>();

// Limite de tentativas nas rotas de autenticação (M04).
int authPerMinute = builder.Configuration.GetValue<int?>("RateLimit:AuthPerMinute") ?? 10;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Limite global por usuário/IP (M14); /auth tem limite próprio mais restrito.
    options.GlobalLimiter = ObservabilityExtensions.ApiLimiter(builder.Configuration.GetValue<int?>("RateLimit:ApiPerMinute") ?? 600);
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SigningKey
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendUI", policy =>
    {
        // Origens permitidas vêm de Cors:Origins (padrão: portas locais do Vite/React)
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()  // Permite enviarmos o cabeçalho "Authorization: Bearer..."
              .AllowAnyMethod(); // Permite GET, POST, PUT, DELETE, etc.
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} " +
        "→ {StatusCode} ({Elapsed:0.0} ms)";

    options.GetLevel = (context, elapsed, exception) =>
    {
        if (exception != null ||
            context.Response.StatusCode >= 500)
            return LogEventLevel.Error;

        if (context.Response.StatusCode >= 400)
            return LogEventLevel.Warning;

        return LogEventLevel.Information;
    };
});


app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseStatusCodePages();
// Swagger só em Development ou se Swagger:Enabled=true (não expor o contrato em produção por padrão).
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("FrontendUI");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapNoteeHealth();
app.MapIdentityEndpoints();
app.MapPreferenceEndpoints();
app.MapNoteEndpoints();
app.MapResearchEndpoints();
app.MapNoteReferenceEndpoints();
app.MapBoardEndpoints();
app.MapGraphAiEndpoints();
app.MapPublishingEndpoints();
app.MapNoteAnalysisEndpoints();
app.MapAdminEndpoints();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

if (jwt.UsesDevelopmentKey)
{
    logger.LogWarning("Jwt:Key não configurada — usando chave de DESENVOLVIMENTO. Configure Jwt__Key fora de ambiente local.");
}

if (devConnection)
{
    logger.LogWarning("ConnectionStrings:Default não configurada — usando o Postgres local de desenvolvimento.");
}

// Verificar conexão com PostgreSQL
try
{
    logger.LogInformation("Verificando conexão com PostgreSQL...");

    await using var scope = app.Services.CreateAsyncScope();

    var db = scope.ServiceProvider
        .GetRequiredService<NoteDbContext>();

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    await db.Database.OpenConnectionAsync();

    stopwatch.Stop();

    logger.LogInformation(
        "PostgreSQL conectado | Banco: {Database} | {Elapsed} ms",
        db.Database.GetDbConnection().Database,
        stopwatch.ElapsedMilliseconds
    );

    await db.Database.CloseConnectionAsync();
}
catch (Exception ex)
{
    logger.LogError(
        ex,
        "Falha ao conectar ao PostgreSQL durante a inicialização"
    );

    throw;
}

await AdminEndpoints.SeedAdminAsync(app.Services, app.Configuration, logger);

app.Lifetime.ApplicationStarted.Register(() =>
{
    logger.LogInformation(
        "NoteLab API iniciada | Ambiente: {Environment}",
        app.Environment.EnvironmentName
    );

    foreach (var url in app.Urls)
    {
        logger.LogInformation(
            "API disponível em {Url}",
            url
        );
    }
});

app.Run();

