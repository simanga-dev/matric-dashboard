namespace MatricDasbhoard.Domain.Entities;

/// <summary>
/// Represents a school's NSC matric performance data for a single academic year.
/// Each record captures the number of learners who wrote, achieved, and the pass percentage.
/// </summary>
public sealed class SchoolPerformance : BaseEntity
{
    /// <summary>
    /// Gets the unique identifier of the school this performance record belongs to.
    /// </summary>
    public Guid SchoolId { get; private set; }

    /// <summary>
    /// Gets the school this performance record belongs to.
    /// </summary>
    public School School { get; private set; } = null!;

    /// <summary>
    /// Gets the academic year (e.g., 2024).
    /// </summary>
    public int Year { get; private set; }

    /// <summary>
    /// Gets the number of progressed learners who wrote the NSC exams.
    /// Progressed learners are those who moved from Grade 11 to Grade 12 without meeting the promotion requirements.
    /// </summary>
    public int ProgressedNumber { get; private set; }

    /// <summary>
    /// Gets the total number of learners who wrote the NSC exams.
    /// </summary>
    public int TotalWrote { get; private set; }

    /// <summary>
    /// Gets the total number of learners who achieved (passed) the NSC exams.
    /// </summary>
    public int TotalAchieved { get; private set; }

    /// <summary>
    /// Gets the pass percentage (total achieved / total wrote * 100).
    /// </summary>
    public decimal PercentAchieved { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private SchoolPerformance()
    {
    }

    /// <summary>
    /// Creates a new <see cref="SchoolPerformance"/> instance.
    /// </summary>
    public SchoolPerformance(
        int year,
        int progressedNumber,
        int totalWrote,
        int totalAchieved,
        decimal percentAchieved)
    {
        Year = year;
        ProgressedNumber = progressedNumber;
        TotalWrote = totalWrote;
        TotalAchieved = totalAchieved;
        PercentAchieved = percentAchieved;
    }

    /// <summary>
    /// Associates this performance record with a school.
    /// </summary>
    internal void SetSchool(School school)
    {
        School = school;
        SchoolId = school.Id;
    }
}
