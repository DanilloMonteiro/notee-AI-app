namespace NoteLab.Domain;

/// <summary>
/// Posição de uma nota dentro de um mapa. Uma nota tem no máximo uma posição por mapa
/// e posições independentes em mapas diferentes. Mover não cria revisão da nota.
/// </summary>
public sealed class BoardPlacement
{
    /// <summary>Limite de coordenadas aceito (o palco do canvas tem 6400px).</summary>
    public const double MinCoordinate = -1000;
    public const double MaxCoordinate = 7000;

    public Guid BoardId { get; private set; }
    public Guid NoteId { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public long PositionVersion { get; private set; } = 1;

    private BoardPlacement() { }

    public BoardPlacement(Guid boardId, Guid noteId, double x, double y)
    {
        if (boardId == Guid.Empty || noteId == Guid.Empty)
        {
            throw new DomainException("Board e nota são obrigatórios para posicionar um cartão.");
        }

        BoardId = boardId;
        NoteId = noteId;
        (X, Y) = Validate(x, y);
    }

    public void MoveTo(double x, double y)
    {
        (X, Y) = Validate(x, y);
        PositionVersion++;
    }

    private static (double, double) Validate(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw new DomainException("Coordenadas devem ser números finitos.");
        }

        if (x < MinCoordinate || x > MaxCoordinate || y < MinCoordinate || y > MaxCoordinate)
        {
            throw new DomainException($"Coordenadas devem estar entre {MinCoordinate} e {MaxCoordinate}.");
        }

        return (Math.Round(x, 2), Math.Round(y, 2));
    }
}
