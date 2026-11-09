namespace NoteLab.Domain;

/// <summary>Tipos de relação definidos no protótipo v1.2 (MAPAS_DE_IDEIAS.md §3).</summary>
public enum EdgeRelationType
{
    None,
    Related,
    Causes,
    Example,
    Contradicts,
    Depends,
    Influences,
    Custom
}

public static class EdgeRelationTypeCodes
{
    /// <summary>Código usado no contrato JSON (ex.: "related").</summary>
    public static string ToCode(this EdgeRelationType type) => type.ToString().ToLowerInvariant();

    public static bool TryParse(string? code, out EdgeRelationType type)
    {
        type = EdgeRelationType.Related;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return Enum.TryParse(code.Trim(), ignoreCase: true, out type)
               && Enum.IsDefined(type)
               && !int.TryParse(code, out _);
    }
}
