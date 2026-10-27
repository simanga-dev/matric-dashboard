namespace MatricDasbhoard.Domain.Entities;

/// <summary>
/// Represents the number of learners and educators at a school for a single year,
/// sourced from the DBE EMIS masterlist.
/// </summary>
public sealed class SchoolEnrolment : BaseEntity
{
    /// <summary>
    /// Gets the unique identifier of the school this enrolment record belongs to.
    /// </summary>
    public Guid SchoolId { get; private set; }

    /// <summary>
    /// Gets the school this enrolment record belongs to.
    /// </summary>
    public School School { get; private set; } = null!;

    /// <summary>
    /// Gets the data year the enrolment figures apply to (e.g. 2025).
    /// </summary>
    public int Year { get; private set; }

    /// <summary>
    /// Gets the number of learners enrolled, or null when the masterlist has no figure.
    /// </summary>
    public int? Learners { get; private set; }

    /// <summary>
    /// Gets the number of educators employed, or null when the masterlist has no figure.
    /// </summary>
    public int? Educators { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private SchoolEnrolment()
    {
    }

    /// <summary>
    /// Creates a new <see cref="SchoolEnrolment"/> instance.
    /// </summary>
    /// <param name="year">Data year the figures apply to.</param>
    /// <param name="learners">Number of learners enrolled.</param>
    /// <param name="educators">Number of educators employed.</param>
    public SchoolEnrolment(int year, int? learners, int? educators)
    {
        Year = year;
        Learners = learners;
        Educators = educators;
    }

    /// <summary>
    /// Updates the enrolment figures for this year.
    /// </summary>
    /// <param name="learners">Number of learners enrolled.</param>
    /// <param name="educators">Number of educators employed.</param>
    public void Update(int? learners, int? educators)
    {
        Learners = learners;
        Educators = educators;
    }

    /// <summary>
    /// Associates this enrolment record with a school.
    /// </summary>
    internal void SetSchool(School school)
    {
        School = school;
        SchoolId = school.Id;
    }
}
