using MatricDasbhoard.Application.Features.PastPapers.Enums;
using MatricDasbhoard.Application.Features.PastPapers.Parsing;

namespace MatricDasbhoard.Unit.Tests.Application.PastPapers;

/// <summary>Covers the real-world variety of NSC past paper storage keys.</summary>
public class PastPaperKeyParserTests
{
    private const string Nov2024 = "exam_papers/2024 November NSC Examination Papers/";
    private const string MayJune2026 = "exam_papers/2026 May_June NSC_SC Examination Papers/";

    /// <summary>Expected parse for one key; null optional fields mean "expect null".</summary>
    public sealed record Expected(
        string Title,
        string Subject,
        int Year,
        PastPaperSession Session,
        int Grade,
        int? PaperNumber,
        PastPaperKind Kind,
        PastPaperLanguage? Language,
        string? Variant,
        string FileType);

    public static TheoryData<string, Expected> Cases => new()
    {
        {
            Nov2024 + "Mathematics/Mathematics P1 Nov 2024 Eng.pdf",
            new("Mathematics P1 Nov 2024 Eng", "Mathematics", 2024, PastPaperSession.November, 12, 1,
                PastPaperKind.QuestionPaper, PastPaperLanguage.English, null, "pdf")
        },
        {
            Nov2024 + "Mathematics/Mathematics P1 Nov 2024 MG Afr & Eng.pdf",
            new("Mathematics P1 Nov 2024 MG Afr & Eng", "Mathematics", 2024, PastPaperSession.November, 12, 1,
                PastPaperKind.Memo, PastPaperLanguage.EnglishAndAfrikaans, null, "pdf")
        },
        {
            Nov2024 + "Mathematics/Mathematics P2 Nov 2024 Answer Book Afr & Eng.pdf",
            new("Mathematics P2 Nov 2024 Answer Book Afr & Eng", "Mathematics", 2024, PastPaperSession.November, 12, 2,
                PastPaperKind.AnswerBook, PastPaperLanguage.EnglishAndAfrikaans, null, "pdf")
        },
        {
            MayJune2026 + "Business Studies/Business Studies P1 May-June 2026 Afr.pdf",
            new("Business Studies P1 May-June 2026 Afr", "Business Studies", 2026, PastPaperSession.MayJune, 12, 1,
                PastPaperKind.QuestionPaper, PastPaperLanguage.Afrikaans, null, "pdf")
        },
        {
            MayJune2026 + "Civil Technology/Civil Technology May-June 2026 (Woodworking) MG Eng.pdf",
            new("Civil Technology May-June 2026 (Woodworking) MG Eng", "Civil Technology", 2026, PastPaperSession.MayJune,
                12, null, PastPaperKind.Memo, PastPaperLanguage.English, "Woodworking", "pdf")
        },
        {
            "exam_papers/2013 Feb_March NSC Examination Papers/Mathematics/Mathematics P2 Feb-March 2013 Memo Eng.pdf",
            new("Mathematics P2 Feb-March 2013 Memo Eng", "Mathematics", 2013, PastPaperSession.FebMarch, 12, 2,
                PastPaperKind.Memo, PastPaperLanguage.English, null, "pdf")
        },
        {
            "exam_papers/2011 Feb_March NSC Examination Papers/Afrikaans/Exams 2011 Afrikaans_Tweede_Addisionale_Taal_V1_Memorandum_Limpopo.pdf",
            new("Exams 2011 Afrikaans Tweede Addisionale Taal V1 Memorandum Limpopo", "Afrikaans", 2011,
                PastPaperSession.FebMarch, 12, 1, PastPaperKind.Memo, null, "Limpopo", "pdf")
        },
        {
            Nov2024 + "History/History P2 Nov 2024 Addendum Eng.pdf",
            new("History P2 Nov 2024 Addendum Eng", "History", 2024, PastPaperSession.November, 12, 2,
                PastPaperKind.Addendum, PastPaperLanguage.English, null, "pdf")
        },
        {
            "exam_papers/Grade 11 Common Paper (2015-2018)/Mathematics_ 2015/Mathematics P2 Grade 11 Nov 2015 Afr.pdf",
            new("Mathematics P2 Grade 11 Nov 2015 Afr", "Mathematics", 2015, PastPaperSession.CommonPaper, 11, 2,
                PastPaperKind.QuestionPaper, PastPaperLanguage.Afrikaans, null, "pdf")
        },
        {
            "exam_papers/Grade 10 Common Paper (2015-2018)/English First Additional Language_ 2016-2018/English FAL P3 Grade 10 Nov 2018 Memo.pdf",
            new("English FAL P3 Grade 10 Nov 2018 Memo", "English", 2018, PastPaperSession.CommonPaper, 10, 3,
                PastPaperKind.Memo, null, null, "pdf")
        },
        {
            "exam_papers/2018 Grade 12 Exemplars_ Technical Subjects/Mechanical Technology/Mechanical Technology Exemplar GR 12 2018 (Fitting & Machining) Memo Eng.docx",
            new("Mechanical Technology Exemplar GR 12 2018 (Fitting & Machining) Memo Eng", "Mechanical Technology", 2018,
                PastPaperSession.Exemplar, 12, null, PastPaperKind.Memo, PastPaperLanguage.English, "Fitting & Machining", "docx")
        },
        {
            MayJune2026 + "South African Sign Language (Final)/SASL HL P1 May-June 2026 (QP) (ppt) No password, Read only.pptx",
            new("SASL HL P1 May-June 2026 (QP) (ppt) No password, Read only", "South African Sign Language", 2026,
                PastPaperSession.MayJune, 12, 1, PastPaperKind.QuestionPaper, null, null, "pptx")
        },
        {
            MayJune2026 + "Computer Application Technology/CAT P1 May-June 2026 DATA.zip",
            new("CAT P1 May-June 2026 DATA", "Computer Applications Technology", 2026, PastPaperSession.MayJune, 12, 1,
                PastPaperKind.Other, null, null, "zip")
        },
        {
            MayJune2026 + "English/English FAL P1 May-June 2026 MG.docx",
            new("English FAL P1 May-June 2026 MG", "English", 2026, PastPaperSession.MayJune, 12, 1,
                PastPaperKind.Memo, null, null, "docx")
        },
        {
            "exam_papers/2016 NSC Examinations (Oct_Nov)/Business Studies/Business Studies Nov 2016 Eng.pdf",
            new("Business Studies Nov 2016 Eng", "Business Studies", 2016, PastPaperSession.November, 12, null,
                PastPaperKind.QuestionPaper, PastPaperLanguage.English, null, "pdf")
        },
        {
            "exam_papers/2013 November NSC Examination Papers/History/History P2 Feb-March 2014 Afr.pdf",
            new("History P2 Feb-March 2014 Afr", "History", 2014, PastPaperSession.FebMarch, 12, 2,
                PastPaperKind.QuestionPaper, PastPaperLanguage.Afrikaans, null, "pdf")
        },
        {
            "exam_papers/2008 November NSC Examination Papers/Religious Studies/2008pp Religion Studies P1 Eng Nov. 2008.pdf",
            new("2008pp Religion Studies P1 Eng Nov. 2008", "Religion Studies", 2008, PastPaperSession.November, 12, 1,
                PastPaperKind.QuestionPaper, PastPaperLanguage.English, null, "pdf")
        },
        {
            "exam_papers/2018 November NSC Examination Papers/Life Sciences/Life Sciences P2 Nov 2018 FINAL Memo Afr..pdf",
            new("Life Sciences P2 Nov 2018 FINAL Memo Afr", "Life Sciences", 2018, PastPaperSession.November, 12, 2,
                PastPaperKind.Memo, PastPaperLanguage.Afrikaans, null, "pdf")
        },
        {
            "exam_papers/2020 NSC and SC Exam Papers (November)/Physical Sciences/Physical Sciences P1 Nov 2020 Eng & Afr Memo.pdf",
            new("Physical Sciences P1 Nov 2020 Eng & Afr Memo", "Physical Sciences", 2020, PastPaperSession.November, 12, 1,
                PastPaperKind.Memo, PastPaperLanguage.EnglishAndAfrikaans, null, "pdf")
        },
        {
            Nov2024 + "Agricultural Management Practices/Agricultural Management Practices Nov 2024 MG Afr.docx.pdf",
            new("Agricultural Management Practices Nov 2024 MG Afr", "Agricultural Management Practices", 2024,
                PastPaperSession.November, 12, null, PastPaperKind.Memo, PastPaperLanguage.Afrikaans, null, "pdf")
        },
        {
            "exam_papers/2011 November NSC Examination Papers/Computer Aplication Technology/CAT P1 Nov 2011 Afr.pdf",
            new("CAT P1 Nov 2011 Afr", "Computer Applications Technology", 2011, PastPaperSession.November, 12, 1,
                PastPaperKind.QuestionPaper, PastPaperLanguage.Afrikaans, null, "pdf")
        },
        {
            "exam_papers/2022 May_June NSC_SC Exam Papers/Mechanical Technology/Mechanical Technology May-June 2022 (Welding and Metal Work) Eng.pdf",
            new("Mechanical Technology May-June 2022 (Welding and Metal Work) Eng", "Mechanical Technology", 2022,
                PastPaperSession.MayJune, 12, null, PastPaperKind.QuestionPaper, PastPaperLanguage.English,
                "Welding & Metalwork", "pdf")
        },
        {
            "exam_papers/2011 November NSC Examination Papers/Mathematics/Mathematics Paper 3 - English.pdf",
            new("Mathematics Paper 3 - English", "Mathematics", 2011, PastPaperSession.November, 12, 3,
                PastPaperKind.QuestionPaper, PastPaperLanguage.English, null, "pdf")
        }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Parse_RealWorldKey_ExtractsMetadata(string key, Expected expected)
    {
        var paper = PastPaperKeyParser.Parse(key, 1234);

        Assert.NotNull(paper);
        Assert.Equal(expected, new Expected(paper.Title, paper.Subject, paper.Year, paper.Session, paper.Grade,
            paper.PaperNumber, paper.Kind, paper.Language, paper.Variant, paper.FileType));
        Assert.Equal(1234, paper.SizeBytes);
        Assert.Equal(key, paper.Key);
        Assert.Equal(PastPaperId.FromKey(key), paper.Id);
    }

    [Theory]
    [InlineData("exam_papers/2025 November NSC Examination Papers/Information Technology/DataNov2025.exe")]
    [InlineData("exam_papers/2025 November NSC Examination Papers/English/1 (1).jpg")]
    [InlineData("avatars/123.webp")]
    [InlineData("exam_papers/README.md")]
    [InlineData("exam_papers/2024 November NSC Examination Papers/Mathematics/nested/Mathematics P1 Nov 2024 Eng.pdf")]
    [InlineData("exam_papers/2024 November NSC Examination Papers/Mathematics/.pdf")]
    [InlineData("exam_papers/Unknown Folder/Mathematics/Mathematics P1 Nov 2024 Eng.pdf")]
    public void Parse_UnsupportedKey_ReturnsNull(string key)
    {
        Assert.Null(PastPaperKeyParser.Parse(key, 1));
    }

    [Theory]
    [InlineData("2026 May_June NSC_SC Examination Papers", PastPaperSession.MayJune)]
    [InlineData("2016 ASC Exam Papers (May_June)", PastPaperSession.MayJune)]
    [InlineData("2017 May_June SC(a) Exam Papers", PastPaperSession.MayJune)]
    [InlineData("2013 Feb_March NSC Examination Papers", PastPaperSession.FebMarch)]
    [InlineData("2018 Grade 12 NSC Supplementary Exams (Feb_March)", PastPaperSession.FebMarch)]
    [InlineData("2016 NSC Examinations (Oct_Nov)", PastPaperSession.November)]
    [InlineData("2019 NSC Examination Papers (November)", PastPaperSession.November)]
    [InlineData("2014 Grade 12 NSC Exemplars", PastPaperSession.Exemplar)]
    [InlineData("Grade 10 Common Paper (2015-2018)", PastPaperSession.CommonPaper)]
    public void ParseFolderSession_KnownFolders_MapToSession(string folder, PastPaperSession expected)
    {
        Assert.Equal(expected, PastPaperKeyParser.ParseFolderSession(folder));
    }

    [Theory]
    [InlineData("Computer Applicaitons Technology", "Computer Applications Technology")]
    [InlineData("GEOGRAPHY", "Geography")]
    [InlineData("isiZulu HL", "IsiZulu")]
    [InlineData("Physical Sciences_ 2017", "Physical Sciences")]
    [InlineData("Religion Studies (Senior Certificate)", "Religion Studies")]
    [InlineData("South African Sign Language (Preparatory)", "South African Sign Language")]
    [InlineData("Life Sciences version 1", "Life Sciences")]
    [InlineData("Engineering Graphic and Design", "Engineering Graphics and Design")]
    public void CanonicalSubject_FolderVariants_Normalize(string folder, string expected)
    {
        Assert.Equal(expected, PastPaperKeyParser.CanonicalSubject(folder));
    }

    [Fact]
    public void Parse_QuestionPaperAndMemo_ShareMatchStem()
    {
        var paper = PastPaperKeyParser.Parse(Nov2024 + "Afrikaans/Afrikaans SAL P2 Nov 2024 Gauteng.pdf", 1);
        var memo = PastPaperKeyParser.Parse(Nov2024 + "Afrikaans/Afrikaans SAL P2 Nov 2024 MG Gauteng.pdf", 1);
        var otherLevel = PastPaperKeyParser.Parse(Nov2024 + "Afrikaans/Afrikaans HL P2 Nov 2024 MG.pdf", 1);

        Assert.NotNull(paper);
        Assert.NotNull(memo);
        Assert.NotNull(otherLevel);
        Assert.Equal(paper.MatchStem, memo.MatchStem);
        Assert.NotEqual(paper.MatchStem, otherLevel.MatchStem);
        Assert.Equal("Gauteng", paper.Variant);
        Assert.Equal("sal", paper.Level);
    }
}
