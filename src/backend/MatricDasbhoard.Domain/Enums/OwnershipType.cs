namespace MatricDasbhoard.Domain.Enums;

/// <summary>
/// Ownership of a school's land or buildings, normalized from the DBE EMIS
/// masterlist's <c>OwnerLand</c> and <c>OwnerBuild</c> columns.
/// </summary>
public enum OwnershipType
{
    /// <summary>Owned by the state (masterlist values such as "GOVERNMENT", "Govt", "PUBLIC", "STATE").</summary>
    Government,

    /// <summary>Owned by a private individual or company.</summary>
    Private,

    /// <summary>Owned by an independent school body.</summary>
    Independent,

    /// <summary>The masterlist records ownership as unknown.</summary>
    Unknown,

    /// <summary>The masterlist has no usable value (blank or the "99" sentinel).</summary>
    NotProvided,
}
