using System.Security.Claims;
using NoteLab.Domain;

namespace NoteLab.Application;

public static class ResearchEndpoints
{
    public static void MapResearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/doubts").RequireAuthorization();

        // Dúvida a partir de trecho selecionado no editor. A pesquisa é enfileirada na mesma transação.
        group.MapPost("/", async (CreateDoubtRequest request, ResearchService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await service.CreateSelectionDoubtAsync(Owner(user), request, ct);
            if (!result.IsSuccess)
            {
                return BoardEndpoints.ToProblem(result);
            }

            (Doubt doubt, ResearchJob? job) = result.Value;
            return Results.Ok(new { DoubtId = doubt.Id, Status = doubt.Status.ToString(), JobId = job?.Id });
        });

        group.MapGet("/{id:guid}", async (Guid id, IDoubtRepository doubtRepo, ResearchService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            Doubt? doubt = await doubtRepo.GetByIdAsync(Owner(user), id, ct);
            if (doubt == null)
            {
                return Results.NotFound("Duvida nao encontrada.");
            }

            Dictionary<Guid, ResearchJob> jobs = await service.LatestJobsAsync([doubt.Id], ct);
            return Results.Ok(DoubtResponse.From(doubt, jobs.GetValueOrDefault(doubt.Id)));
        });

        // Lista vazia é 200 [] (antes respondia 404).
        group.MapGet("/", async (IDoubtRepository doubtRepo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            List<Doubt> doubts = await doubtRepo.GetAllAsync(Owner(user), ct);
            return Results.Ok(doubts.Select(d => DoubtResponse.From(d)).ToList());
        });

        group.MapGet("/note/{noteId:guid}", async (Guid noteId, IDoubtRepository doubtRepo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            List<Doubt> doubts = await doubtRepo.GetByNoteIdAsync(Owner(user), noteId, ct);
            return Results.Ok(doubts.Select(d => DoubtResponse.From(d)).ToList());
        });

        group.MapDelete("/{id:guid}", async (Guid id, IDoubtRepository doubtRepo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            bool deleted = await doubtRepo.DeleteDoubtAsync(Owner(user), id, ct);
            return deleted ? Results.NoContent() : Results.NotFound("Dúvida não encontrada ou não pertence ao usuário.");
        });

        // ---- Contrato de pesquisa durável (M09/M12) ----

        // 202 = job aceito e persistido; NÃO significa pesquisa concluída.
        group.MapPost("/{id:guid}/research-jobs", async (Guid id, HttpRequest http, ResearchService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            string? key = http.Headers["Idempotency-Key"].FirstOrDefault();
            MapResult<ResearchJob> result = await service.EnqueueAsync(Owner(user), id, key, ct);
            return result.IsSuccess
                ? Results.Accepted($"/research-jobs/{result.Value!.Id}", ResearchJobResponse.From(result.Value))
                : BoardEndpoints.ToProblem(result);
        });

        // Dúvida livre do mapa (AC09): sem âncora, origem BoardQuestion.
        app.MapPost("/notes/{noteId:guid}/doubts", async (Guid noteId, CreateBoardDoubtRequest request, ResearchService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await service.CreateBoardDoubtAsync(Owner(user), noteId, request.Question, ct);
            if (!result.IsSuccess)
            {
                return BoardEndpoints.ToProblem(result);
            }

            (Doubt doubt, ResearchJob? job) = result.Value;
            return Results.Created($"/doubts/{doubt.Id}", DoubtResponse.From(doubt, job));
        }).RequireAuthorization();

        var jobs = app.MapGroup("/research-jobs").RequireAuthorization();

        jobs.MapGet("/{id:guid}", async (Guid id, ResearchService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            ResearchJob? job = await service.GetJobAsync(Owner(user), id, ct);
            return job is null ? Results.NotFound() : Results.Ok(ResearchJobResponse.From(job));
        });

        jobs.MapPost("/{id:guid}/cancel", async (Guid id, ResearchService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            MapResult<ResearchJob> result = await service.CancelAsync(Owner(user), id, ct);
            return result.IsSuccess ? Results.Ok(ResearchJobResponse.From(result.Value!)) : BoardEndpoints.ToProblem(result);
        });
    }

    private static string Owner(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
}
