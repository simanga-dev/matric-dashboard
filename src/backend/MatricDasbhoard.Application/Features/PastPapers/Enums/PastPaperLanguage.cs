namespace MatricDasbhoard.Application.Features.PastPapers.Enums;

/// <summary>
/// The language a past paper is published in, derived from the Eng/Afr markers in its filename.
/// </summary>
public enum PastPaperLanguage
{
    /// <summary>English only.</summary>
    English,

    /// <summary>Afrikaans only.</summary>
    Afrikaans,

    /// <summary>A single bilingual document in English and Afrikaans.</summary>
    EnglishAndAfrikaans
}
