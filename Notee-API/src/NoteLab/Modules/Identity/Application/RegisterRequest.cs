namespace NoteLab.Application;

/// <summary>FullName e PhoneNumber são obrigatórios; nulos são aceitos aqui só para responder 400 com o código de erro.</summary>
public record RegisterRequest(string Email, string Password, string? FullName = null, string? PhoneNumber = null);
