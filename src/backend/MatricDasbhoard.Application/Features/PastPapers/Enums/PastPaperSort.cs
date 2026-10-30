namespace MatricDasbhoard.Application.Features.PastPapers.Enums;

/// <summary>
/// Sort orders supported by the past paper listing.
/// </summary>
public enum PastPaperSort
{
    /// <summary>Most recent year first, then November, May/June, Feb/March, exemplar, common paper.</summary>
    Newest,

    /// <summary>Oldest year first, then Feb/March, May/June, November, exemplar, common paper.</summary>
    Oldest,

    /// <summary>Subject name ascending, then most recent first within a subject.</summary>
    Subject
}
