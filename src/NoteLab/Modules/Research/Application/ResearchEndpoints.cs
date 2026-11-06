using System.Security.Claims;
using NoteLab.Domain;

namespace NoteLab.Application;

public static class ResearchEndpoints
{
    public static void MapResearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/doubts").RequireAuthorization();

        group.MapPost("/", async (CreateDoubtRequest request, INoteRepository noteRepo, IDoubtRepository doubtRepo, ClaimsPrincipal user ,CancellationToken ct) =>
        {
            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();

            Note? note = await noteRepo.GetByIdAsync(ownerId, request.NoteId,ct);

            if (note == null) return Results.NotFound("Nota nao encontrada.");

            var anchor = new DoubtAnchor(request.BlockId, request.ExactQuote, request.StartIndex, request.EndIndex);

            Doubt doubt = await doubtRepo.AddDoubtAsync(new Doubt(ownerId, note.Id, note.Version, anchor, request.Question), ct);

            return Results.Ok(new { DoubtId = doubt.Id, Status = doubt.Status.ToString() });
        });

        group.MapGet("/{id}", async (Guid id, IDoubtRepository doubtRepo, ClaimsPrincipal user ,CancellationToken ct) =>
        {
            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();

            Doubt? doubt = await doubtRepo.GetByIdAsync(ownerId, id, ct);

            if (doubt == null) return Results.NotFound("Duvida nao encontrada.");
    
            return Results.Ok(new 
            { 
                Id = doubt.Id, 
                Question = doubt.Question, 
                Answer = doubt.Answer, 
                Status = doubt.Status.ToString() 
            });
        });

        group.MapGet("/", async (IDoubtRepository doubtRepo, ClaimsPrincipal user ,CancellationToken ct) =>
        {
            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
            List<Doubt> doubts = await doubtRepo.GetAllAsync(ownerId, ct);

            if (doubts == null || !doubts.Any()) return Results.NotFound("Duvidas nao encontrada.");

            var response = doubts.Select(d => new DoubtResponse(d.Id, d.Question, d.Answer, d.Status.ToString(), d.Anchor.BlockId, d.Anchor.StartIndex, d.Anchor.EndIndex)).ToList();

            return Results.Ok(response);
        });

        group.MapGet("/note/{noteId:guid}", async (
            Guid noteId,
            IDoubtRepository doubtRepo,
            ClaimsPrincipal user,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (ownerId is null)
                return Results.Unauthorized();

            logger.LogInformation(
                "Buscando dúvidas da nota {NoteId}",
                noteId
            );

            var doubts = await doubtRepo.GetByNoteIdAsync(
                ownerId, noteId, ct
            );

            logger.LogInformation(
                "Encontradas {Count} dúvidas",
                doubts.Count
            );

            var response = doubts.Select(d => new DoubtResponse(
                d.Id,
                d.Question,
                d.Answer,
                d.Status.ToString(),
                d.Anchor.BlockId,
                d.Anchor.StartIndex,
                d.Anchor.EndIndex
            )).ToList();

            return Results.Ok(response);
        });
    }
}