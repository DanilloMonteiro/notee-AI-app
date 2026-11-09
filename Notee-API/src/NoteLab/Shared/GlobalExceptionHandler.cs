using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NoteLab.Application;

/// <summary>
/// Converte exceções em Problem Details. Erros 500 nunca expõem mensagem interna,
/// stack trace ou dados de outra conta; o traceId permite localizar o log (M14/M15).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, exposeDetail) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito de edição: o recurso foi modificado por outro processo", false),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Não encontrado", false),
            NoteLab.Domain.DomainException => (StatusCodes.Status400BadRequest, "Regra de negócio violada", true),
            ArgumentException => (StatusCodes.Status400BadRequest, "Erro de validação", true),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Requisição inválida", false),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Não autenticado", false),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor", false)
        };

        string traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Erro não tratado (traceId {TraceId})", traceId);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            // Mensagens de domínio/validação são escritas para o usuário; as demais não são expostas.
            Detail = exposeDetail ? exception.Message : null,
        };
        problemDetails.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
