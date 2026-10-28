using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NoteLab.Application;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito de edição: a nota foi modificada por outro processo"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Nota nao encontrada"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Erro de validação"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor")
        };

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        };

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true; // Avisa ao .NET que o erro já foi tratado!
    }
}