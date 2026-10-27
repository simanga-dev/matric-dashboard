using MatricDasbhoard.Domain.Enums;

namespace MatricDasbhoard.Domain.Entities;

/// <summary>
/// Represents a school that participates in NSC matric examinations.
/// Performance data comes from the Department of Basic Education's annual NSC
/// school performance report; contact and classification details come from the
/// DBE EMIS masterlist.
/// </summary>
public sealed class School : BaseEntity
{
    /// <summary>
    /// Gets the province where the school is located (e.g. "EASTERN CAPE", "GAUTENG").
    /// </summary>
    public string Province { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the education district or circuit name.
    /// </summary>
    public string DistrictName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the EMIS (Education Management Information System) number, a unique school identifier.
    /// </summary>
    public string EmisNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the examination centre number assigned by the Department of Basic Education.
    /// </summary>
    public string CentreNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the official name of the school or examination centre.
    /// </summary>
    public string CentreName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the school quintile (1–5), a socio-economic classification used by the DBE for funding allocation.
    /// Quintile 1 is the poorest, Quintile 5 is the wealthiest. Null when no source publishes a
    /// numeric quintile for the school.
    /// </summary>
    public int? Quintile { get; private set; }

    /// <summary>
    /// Gets the DBE EMIS masterlist's quintile code unchanged (e.g. "Q3", "N/A", "99"),
    /// or null when the masterlist has no value. Keeps "not applicable" distinguishable
    /// from "not published".
    /// </summary>
    public string? QuintileCode { get; private set; }

    /// <summary>
    /// Gets the school email address, or null when the masterlist does not provide one.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Gets the school telephone number in E.164 form (e.g. "+27475770029"), or null when unusable.
    /// </summary>
    public string? Telephone { get; private set; }

    /// <summary>
    /// Gets the normalized ownership of the land the school stands on.
    /// </summary>
    public OwnershipType? LandOwnership { get; private set; }

    /// <summary>
    /// Gets the masterlist's land ownership value unchanged, kept for auditability.
    /// </summary>
    public string? LandOwnershipRaw { get; private set; }

    /// <summary>
    /// Gets the normalized ownership of the school buildings.
    /// </summary>
    public OwnershipType? BuildingOwnership { get; private set; }

    /// <summary>
    /// Gets the masterlist's building ownership value unchanged, kept for auditability.
    /// </summary>
    public string? BuildingOwnershipRaw { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the school is a Section 21 school, or null when unknown.
    /// Section 21 schools manage their own allocated funds.
    /// </summary>
    public bool? IsSection21 { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the school charges no fees, or null when unknown.
    /// </summary>
    public bool? IsNoFeeSchool { get; private set; }

    /// <summary>
    /// Gets the urban or rural classification of the school, or null when unknown.
    /// </summary>
    public SettlementType? UrbanRural { get; private set; }

    /// <summary>
    /// Gets the school's public or independent sector, or null when unknown.
    /// </summary>
    public SchoolSector? Sector { get; private set; }

    /// <summary>
    /// Gets the phase of schooling offered, or null when unknown.
    /// </summary>
    public SchoolPhase? Phase { get; private set; }

    /// <summary>
    /// Gets the DBE EMIS masterlist data year the profile values came from, or null when never imported.
    /// </summary>
    public int? MasterlistDataYear { get; private set; }

    /// <summary>
    /// Gets the yearly performance records for this school.
    /// </summary>
    public ICollection<SchoolPerformance> Performances { get; private set; } = new List<SchoolPerformance>();

    /// <summary>
    /// Gets the per-subject performance records for this school.
    /// </summary>
    public ICollection<SchoolSubjectPerformance> SubjectPerformances { get; private set; } = new List<SchoolSubjectPerformance>();

    /// <summary>
    /// Gets the yearly learner and educator counts for this school.
    /// </summary>
    public ICollection<SchoolEnrolment> Enrolments { get; private set; } = new List<SchoolEnrolment>();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private School()
    {
    }

    /// <summary>
    /// Creates a new <see cref="School"/> instance with the specified details.
    /// </summary>
    public School(
        string province,
        string districtName,
        string emisNumber,
        string centreNumber,
        string centreName,
        int? quintile)
    {
        Province = province;
        DistrictName = districtName;
        EmisNumber = emisNumber;
        CentreNumber = centreNumber;
        CentreName = centreName;
        Quintile = quintile;
    }

    /// <summary>
    /// Adds a yearly performance record to this school.
    /// </summary>
    public void AddPerformance(SchoolPerformance performance)
    {
        Performances.Add(performance);
    }

    /// <summary>
    /// Adds a per-subject performance record to this school.
    /// </summary>
    public void AddSubjectPerformance(SchoolSubjectPerformance performance)
    {
        SubjectPerformances.Add(performance);
        performance.SetSchool(this);
    }

    /// <summary>
    /// Adds a yearly enrolment record to this school.
    /// </summary>
    /// <param name="enrolment">The enrolment record to add.</param>
    public void AddEnrolment(SchoolEnrolment enrolment)
    {
        Enrolments.Add(enrolment);
        enrolment.SetSchool(this);
    }

    /// <summary>
    /// Applies contact and classification details from the DBE EMIS masterlist.
    /// </summary>
    /// <param name="profile">The masterlist values for this school.</param>
    public void ApplyMasterlistProfile(SchoolMasterlistProfile profile)
    {
        Email = profile.Email;
        Telephone = profile.Telephone;
        LandOwnership = profile.LandOwnership;
        LandOwnershipRaw = profile.LandOwnershipRaw;
        BuildingOwnership = profile.BuildingOwnership;
        BuildingOwnershipRaw = profile.BuildingOwnershipRaw;
        IsSection21 = profile.IsSection21;
        IsNoFeeSchool = profile.IsNoFeeSchool;
        UrbanRural = profile.UrbanRural;
        Sector = profile.Sector;
        Phase = profile.Phase;
        // The masterlist only wins when it publishes a numeric quintile; schools
        // matched from the NSC report keep the report's value otherwise.
        Quintile ??= profile.Quintile;
        QuintileCode = profile.QuintileCode;
        MasterlistDataYear = profile.DataYear;
    }
}
