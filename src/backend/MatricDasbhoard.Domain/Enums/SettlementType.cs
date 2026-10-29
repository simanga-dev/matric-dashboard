namespace MatricDasbhoard.Domain.Enums;

/// <summary>
/// Urban or rural classification of a school's location, normalized from the
/// DBE EMIS masterlist's <c>Urban_Rural</c> column.
/// </summary>
public enum SettlementType
{
    /// <summary>The school is in an urban area.</summary>
    Urban,

    /// <summary>The school is in a rural area.</summary>
    Rural,

    /// <summary>The masterlist records the classification as unknown.</summary>
    Unknown,

    /// <summary>The masterlist has no usable value (blank, "99", or "TO BE UPDATED").</summary>
    NotProvided,
}
