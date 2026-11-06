using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NoteLab.Application; // Ajuste para o namespace correto se necessário

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        // Cria um grupo base para todas as rotas começarem com /auth
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (UserManager<IdentityUser> userManager, CancellationToken ct, RegisterRequest request) =>
        {
            var user = new IdentityUser { UserName = request.Email, Email = request.Email };

            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return Results.BadRequest(result.Errors);
            }

            return Results.Ok();
        });

        group.MapPost("/login", async (UserManager<IdentityUser> userManager, CancellationToken ct, LoginrRequest request) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Results.Unauthorized();
            }

            // 1. Definimos o payload (as Claims)
            var claims = new[] 
            { 
                new Claim(ClaimTypes.NameIdentifier, user.Id) 
            };

            // 2. Definimos a chave e o algoritmo de assinatura
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperChaveSecretaDoNoteeComPeloMenos32Caracteres!"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 3. Montamos o token
            var token = new JwtSecurityToken(
                issuer: "NoteeApi",
                audience: "NoteeApp",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(240),
                signingCredentials: creds
            );

            // 4. Serializamos para string
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Results.Ok(new { Token = tokenString });
        });

        group.MapPost("/forgot-password", async (UserManager<IdentityUser> userManager, ForgotPasswordRequest request) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            if (user != null)
            {
            var token = await userManager.GeneratePasswordResetTokenAsync(user); 
            Console.WriteLine($"[FAKE EMAIL] Token para {request.Email}: {token}");
            }

            return Results.Ok(new { Message = "Se o e-mail existir, receberá um link de recuperação"});
        });

        group.MapPost("/reset-password", async (UserManager<IdentityUser> userManager, ResetPasswordRequest request) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            if (user == null)
            {
            return Results.BadRequest(new { Message = "Falha ao redefinir a senha." });
            }

            var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

            if (result.Succeeded)
            {
                return Results.Ok(new { Message = "Senha trocada com sucesso!"});
            }

            return Results.BadRequest(result.Errors);   
        });
    }
}