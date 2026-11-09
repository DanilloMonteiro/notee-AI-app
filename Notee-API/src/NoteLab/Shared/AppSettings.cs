using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NoteLab.Application;

/// <summary>
/// Configuração de JWT lida de "Jwt:*" (appsettings, variáveis Jwt__Key etc.).
/// Sem configuração, usa um segredo de DESENVOLVIMENTO e registra aviso — nunca use em produção.
/// </summary>
public sealed class JwtSettings
{
    public const string DevelopmentKey = "DEV-ONLY-notee-chave-local-com-pelo-menos-32-caracteres";

    public string Key { get; init; } = DevelopmentKey;
    public string Issuer { get; init; } = "NoteeApi";
    public string Audience { get; init; } = "NoteeApp";
    public int ExpirationHours { get; init; } = 12;

    public bool UsesDevelopmentKey => Key == DevelopmentKey;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));

    public static JwtSettings From(IConfiguration config)
    {
        var section = config.GetSection("Jwt");
        var settings = new JwtSettings
        {
            Key = string.IsNullOrWhiteSpace(section["Key"]) ? DevelopmentKey : section["Key"]!,
            Issuer = section["Issuer"] ?? "NoteeApi",
            Audience = section["Audience"] ?? "NoteeApp",
            ExpirationHours = int.TryParse(section["ExpirationHours"], out int h) && h > 0 ? h : 12,
        };

        if (Encoding.UTF8.GetByteCount(settings.Key) < 32)
        {
            throw new InvalidOperationException("Jwt:Key deve ter pelo menos 32 bytes.");
        }

        return settings;
    }
}

public static class ConnectionStrings
{
    public const string DevelopmentDefault = "Host=localhost;Port=5434;Database=notelab_db;Username=postgres;Password=admin";

    public static string Resolve(IConfiguration config, out bool usesDevelopmentDefault)
    {
        string? configured = config.GetConnectionString("Default");
        usesDevelopmentDefault = string.IsNullOrWhiteSpace(configured);
        return usesDevelopmentDefault ? DevelopmentDefault : configured!;
    }
}
