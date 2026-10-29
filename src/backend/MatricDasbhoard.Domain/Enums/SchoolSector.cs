namespace MatricDasbhoard.Domain.Enums;

/// <summary>
/// Funding sector of a school, normalized from the DBE EMIS masterlist's
/// <c>Sector</c> column.
/// </summary>
public enum SchoolSector
{
    /// <summary>A public (state) school.</summary>
    Public,

    /// <summary>An independent school.</summary>
    Independent,

    /// <summary>The masterlist has no usable value.</summary>
    Unknown,
}
