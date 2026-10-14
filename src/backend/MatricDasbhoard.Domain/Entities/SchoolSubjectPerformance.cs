namespace MatricDasbhoard.Domain.Entities;

/// <summary>
/// Represents a school's NSC performance in a single subject for one academic year.
/// </summary>
public sealed class SchoolSubjectPerformance : BaseEntity
{
    /// <summary>
    /// Gets the unique identifier of the school this record belongs to.
    /// </summary>
    public Guid SchoolId { get; private set; }

    /// <summary>
    /// Gets the school this record belongs to, null until the record is associated with a school.
    /// </summary>
    public School? School { get; private set; }

    /// <summary>
    /// Gets the unique identifier of the subject this record describes.
    /// </summary>
    public Guid SubjectId { get; private set; }

    /// <summary>
    /// Gets the subject this record describes, null until the record is associated with a subject.
    /// </summary>
    public Subject? Subject { get; private set; }

    /// <summary>
    /// Gets the academic year (e.g. 2024).
    /// </summary>
    public int Year { get; private set; }

    /// <summary>
    /// Gets the number of learners who wrote the subject.
    /// </summary>
    public int TotalWrote { get; private set; }

    /// <summary>
    /// Gets the number of learners who achieved (passed) the subject, null when unavailable.
    /// </summary>
    public int? Achieved { get; private set; }

    /// <summary>
    /// Gets the pass percentage for the subject, null when unavailable.
    /// </summary>
    public decimal? PercentAchieved { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private SchoolSubjectPerformance()
    {
    }

    /// <summary>
    /// Creates a new <see cref="SchoolSubjectPerformance"/> instance.
    /// </summary>
    /// <param name="year">The academic year.</param>
    /// <param name="totalWrote">Number of learners who wrote the subject.</param>
    /// <param name="achieved">Number of learners who achieved (passed) the subject, null when unavailable.</param>
    /// <param name="percentAchieved">Pass percentage for the subject, null when unavailable.</param>
    public SchoolSubjectPerformance(
        int year,
        int totalWrote,
        int? achieved,
        decimal? percentAchieved)
    {
        Year = year;
        TotalWrote = totalWrote;
        Achieved = achieved;
        PercentAchieved = percentAchieved;
    }

    /// <summary>
    /// Associates this record with a school.
    /// </summary>
    internal void SetSchool(School school)
    {
        School = school;
        SchoolId = school.Id;
    }

    /// <summary>
    /// Associates this record with a subject.
    /// </summary>
    internal void SetSubject(Subject subject)
    {
        Subject = subject;
        SubjectId = subject.Id;
    }
}
