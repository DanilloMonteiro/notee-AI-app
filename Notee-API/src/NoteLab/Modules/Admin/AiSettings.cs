using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Domain
{
    /// <summary>Configuração global editável pelo administrador (ex.: IA ativa). Segredos NUNCA ficam aqui.</summary>
    public sealed class SystemSetting
    {
        public string Key { get; private set; }
        public string Value { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public string UpdatedBy { get; private set; }

        private SystemSetting()
        {
            Key = null!;
            Value = null!;
            UpdatedBy = null!;
        }

        public SystemSetting(string key, string value, string updatedBy)
        {
            Key = key;
            Value = value;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        public void Set(string value, string updatedBy)
        {
            Value = value;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}

namespace NoteLab.Domain
{
    /// <summary>
    /// Chave de API cadastrada pelo painel admin. Só o valor CRIPTOGRAFADO (ASP.NET Data Protection) é guardado;
    /// usada apenas quando não há chave na configuração do servidor.
    /// </summary>
    public sealed class AiProviderSecret
    {
        public string ProviderId { get; private set; }
        public string ProtectedKey { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public string UpdatedBy { get; private set; }

        private AiProviderSecret()
        {
            ProviderId = null!;
            ProtectedKey = null!;
            UpdatedBy = null!;
        }

        public AiProviderSecret(string providerId, string protectedKey, string updatedBy)
        {
            ProviderId = providerId;
            ProtectedKey = protectedKey;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        public void Replace(string protectedKey, string updatedBy)
        {
            ProtectedKey = protectedKey;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}

namespace NoteLab.Application
{
    /// <summary>De onde vem a chave de um provedor.</summary>
    public enum KeySource
    {
        None,
        /// <summary>appsettings.json / variável de ambiente: tem prioridade e não pode ser alterada pelo painel.</summary>
        Config,
        /// <summary>Cadastrada pelo painel admin (criptografada no banco).</summary>
        Panel
    }

    /// <summary>Descrição de um provedor de IA suportado.</summary>
    public sealed record AiProviderInfo(string Id, string Name, string ConfigSection, string DefaultModel, string DefaultBaseUrl);

    /// <summary>Catálogo fixo de provedores. Chaves de API vêm só da configuração (appsettings/variáveis), nunca do painel.</summary>
    public static class AiProviders
    {
        public const string Gemini = "gemini";
        public const string ChatGpt = "chatgpt";
        public const string Claude = "claude";
        public const string Grok = "grok";
        public const string DeepSeek = "deepseek";

        public static readonly IReadOnlyList<AiProviderInfo> All =
        [
            new(Gemini, "Google Gemini", "Gemini", "gemini-3.5-flash-lite", "https://generativelanguage.googleapis.com"),
            new(ChatGpt, "OpenAI ChatGPT", "OpenAI", "gpt-4o-mini", "https://api.openai.com/v1"),
            new(Claude, "Anthropic Claude", "Claude", "claude-opus-5-5", "https://api.anthropic.com"),
            new(Grok, "xAI Grok", "Grok", "grok-4", "https://api.x.ai/v1"),
            new(DeepSeek, "DeepSeek", "DeepSeek", "deepseek-chat", "https://api.deepseek.com"),
        ];

        public static AiProviderInfo? Find(string? id) =>
            All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Chave "de verdade": ignora vazios e placeholders do appsettings.Example.json.</summary>
        public static bool IsRealKey(string? key) =>
            !string.IsNullOrWhiteSpace(key)
            && !key.StartsWith("COLOQUE", StringComparison.OrdinalIgnoreCase)
            && !key.Contains("SUA_CHAVE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// IA ativa e modelo por provedor, persistidos em SystemSettings e cacheados por alguns segundos
    /// (várias instâncias da API enxergam a troca em até CacheTtl).
    /// </summary>
    public sealed class AiSettings
    {
        public const string ActiveKey = "ai.activeProvider";
        public static string ModelKey(string providerId) => $"ai.model.{providerId}";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly object _lock = new();
        private Dictionary<string, string> _cache = new();
        private Dictionary<string, string> _secrets = new();
        private readonly IDataProtector _protector;
        private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

        public AiSettings(IServiceScopeFactory scopes, IConfiguration config, IDataProtectionProvider dataProtection)
        {
            _scopes = scopes;
            _config = config;
            _protector = dataProtection.CreateProtector("Notee.AiProviderKeys.v1");
        }

        public string ActiveProvider =>
            Get(ActiveKey) ?? AiProviders.Find(_config["Ai:ActiveProvider"])?.Id ?? AiProviders.Gemini;

        public string ModelFor(AiProviderInfo provider) =>
            Get(ModelKey(provider.Id)) ?? _config[$"{provider.ConfigSection}:Model"] ?? provider.DefaultModel;

        public string BaseUrlFor(AiProviderInfo provider) =>
            (_config[$"{provider.ConfigSection}:BaseUrl"] ?? provider.DefaultBaseUrl).TrimEnd('/');

        /// <summary>Precedência: configuração do servidor; se não houver, a chave cadastrada pelo painel.</summary>
        public string? ApiKeyFor(AiProviderInfo provider) =>
            ConfigKey(provider) ?? PanelKey(provider);

        public bool IsConfigured(AiProviderInfo provider) => AiProviders.IsRealKey(ApiKeyFor(provider));

        public KeySource SourceOf(AiProviderInfo provider) =>
            ConfigKey(provider) is not null ? KeySource.Config : PanelKey(provider) is not null ? KeySource.Panel : KeySource.None;

        private string? ConfigKey(AiProviderInfo provider) =>
            _config[$"{provider.ConfigSection}:ApiKey"] is { } k && AiProviders.IsRealKey(k) ? k : null;

        private string? PanelKey(AiProviderInfo provider)
        {
            Get(ActiveKey); // garante cache carregado
            string? protectedKey;
            lock (_lock)
            {
                _secrets.TryGetValue(provider.Id, out protectedKey);
            }

            if (protectedKey is null)
            {
                return null;
            }

            try
            {
                return _protector.Unprotect(protectedKey);
            }
            catch (CryptographicException)
            {
                // Chaves do Data Protection trocadas/perdidas: trata como ausente (o admin cadastra de novo).
                return null;
            }
        }

        /// <summary>Salva a chave criptografada. Falha se já existir chave na configuração do servidor.</summary>
        public async Task SetPanelKeyAsync(AiProviderInfo provider, string apiKey, string updatedBy, CancellationToken ct)
        {
            if (ConfigKey(provider) is not null)
            {
                throw new InvalidOperationException("Chave definida na configuração do servidor.");
            }

            using IServiceScope scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
            string protectedKey = _protector.Protect(apiKey);
            AiProviderSecret? secret = await db.AiProviderSecrets.SingleOrDefaultAsync(s => s.ProviderId == provider.Id, ct);
            if (secret is null)
            {
                db.AiProviderSecrets.Add(new AiProviderSecret(provider.Id, protectedKey, updatedBy));
            }
            else
            {
                secret.Replace(protectedKey, updatedBy);
            }

            await db.SaveChangesAsync(ct);
            Invalidate();
        }

        public async Task<bool> RemovePanelKeyAsync(AiProviderInfo provider, CancellationToken ct)
        {
            using IServiceScope scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
            int removed = await db.AiProviderSecrets.Where(s => s.ProviderId == provider.Id).ExecuteDeleteAsync(ct);
            Invalidate();
            return removed > 0;
        }

        public async Task SetAsync(string key, string value, string updatedBy, CancellationToken ct)
        {
            using IServiceScope scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
            SystemSetting? setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.Key == key, ct);
            if (setting is null)
            {
                db.SystemSettings.Add(new SystemSetting(key, value, updatedBy));
            }
            else
            {
                setting.Set(value, updatedBy);
            }

            await db.SaveChangesAsync(ct);
            Invalidate();
        }

        public async Task<SystemSetting?> GetRecordAsync(string key, CancellationToken ct)
        {
            using IServiceScope scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
            return await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Key == key, ct);
        }

        public void Invalidate()
        {
            lock (_lock)
            {
                _loadedAt = DateTimeOffset.MinValue;
            }
        }

        private string? Get(string key)
        {
            lock (_lock)
            {
                if (DateTimeOffset.UtcNow - _loadedAt > CacheTtl)
                {
                    using IServiceScope scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
                    _cache = db.SystemSettings.AsNoTracking()
                        .Where(s => s.Key.StartsWith("ai."))
                        .ToDictionary(s => s.Key, s => s.Value);
                    _secrets = db.AiProviderSecrets.AsNoTracking().ToDictionary(s => s.ProviderId, s => s.ProtectedKey);
                    _loadedAt = DateTimeOffset.UtcNow;
                }

                return _cache.TryGetValue(key, out string? value) ? value : null;
            }
        }
    }
}
