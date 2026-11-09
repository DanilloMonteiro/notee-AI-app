namespace NoteLab.Application;

public record DoubtResponse(
    Guid Id,
    string Question,
    string? Answer,
    string Status,
    string BlockId,
    string ExactQuote,
    int StartIndex,
    int EndIndex
);