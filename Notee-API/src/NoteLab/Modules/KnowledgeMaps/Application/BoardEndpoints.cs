using System.Security.Claims;

namespace NoteLab.Application;

/// <summary>
/// Rotas do módulo KnowledgeMaps (Mapas de Ideias). Seguem a convenção atual do
/// repositório (sem prefixo /api/v1). OwnerId vem sempre do token, nunca do payload.
/// </summary>
public static class BoardEndpoints
{
    public static void MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/boards").RequireAuthorization().WithTags("Boards");

        group.MapGet("/", async (BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(OwnerId(user), ct)));

        group.MapPost("/", async (CreateBoardRequest request, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            MapResult<BoardSummaryResponse> result = await service.CreateAsync(OwnerId(user), request, ct);
            return result.IsSuccess
                ? Results.Created($"/boards/{result.Value!.Id}", result.Value)
                : ToProblem(result);
        });

        group.MapGet("/{boardId:guid}", async (Guid boardId, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            BoardDetailResponse? board = await service.GetAsync(OwnerId(user), boardId, ct);
            return board is null ? NotFound("Mapa não encontrado.") : Results.Ok(board);
        });

        group.MapPatch("/{boardId:guid}", async (Guid boardId, UpdateBoardRequest request, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
            ToHttp(await service.UpdateAsync(OwnerId(user), boardId, request, ct)));

        group.MapDelete("/{boardId:guid}", async (Guid boardId, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
            await service.DeleteAsync(OwnerId(user), boardId, ct) ? Results.NoContent() : NotFound("Mapa não encontrado."));

        group.MapPut("/{boardId:guid}/placements/{noteId:guid}", async (Guid boardId, Guid noteId, MovePlacementRequest request, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
            ToHttp(await service.MovePlacementAsync(OwnerId(user), boardId, noteId, request, ct)));

        group.MapPut("/{boardId:guid}/placements", async (Guid boardId, BatchPlacementsRequest request, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
            ToHttp(await service.MovePlacementsAsync(OwnerId(user), boardId, request, ct)));

        group.MapPost("/{boardId:guid}/edges", async (Guid boardId, CreateEdgeRequest request, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            MapResult<BoardEdgeResponse> result = await service.CreateEdgeAsync(OwnerId(user), boardId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/boards/{boardId}/edges/{result.Value!.Id}", result.Value)
                : ToProblem(result);
        });

        group.MapPatch("/{boardId:guid}/edges/{edgeId:guid}", async (Guid boardId, Guid edgeId, UpdateEdgeRequest request, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
            ToHttp(await service.UpdateEdgeAsync(OwnerId(user), boardId, edgeId, request, ct)));

        group.MapDelete("/{boardId:guid}/edges/{edgeId:guid}", async (Guid boardId, Guid edgeId, BoardService service, ClaimsPrincipal user, CancellationToken ct) =>
            await service.DeleteEdgeAsync(OwnerId(user), boardId, edgeId, ct) ? Results.NoContent() : NotFound("Conexão não encontrada."));
    }

    internal static string OwnerId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();

    private static IResult ToHttp<T>(MapResult<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result);

    internal static IResult ToProblem<T>(MapResult<T> result) => result.Error switch
    {
        MapErrorKind.NotFound => NotFound(result.Message),
        MapErrorKind.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflito", detail: result.Message),
        MapErrorKind.TooManyRequests => Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Limite atingido", detail: result.Message),
        _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Erro de validação", detail: result.Message)
    };

    private static IResult NotFound(string? detail) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Não encontrado", detail: detail);
}

/// <summary>Rotas de contexto e sugestões do mapa (MAP-04/MAP-05).</summary>
public static class GraphAiEndpoints
{
    public static void MapGraphAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/boards/{boardId:guid}").RequireAuthorization().WithTags("BoardsAI");

        group.MapPost("/context-preview", async (Guid boardId, GraphScopeRequest request, GraphContextService service, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
        {
            MapResult<GraphContextResponse> result = await service.BuildAsync(BoardEndpoints.OwnerId(user), boardId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : BoardEndpoints.ToProblem(result);
        });

        group.MapGet("/suggestions", async (Guid boardId, GraphContextService service, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct, string? status = null) =>
        {
            List<GraphSuggestionResponse>? list = await service.ListAsync(BoardEndpoints.OwnerId(user), boardId, status, ct);
            return list is null ? Results.Problem(statusCode: 404, title: "Não encontrado", detail: "Mapa não encontrado.") : Results.Ok(list);
        });

        group.MapPost("/suggestions", async (Guid boardId, GraphScopeRequest request, GraphContextService service, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
        {
            MapResult<List<GraphSuggestionResponse>> result = await service.GenerateAsync(BoardEndpoints.OwnerId(user), boardId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : BoardEndpoints.ToProblem(result);
        });

        group.MapPost("/suggestions/{suggestionId:guid}/accept", async (Guid boardId, Guid suggestionId, GraphContextService service, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
        {
            MapResult<BoardEdgeResponse> result = await service.AcceptAsync(BoardEndpoints.OwnerId(user), boardId, suggestionId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : BoardEndpoints.ToProblem(result);
        });

        group.MapPost("/suggestions/{suggestionId:guid}/dismiss", async (Guid boardId, Guid suggestionId, GraphContextService service, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) =>
        {
            MapResult<bool> result = await service.DismissAsync(BoardEndpoints.OwnerId(user), boardId, suggestionId, ct);
            return result.IsSuccess ? Results.NoContent() : BoardEndpoints.ToProblem(result);
        });
    }
}
