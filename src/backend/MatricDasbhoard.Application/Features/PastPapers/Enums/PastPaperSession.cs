namespace MatricDasbhoard.Application.Features.PastPapers.Enums;

/// <summary>
/// The examination sitting a past paper belongs to.
/// </summary>
public enum PastPaperSession
{
    /// <summary>The main end-of-year examination (October/November).</summary>
    November,

    /// <summary>The May/June examination (NSC/SC and amended senior certificate).</summary>
    MayJune,

    /// <summary>The February/March supplementary examination.</summary>
    FebMarch,

    /// <summary>A Grade 12 exemplar paper published ahead of a curriculum change.</summary>
    Exemplar,

    /// <summary>A Grade 10 or Grade 11 national common paper.</summary>
    CommonPaper
}
