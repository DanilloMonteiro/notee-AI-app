using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

/// <param name="Kind">blocked ou trusted.</param>
public sealed record SourceRuleRequest(string? Domain, string? Kind);

public sealed record SourceRuleResponse(string Domain, string Kind, bool Fixed);

/// <summary>Sites bloqueados e confiáveis de cada pessoa (ADR-047). Os bloqueios fixos do servidor vêm marcados como <c>Fixed</c>.</summary>
public static class SourceRuleEndpoints
{
    /// <summary>Regras da pessoa somadas aos bloqueios fixos do servidor.</summary>
    public static async Task<SourceRules> LoadAsync(NoteDbContext db, string ownerId, SourceSearchOptions options, CancellationToken ct)
    {
        var rows = await db.SourceDomainRules.AsNoTracking().Where(r => r.OwnerId == ownerId)
            .Select(r => new { r.Domain, r.Kind }).ToListAsync(ct);
        return new SourceRules(
            options.BlockedDomains.Concat(rows.Where(r => r.Kind == SourceDomainRuleKind.Blocked).Select(r => r.Domain)),
            rows.Where(r => r.Kind == SourceDomainRuleKind.Trusted).Select(r => r.Domain));
    }

    public static void MapSourceRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/source-rules").RequireAuthorization().WithTags("Me");

        group.MapGet("/", async (ClaimsPrincipal user, NoteDbContext db, IConfiguration config, CancellationToken ct) =>
            Results.Ok(await ListAsync(db, Owner(user), SourceSearchOptions.From(config), ct)));

        // Cria ou troca o tipo (bloqueado ⇄ confiável) de um site.
        group.MapPut("/", async (SourceRuleRequest body, ClaimsPrincipal user, NoteDbContext db, IConfiguration config, CancellationToken ct) =>
        {
            string owner = Owner(user);
            SourceSearchOptions options = SourceSearchOptions.From(config);
            string? domain = SourceRules.NormalizeDomain(body.Domain);
            if (domain is null || domain.Length > SourceDomainRule.MaxDomainLength)
            {
                return Problem(400, "Informe um site válido, por exemplo scielo.br.");
            }

            SourceDomainRuleKind? kind = body.Kind?.Trim().ToLowerInvariant() switch
            {
                "blocked" => SourceDomainRuleKind.Blocked,
                "trusted" => SourceDomainRuleKind.Trusted,
                _ => null,
            };
            if (kind is null)
            {
                return Problem(400, "Tipo inválido (use blocked ou trusted).");
            }

            if (kind == SourceDomainRuleKind.Trusted && new SourceRules(options.BlockedDomains, []).IsBlocked("https://" + domain))
            {
                return Problem(400, "Este site é bloqueado pelo Notee e não pode ser marcado como confiável.");
            }

            SourceDomainRule? rule = await db.SourceDomainRules.SingleOrDefaultAsync(r => r.OwnerId == owner && r.Domain == domain, ct);
            if (rule is null)
            {
                if (await db.SourceDomainRules.CountAsync(r => r.OwnerId == owner, ct) >= SourceDomainRule.MaxPerOwner)
                {
                    return Problem(400, $"Limite de {SourceDomainRule.MaxPerOwner} sites atingido.");
                }

                db.SourceDomainRules.Add(new SourceDomainRule(owner, domain, kind.Value));
            }
            else
            {
                rule.Change(kind.Value);
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Problem(409, "O site foi alterado em outra requisição. Recarregue a página.");
            }

            return Results.Ok(await ListAsync(db, owner, options, ct));
        });

        group.MapDelete("/{domain}", async (string domain, ClaimsPrincipal user, NoteDbContext db, IConfiguration config, CancellationToken ct) =>
        {
            string owner = Owner(user);
            string? normalized = SourceRules.NormalizeDomain(domain);
            SourceDomainRule? rule = normalized is null ? null
                : await db.SourceDomainRules.SingleOrDefaultAsync(r => r.OwnerId == owner && r.Domain == normalized, ct);
            if (rule is null)
            {
                return Results.NotFound();
            }

            db.SourceDomainRules.Remove(rule);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ListAsync(db, owner, SourceSearchOptions.From(config), ct));
        });
    }

    private static async Task<List<SourceRuleResponse>> ListAsync(NoteDbContext db, string owner, SourceSearchOptions options, CancellationToken ct)
    {
        var rows = await db.SourceDomainRules.AsNoTracking().Where(r => r.OwnerId == owner).OrderBy(r => r.Domain).ToListAsync(ct);
        return options.BlockedDomains.Select(d => new SourceRuleResponse(d, "blocked", true))
            .Concat(rows.Select(r => new SourceRuleResponse(r.Domain, r.Kind == SourceDomainRuleKind.Blocked ? "blocked" : "trusted", false)))
            .ToList();
    }

    private static IResult Problem(int status, string detail) =>
        Results.Problem(statusCode: status, title: status == 409 ? "Conflito" : "Erro de validação", detail: detail);

    private static string Owner(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
}
