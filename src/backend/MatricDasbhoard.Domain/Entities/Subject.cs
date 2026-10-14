namespace MatricDasbhoard.Domain.Entities;

/// <summary>
/// Represents a National Senior Certificate (NSC) examination subject.
/// </summary>
public sealed class Subject : BaseEntity
{
    /// <summary>
    /// Gets the stable subject code (e.g. "accounting").
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the display name of the subject (e.g. "Accounting").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the subject is treated as a gateway subject.
    /// </summary>
    public bool IsGateway { get; private set; } = true;

    /// <summary>
    /// Gets the per-school performance records for this subject.
    /// </summary>
    public ICollection<SchoolSubjectPerformance> Performances { get; private set; } = new List<SchoolSubjectPerformance>();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Subject()
    {
    }

    /// <summary>
    /// Creates a new <see cref="Subject"/> instance.
    /// </summary>
    /// <param name="code">Stable subject code.</param>
    /// <param name="name">Display name of the subject.</param>
    /// <param name="isGateway">Whether the subject is treated as a gateway subject.</param>
    public Subject(string code, string name, bool isGateway = true)
    {
        Code = code;
        Name = name;
        IsGateway = isGateway;
    }

    /// <summary>
    /// Adds a per-school performance record to this subject.
    /// </summary>
    public void AddPerformance(SchoolSubjectPerformance performance)
    {
        Performances.Add(performance);
        performance.SetSubject(this);
    }
}
