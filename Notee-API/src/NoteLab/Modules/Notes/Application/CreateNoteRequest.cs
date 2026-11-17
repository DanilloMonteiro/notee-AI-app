namespace NoteLab.Application;

/// <summary>Categoria e tags são opcionais: permitem criar a nota já com a taxonomia aceita (ADR-044).</summary>
public sealed record CreateNoteRequest(string Title, string? Content, Guid? CategoryId = null, List<Guid>? TagIds = null);
