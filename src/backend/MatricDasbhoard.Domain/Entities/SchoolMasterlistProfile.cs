using MatricDasbhoard.Domain.Enums;

namespace MatricDasbhoard.Domain.Entities;

/// <summary>
/// Contact and classification details for a school, taken from a single year of
/// the DBE EMIS masterlist. Raw ownership values are kept so the normalization
/// applied by the import script stays auditable.
/// </summary>
/// <param name="Email">School email address, or null when the masterlist has none.</param>
/// <param name="Telephone">School telephone number in E.164 form, or null when unusable.</param>
/// <param name="LandOwnership">Normalized ownership of the land.</param>
/// <param name="LandOwnershipRaw">The masterlist's land ownership value, unchanged.</param>
/// <param name="BuildingOwnership">Normalized ownership of the buildings.</param>
/// <param name="BuildingOwnershipRaw">The masterlist's building ownership value, unchanged.</param>
/// <param name="IsSection21">Whether the school is a Section 21 school, null when unknown.</param>
/// <param name="IsNoFeeSchool">Whether the school charges no fees, null when unknown.</param>
/// <param name="UrbanRural">Urban or rural classification, null when unknown.</param>
/// <param name="Sector">Public or independent sector, null when unknown.</param>
/// <param name="Phase">Phase of schooling offered, null when unknown.</param>
/// <param name="Quintile">Numeric quintile (1–5), null when the masterlist publishes none.</param>
/// <param name="QuintileCode">The masterlist's raw quintile value (e.g. "Q3", "N/A"), null when absent.</param>
/// <param name="DataYear">The masterlist data year these values came from.</param>
public sealed record SchoolMasterlistProfile(
    string? Email,
    string? Telephone,
    OwnershipType? LandOwnership,
    string? LandOwnershipRaw,
    OwnershipType? BuildingOwnership,
    string? BuildingOwnershipRaw,
    bool? IsSection21,
    bool? IsNoFeeSchool,
    SettlementType? UrbanRural,
    SchoolSector? Sector,
    SchoolPhase? Phase,
    int? Quintile,
    string? QuintileCode,
    int DataYear);
