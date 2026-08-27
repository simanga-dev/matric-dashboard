namespace MatricDasbhoard.Domain.Entities;

/// <summary>
/// Represents a school that participates in NSC matric examinations.
/// Data sourced from the Department of Basic Education's annual NSC school performance report.
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
    /// Gets the EMIS (Education Management Information System) number — a unique school identifier.
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
    /// Quintile 1 is the poorest, Quintile 5 is the wealthiest.
    /// </summary>
    public int Quintile { get; private set; }

    /// <summary>
    /// Gets the yearly performance records for this school.
    /// </summary>
    public ICollection<SchoolPerformance> Performances { get; private set; } = new List<SchoolPerformance>();

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
        int quintile)
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
}
