namespace MatricDasbhoard.Domain.Enums;

/// <summary>
/// Phase or type of schooling offered, normalized from the DBE EMIS
/// masterlist's <c>Phase_PED</c> column.
/// </summary>
public enum SchoolPhase
{
    /// <summary>A primary school.</summary>
    Primary,

    /// <summary>A secondary school.</summary>
    Secondary,

    /// <summary>A combined primary and secondary school.</summary>
    Combined,

    /// <summary>An intermediate school.</summary>
    Intermediate,

    /// <summary>A special needs education school or centre.</summary>
    SpecialNeeds,

    /// <summary>A school of skills.</summary>
    Skills,

    /// <summary>Any other phase recorded by the masterlist.</summary>
    Other,
}
