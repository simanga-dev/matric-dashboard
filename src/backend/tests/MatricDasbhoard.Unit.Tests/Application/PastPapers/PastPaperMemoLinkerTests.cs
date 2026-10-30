using MatricDasbhoard.Application.Features.PastPapers.Parsing;

namespace MatricDasbhoard.Unit.Tests.Application.PastPapers;

public class PastPaperMemoLinkerTests
{
    private const string Folder = "exam_papers/2024 November NSC Examination Papers/";

    private static ParsedPastPaper Parse(string relative) =>
        PastPaperKeyParser.Parse(Folder + relative, 1) ?? throw new InvalidOperationException(relative);

    [Fact]
    public void Link_PrefersSameLanguageMemo()
    {
        var english = Parse("Accounting/Accounting P1 Nov 2024 Eng.pdf");
        var afrikaans = Parse("Accounting/Accounting P1 Nov 2024 Afr.pdf");
        var englishMemo = Parse("Accounting/Accounting P1 Nov 2024 MG Eng.pdf");
        var afrikaansMemo = Parse("Accounting/Accounting P1 Nov 2024 MG Afr.pdf");

        var links = PastPaperMemoLinker.Link([english, afrikaans, englishMemo, afrikaansMemo]);

        Assert.Equal(englishMemo.Id, links[english.Id]);
        Assert.Equal(afrikaansMemo.Id, links[afrikaans.Id]);
        Assert.Equal(2, links.Count);
    }

    [Fact]
    public void Link_UsesBilingualMemoForEitherLanguage()
    {
        var english = Parse("Mathematics/Mathematics P1 Nov 2024 Eng.pdf");
        var afrikaans = Parse("Mathematics/Mathematics P1 Nov 2024 Afr.pdf");
        var memo = Parse("Mathematics/Mathematics P1 Nov 2024 MG Afr & Eng.pdf");

        var links = PastPaperMemoLinker.Link([english, afrikaans, memo]);

        Assert.Equal(memo.Id, links[english.Id]);
        Assert.Equal(memo.Id, links[afrikaans.Id]);
    }

    [Fact]
    public void Link_NeverPairsEnglishPaperWithAfrikaansMemo()
    {
        var english = Parse("Accounting/Accounting P1 Nov 2024 Eng.pdf");
        var afrikaansMemo = Parse("Accounting/Accounting P1 Nov 2024 MG Afr.pdf");

        var links = PastPaperMemoLinker.Link([english, afrikaansMemo]);

        Assert.Empty(links);
    }

    [Fact]
    public void Link_KeepsPaperNumbersVariantsAndLevelsApart()
    {
        var p1 = Parse("Mathematics/Mathematics P1 Nov 2024 Eng.pdf");
        var p2Memo = Parse("Mathematics/Mathematics P2 Nov 2024 MG Eng.pdf");
        var woodworking = Parse("Civil Technology/Civil Technology Nov 2024 (Woodworking) Eng.pdf");
        var constructionMemo = Parse("Civil Technology/Civil Technology Nov 2024 (Construction) MG Eng.pdf");
        var homeLanguage = Parse("IsiZulu/IsiZulu HL P1 Nov 2024.pdf");
        var firstAdditionalMemo = Parse("IsiZulu/IsiZulu FAL P1 Nov 2024 MG.pdf");

        var links = PastPaperMemoLinker.Link([p1, p2Memo, woodworking, constructionMemo, homeLanguage, firstAdditionalMemo]);

        Assert.Empty(links);
    }

    [Fact]
    public void Link_FallsBackToStructuredMatchWhenStemsDiffer()
    {
        var paper = Parse("Electrical Technology/Electrical Technology November 2024 (Digital) Eng.pdf");
        var memo = Parse("Electrical Technology/Electrical Technolgy Nov 2024 (Digital) Final MG Eng.pdf");

        var links = PastPaperMemoLinker.Link([paper, memo]);

        Assert.Equal(memo.Id, links[paper.Id]);
    }

    [Fact]
    public void Link_OnlyLinksQuestionPapers()
    {
        var answerBook = Parse("Mathematics/Mathematics P2 Nov 2024 Answer Book Afr & Eng.pdf");
        var memo = Parse("Mathematics/Mathematics P2 Nov 2024 MG Afr & Eng.pdf");

        var links = PastPaperMemoLinker.Link([answerBook, memo]);

        Assert.Empty(links);
    }
}
