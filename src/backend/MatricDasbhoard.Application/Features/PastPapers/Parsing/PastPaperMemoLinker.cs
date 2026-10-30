using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.Application.Features.PastPapers.Parsing;

/// <summary>
/// Pairs question papers with their memos. Pure and deterministic.
/// </summary>
/// <remarks>
/// A memo is only considered when it sits in the same session folder and subject folder as the
/// question paper. Candidates are first matched on the filename stem (the filename without kind,
/// language, and revision markers), which also keeps language levels (HL/FAL/SAL), provincial
/// papers, and versions apart. When no stem match exists, a structured match on year, grade, paper
/// number, variant, and language level is used. Among candidates the most language-compatible memo
/// wins: same language, then bilingual, then unmarked. English and Afrikaans never pair with each other.
/// </remarks>
public static class PastPaperMemoLinker
{
    /// <summary>
    /// Computes the memo for each question paper that has one.
    /// </summary>
    /// <param name="papers">All parsed papers in the catalog.</param>
    /// <returns>A map from question paper id to memo id.</returns>
    public static IReadOnlyDictionary<string, string> Link(IReadOnlyList<ParsedPastPaper> papers)
    {
        var memos = papers.Where(p => p.Kind == PastPaperKind.Memo).ToList();

        var memosByStem = memos
            .GroupBy(m => (m.SessionFolder, m.SubjectFolder, m.MatchStem))
            .ToDictionary(g => g.Key, g => g.ToList());

        var memosByFields = memos
            .GroupBy(FieldKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        var links = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var paper in papers.Where(p => p.Kind == PastPaperKind.QuestionPaper))
        {
            var memo = memosByStem.TryGetValue((paper.SessionFolder, paper.SubjectFolder, paper.MatchStem), out var byStem)
                ? BestMatch(paper, byStem)
                : null;

            if (memo is null && memosByFields.TryGetValue(FieldKey(paper), out var byFields))
            {
                memo = BestMatch(paper, byFields);
            }

            if (memo is not null)
            {
                links[paper.Id] = memo.Id;
            }
        }

        return links;
    }

    private static (string SessionFolder, string SubjectFolder, int Year, int Grade, int? PaperNumber, string? Variant, string? Level)
        FieldKey(ParsedPastPaper paper) =>
        (paper.SessionFolder, paper.SubjectFolder, paper.Year, paper.Grade, paper.PaperNumber, paper.Variant, paper.Level);

    private static ParsedPastPaper? BestMatch(ParsedPastPaper paper, List<ParsedPastPaper> candidates) =>
        candidates
            .Select(memo => (Memo: memo, Score: LanguageScore(paper.Language, memo.Language)))
            .Where(c => c.Score > 0)
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Memo.FileType == "pdf" ? 0 : 1)
            .ThenBy(c => c.Memo.Id, StringComparer.Ordinal)
            .Select(c => c.Memo)
            .FirstOrDefault();

    /// <summary>
    /// Scores how well a memo's language serves a question paper's language. Zero means incompatible.
    /// </summary>
    private static int LanguageScore(PastPaperLanguage? paper, PastPaperLanguage? memo)
    {
        if (paper == memo)
        {
            return 4;
        }

        if (memo == PastPaperLanguage.EnglishAndAfrikaans)
        {
            return 3;
        }

        if (memo is null)
        {
            return 2;
        }

        // The paper is unmarked or bilingual and the memo is a single language: usable, least preferred.
        return paper is null or PastPaperLanguage.EnglishAndAfrikaans ? 1 : 0;
    }
}
