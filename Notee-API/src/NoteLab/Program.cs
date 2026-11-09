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

// Permite apontar para outro banco (ex.: testes) via ConnectionStrings__Default,
// mantendo o padrão local anterior quando nada for configurado.
string connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5434;Database=notelab_db;Username=postgres;Password=admin";

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

builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<IDoubtRepository, DoubtRepository>();
builder.Services.AddScoped<BoardService>();
builder.Services.AddDbContext<NoteDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<NoteDbContext>().AddDefaultTokenProviders();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHostedService<DoubtProcessingWorker>(); // É assim que se regista um BackgroundService
builder.Services.AddHttpClient<IAiService, GeminiAiService>(); // OpenAiService

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
            // Adicione as 3 linhas abaixo:
            ValidIssuer = "NoteeApi",
            ValidAudience = "NoteeApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperChaveSecretaDoNoteeComPeloMenos32Caracteres!"))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendUI", policy =>
    {
        // Aqui listamos as origens permitidas (sem a barra final "/")
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:4200")
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
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("FrontendUI");
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();
app.MapNoteEndpoints();
app.MapResearchEndpoints();
app.MapNoteReferenceEndpoints();
app.MapBoardEndpoints();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

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

