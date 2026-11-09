using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoteLab.Domain;

namespace NoteLab.Application;

public record RefreshRequest(string RefreshToken);

/// <summary>Token = access token (JWT curto); RefreshToken = segredo opaco rotacionado a cada uso.</summary>
public record AuthResponse(string Token, DateTimeOffset ExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt);

/// <summary>
/// Identidade (M04): registro, login com access token curto + refresh token rotativo,
/// refresh com detecção de reuso, logout que revoga a sessão e reset de senha que encerra sessões.
/// </summary>
public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").RequireRateLimiting("auth");

        group.MapPost("/register", async (UserManager<IdentityUser> userManager, RegisterRequest request) =>
        {
            var user = new IdentityUser { UserName = request.Email, Email = request.Email };
            var result = await userManager.CreateAsync(user, request.Password);
            return result.Succeeded ? Results.Ok() : Results.BadRequest(result.Errors);
        });

        group.MapPost("/login", async (UserManager<IdentityUser> userManager, NoteDbContext db, JwtSettings jwt, LoginrRequest request, CancellationToken ct) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await IssueAsync(db, jwt, user.Id, familyId: null, ct));
        });

        // Rotação: o refresh usado é invalidado e substituído. Reuso de um já usado revoga a família.
        group.MapPost("/refresh", async (RefreshRequest request, NoteDbContext db, JwtSettings jwt, ILogger<Program> logger, CancellationToken ct) =>
        {
            string hash = RefreshSession.Hash(request.RefreshToken);
            RefreshSession? session = await db.RefreshSessions.SingleOrDefaultAsync(s => s.TokenHash == hash, ct);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (session is null)
            {
                return Results.Unauthorized();
            }

            if (session.UsedAt is not null || session.RevokedAt is not null)
            {
                if (session.UsedAt is not null && session.RevokedAt is null)
                {
                    logger.LogWarning("Reuso de refresh token detectado; família {FamilyId} revogada", session.FamilyId);
                }

                await RevokeFamilyAsync(db, session.FamilyId, now, ct);
                return Results.Unauthorized();
            }

            if (session.ExpiresAt <= now)
            {
                return Results.Unauthorized();
            }

            AuthResponse response = await IssueAsync(db, jwt, session.UserId, session.FamilyId, ct, rotated: session);
            return Results.Ok(response);
        });

        group.MapPost("/logout", async (RefreshRequest request, NoteDbContext db, CancellationToken ct) =>
        {
            string hash = RefreshSession.Hash(request.RefreshToken);
            RefreshSession? session = await db.RefreshSessions.SingleOrDefaultAsync(s => s.TokenHash == hash, ct);
            if (session is not null)
            {
                await RevokeFamilyAsync(db, session.FamilyId, DateTimeOffset.UtcNow, ct);
            }

            return Results.NoContent(); // não revela se o token existia
        });

        group.MapPost("/forgot-password", async (UserManager<IdentityUser> userManager, IWebHostEnvironment env, ILogger<Program> logger, ForgotPasswordRequest request) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user != null)
            {
                string token = await userManager.GeneratePasswordResetTokenAsync(user);
                // Sem provedor de e-mail configurado: o token só aparece no log em Development.
                if (env.IsDevelopment())
                {
                    logger.LogInformation("[E-MAIL SIMULADO] Token de redefinição para {Email}: {Token}", request.Email, token);
                }
            }

            return Results.Ok(new { Message = "Se o e-mail existir, receberá um link de recuperação" });
        });

        group.MapPost("/reset-password", async (UserManager<IdentityUser> userManager, NoteDbContext db, ResetPasswordRequest request, CancellationToken ct) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Results.BadRequest(new { Message = "Falha ao redefinir a senha." });
            }

            var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
            {
                return Results.BadRequest(result.Errors);
            }

            // Trocar a senha encerra todas as sessões abertas.
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await db.RefreshSessions.Where(s => s.UserId == user.Id && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
            return Results.Ok(new { Message = "Senha trocada com sucesso!" });
        });
    }

    private static async Task<AuthResponse> IssueAsync(NoteDbContext db, JwtSettings jwt, string userId, Guid? familyId, CancellationToken ct,
        RefreshSession? rotated = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        (RefreshSession session, string refreshToken) = RefreshSession.Issue(userId, familyId, TimeSpan.FromDays(jwt.RefreshDays));
        db.RefreshSessions.Add(session);
        rotated?.MarkRotated(session.Id, now);
        await db.SaveChangesAsync(ct);

        DateTimeOffset accessExpires = now.AddMinutes(jwt.AccessMinutes);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            expires: accessExpires.UtcDateTime,
            signingCredentials: new SigningCredentials(jwt.SigningKey, SecurityAlgorithms.HmacSha256));

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), accessExpires, refreshToken, session.ExpiresAt);
    }

    private static Task RevokeFamilyAsync(NoteDbContext db, Guid familyId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshSessions.Where(s => s.FamilyId == familyId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
}
