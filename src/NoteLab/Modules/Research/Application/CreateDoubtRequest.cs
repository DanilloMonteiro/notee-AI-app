namespace NoteLab.Application;

public record CreateDoubtRequest(
    Guid NoteId, 
    string Question, 
    string BlockId, 
    string ExactQuote, 
    int StartIndex, 
    int EndIndex
);