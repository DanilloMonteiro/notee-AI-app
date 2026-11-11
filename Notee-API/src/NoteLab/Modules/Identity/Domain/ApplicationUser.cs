using Microsoft.AspNetCore.Identity;

namespace NoteLab.Domain;

/// <summary>
/// Conta do Notee. Estende o usuário do ASP.NET Identity com o nome da pessoa (privado,
/// diferente do DisplayName do perfil público). O telefone usa a coluna PhoneNumber do Identity.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    public const int FullNameMinLength = 2;
    public const int FullNameMaxLength = 120;

    /// <summary>Nulo apenas em contas criadas antes do cadastro com nome.</summary>
    public string? FullName { get; set; }
}

/// <summary>Normalização e validação de nome e telefone informados no cadastro.</summary>
public static class AccountData
{
    /// <summary>Remove espaços extras. Devolve null se o nome for inválido.</summary>
    public static string? NormalizeFullName(string? value)
    {
        string name = string.Join(' ', (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return name.Length is >= ApplicationUser.FullNameMinLength and <= ApplicationUser.FullNameMaxLength ? name : null;
    }

    /// <summary>
    /// Mantém só dígitos (e um "+" inicial). Aceita de 10 a 15 dígitos, ex.: (11) 98765-4321 ou +55 11 98765-4321.
    /// Devolve null quando vazio ou inválido.
    /// </summary>
    public static string? NormalizePhone(string? value)
    {
        string trimmed = (value ?? "").Trim();
        string digits = new(trimmed.Where(char.IsAsciiDigit).ToArray());
        bool onlyPhoneChars = trimmed.All(c => char.IsAsciiDigit(c) || c is ' ' or '(' or ')' or '-' or '.' or '+');
        bool plusOnlyAtStart = trimmed.LastIndexOf('+') <= 0;
        if (!onlyPhoneChars || !plusOnlyAtStart || digits.Length is < 10 or > 15)
        {
            return null;
        }

        return trimmed.StartsWith('+') ? "+" + digits : digits;
    }
}
