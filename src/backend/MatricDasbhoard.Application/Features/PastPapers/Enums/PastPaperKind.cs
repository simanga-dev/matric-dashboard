namespace MatricDasbhoard.Application.Features.PastPapers.Enums;

/// <summary>
/// The type of document a past paper file contains.
/// </summary>
public enum PastPaperKind
{
    /// <summary>The question paper written by candidates.</summary>
    QuestionPaper,

    /// <summary>The memorandum or marking guideline for a question paper.</summary>
    Memo,

    /// <summary>A separate answer book or answer sheet.</summary>
    AnswerBook,

    /// <summary>An addendum, annexure, or source booklet that accompanies a question paper.</summary>
    Addendum,

    /// <summary>Any other supporting file (data files, learner files, transcriptions).</summary>
    Other
}
