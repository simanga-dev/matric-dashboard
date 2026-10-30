using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.Application.Features.PastPapers.Parsing;

/// <summary>
/// Parses past paper storage keys of the form
/// <c>exam_papers/&lt;session folder&gt;/&lt;subject folder&gt;/&lt;filename&gt;</c> into structured metadata.
/// Pure and deterministic: no I/O, no clock, no configuration.
/// </summary>
public static partial class PastPaperKeyParser
{
    /// <summary>
    /// The storage prefix under which past papers are stored.
    /// </summary>
    public const string KeyPrefix = "exam_papers/";

    /// <summary>
    /// File extensions served by the catalog. Executables, images, and anything else are excluded
    /// (deny by default).
    /// </summary>
    private static readonly FrozenSet<string> AllowedExtensions =
        new[] { "pdf", "doc", "docx", "ppt", "pptx", "xls", "xlsx", "zip" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Canonical subject names, keyed case-insensitively so that casing differences in folder names collapse.
    /// </summary>
    private static readonly FrozenDictionary<string, string> CanonicalSubjects = new[]
    {
        "Accounting", "Afrikaans", "Agricultural Management Practices", "Agricultural Sciences",
        "Agricultural Technology", "Business Studies", "Civil Technology", "Computer Applications Technology",
        "Consumer Studies", "Dance Studies", "Design", "Dramatic Arts", "Economics", "Electrical Technology",
        "Engineering Graphics and Design", "English", "Geography", "History", "Hospitality Studies",
        "Information Technology", "IsiNdebele", "IsiXhosa", "IsiZulu", "Life Orientation", "Life Sciences",
        "Marine Sciences", "Mathematical Literacy", "Mathematics", "Mechanical Technology", "Music",
        "Physical Sciences", "Religion Studies", "Sepedi", "Sesotho", "Setswana", "Siswati",
        "South African Sign Language", "Technical Mathematics", "Technical Sciences", "Tourism", "Tshivenda",
        "Visual Arts", "Xitsonga"
    }.ToFrozenDictionary(s => s, s => s, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Known misspellings and alternate names in the published folder structure.
    /// </summary>
    private static readonly FrozenDictionary<string, string> SubjectAliases = new Dictionary<string, string>
    {
        ["Agricultural Mangement Practices"] = "Agricultural Management Practices",
        ["Computer Aplication Technology"] = "Computer Applications Technology",
        ["Computer Applicaitons Technology"] = "Computer Applications Technology",
        ["Computer Application Technology"] = "Computer Applications Technology",
        ["Engineering Graphic and Design"] = "Engineering Graphics and Design",
        ["Engineering Graphics Design"] = "Engineering Graphics and Design",
        ["English First Additional Language"] = "English",
        ["Physical Science"] = "Physical Sciences",
        ["Religious Studies"] = "Religion Studies",
        ["Relgious Studies"] = "Religion Studies"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Canonical spellings for specialisation variants that appear in parentheses.
    /// </summary>
    private static readonly FrozenDictionary<string, string> VariantAliases = new Dictionary<string, string>
    {
        ["wood working"] = "Woodworking",
        ["woodworking"] = "Woodworking",
        ["civil service"] = "Civil Services",
        ["civil services"] = "Civil Services",
        ["construction"] = "Construction",
        ["automotive"] = "Automotive",
        ["fitting and machining"] = "Fitting & Machining",
        ["fitting & machining"] = "Fitting & Machining",
        ["welding and metalwork"] = "Welding & Metalwork",
        ["welding & metalwork"] = "Welding & Metalwork",
        ["welding and metal work"] = "Welding & Metalwork",
        ["welding & metal work"] = "Welding & Metalwork",
        ["welding"] = "Welding & Metalwork",
        ["contruction"] = "Construction",
        ["civl services"] = "Civil Services",
        ["elektronics"] = "Electronics",
        ["digitaal"] = "Digital",
        ["digital"] = "Digital",
        ["digitals"] = "Digital",
        ["electronics"] = "Electronics",
        ["power system"] = "Power Systems",
        ["power systems"] = "Power Systems"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parenthesised fragments that are not variants (duplicates, formats, language and revision markers).
    /// </summary>
    private static readonly FrozenSet<string> VariantNoise =
        new[] { "qp", "ppt", "afr", "eng", "final", "memo", "mg", "r", "amended" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Provincial paper markers (English and Afrikaans spellings) mapped to the province name.
    /// Longer names come first so "Northern Cape" wins over shorter overlaps.
    /// </summary>
    private static readonly (Regex Pattern, string Province)[] Provinces =
    [
        (ProvincePattern("northern cape|noord[\\s-]*kaap"), "Northern Cape"),
        (ProvincePattern("eastern cape|oos[\\s-]*kaap"), "Eastern Cape"),
        (ProvincePattern("western cape|wes[\\s-]*kaap"), "Western Cape"),
        (ProvincePattern("north[\\s-]*west|noord[\\s-]*wes"), "North West"),
        (ProvincePattern("free state|vrystaat"), "Free State"),
        (ProvincePattern("kwa[\\s-]*zulu[\\s-]*natal|kzn"), "KwaZulu-Natal"),
        (ProvincePattern("gauteng"), "Gauteng"),
        (ProvincePattern("limpopo"), "Limpopo"),
        (ProvincePattern("mpumalanga"), "Mpumalanga")
    ];

    /// <summary>
    /// Tokens removed when building the memo matching stem: kind markers, language markers, and
    /// revision noise that differs between a question paper and its memo.
    /// </summary>
    private static readonly FrozenSet<string> StemNoise = new[]
    {
        "mg", "memo", "memos", "memorandum", "memoranda", "marking", "guideline", "guidelines", "scheme",
        "nasienriglyne", "nasienriglyn", "eng", "afr", "final", "finale", "finaal", "amended", "replace",
        "qp", "ppt", "and", "r", "finalmemo", "english", "afrikaans"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> MemoTokens = new[]
    {
        "mg", "memo", "memos", "finalmemo", "memorandum", "memoranda", "nasienriglyne", "nasienriglyn", "solutions"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> OtherTokens = new[]
    {
        "data", "transcription"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> LevelTokens = new[]
    {
        "hl", "fal", "sal"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Parses a storage key into past paper metadata.
    /// </summary>
    /// <param name="key">The storage key, e.g. <c>exam_papers/2024 November NSC Examination Papers/Mathematics/Mathematics P1 Nov 2024 Eng.pdf</c>.</param>
    /// <param name="sizeBytes">The object size in bytes.</param>
    /// <returns>
    /// The parsed metadata, or <c>null</c> when the key is outside the catalog layout, has a disallowed
    /// extension, or carries no recognisable session or year.
    /// </returns>
    public static ParsedPastPaper? Parse(string key, long sizeBytes)
    {
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = key[KeyPrefix.Length..].Split('/');
        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        var (sessionFolder, subjectFolder, fileName) = (parts[0], parts[1], parts[2]);

        var dot = fileName.LastIndexOf('.');
        if (dot <= 0 || dot == fileName.Length - 1)
        {
            return null;
        }

        var fileType = fileName[(dot + 1)..].ToLowerInvariant();
        if (!AllowedExtensions.Contains(fileType))
        {
            return null;
        }

        var title = NormalizeTitle(fileName[..dot]);
        if (title.Length == 0)
        {
            return null;
        }

        var folderSession = ParseFolderSession(sessionFolder);
        if (folderSession is null)
        {
            return null;
        }

        var (fileSession, fileYear) = FindFileSession(title);
        var session = folderSession is PastPaperSession.November or PastPaperSession.MayJune or PastPaperSession.FebMarch
                      && fileSession is not null
            ? fileSession.Value
            : folderSession.Value;

        var year = fileYear ?? FirstYear(title) ?? FirstYear(subjectFolder) ?? FirstYear(sessionFolder);
        if (year is null)
        {
            return null;
        }

        var lowerTitle = title.ToLowerInvariant();
        var tokens = Tokenize(lowerTitle);
        var subject = CanonicalSubject(subjectFolder);

        return new ParsedPastPaper(
            Key: key,
            Id: PastPaperId.FromKey(key),
            Title: title,
            Subject: subject,
            Year: year.Value,
            Session: session,
            Grade: FindGrade(title) ?? FindGrade(sessionFolder) ?? 12,
            PaperNumber: FindPaperNumber(title),
            Kind: DetectKind(lowerTitle, tokens, fileType),
            Language: DetectLanguage(tokens, subject),
            Variant: DetectVariant(title),
            FileType: fileType,
            SizeBytes: sizeBytes,
            SessionFolder: sessionFolder,
            SubjectFolder: subjectFolder,
            MatchStem: BuildStem(lowerTitle),
            Level: tokens.FirstOrDefault(LevelTokens.Contains));
    }

    /// <summary>
    /// Maps a session folder name to its examination sitting.
    /// </summary>
    /// <param name="folder">The session folder, e.g. <c>2026 May_June NSC_SC Examination Papers</c>.</param>
    /// <returns>The sitting, or <c>null</c> when the folder is not recognised.</returns>
    public static PastPaperSession? ParseFolderSession(string folder)
    {
        var lower = folder.ToLowerInvariant();

        if (lower.Contains("exemplar", StringComparison.Ordinal))
        {
            return PastPaperSession.Exemplar;
        }

        if (lower.Contains("common paper", StringComparison.Ordinal))
        {
            return PastPaperSession.CommonPaper;
        }

        if (FolderMayJunePattern().IsMatch(lower))
        {
            return PastPaperSession.MayJune;
        }

        if (FolderFebMarchPattern().IsMatch(lower))
        {
            return PastPaperSession.FebMarch;
        }

        if (lower.Contains("nov", StringComparison.Ordinal))
        {
            return PastPaperSession.November;
        }

        return null;
    }

    /// <summary>
    /// Maps a subject folder name to its canonical subject name, removing year suffixes, level
    /// suffixes, status markers, and known misspellings.
    /// </summary>
    /// <param name="folder">The subject folder, e.g. <c>Mathematics_ 2015</c> or <c>Computer Aplication Technology</c>.</param>
    /// <returns>The canonical subject name.</returns>
    public static string CanonicalSubject(string folder)
    {
        var name = CollapseWhitespace(folder.Replace('_', ' '));
        name = SubjectYearSuffixPattern().Replace(name, string.Empty);
        name = SubjectStatusPattern().Replace(name, " ");
        name = SubjectLevelSuffixPattern().Replace(name, string.Empty);
        name = CollapseWhitespace(name);

        if (SubjectAliases.TryGetValue(name, out var alias))
        {
            return alias;
        }

        return CanonicalSubjects.TryGetValue(name, out var canonical) ? canonical : name;
    }

    private static string NormalizeTitle(string baseName)
    {
        // Some uploads carry a doubled extension such as "MG Afr.docx.pdf".
        var withoutInnerExtension = InnerExtensionPattern().Replace(baseName, string.Empty);
        return CollapseWhitespace(withoutInnerExtension.Replace('_', ' ')).TrimEnd('.', ' ', '-');
    }

    private static string CollapseWhitespace(string value) =>
        WhitespacePattern().Replace(value, " ").Trim();

    private static string[] Tokenize(string lower) =>
        TokenSplitPattern().Split(lower).Where(t => t.Length > 0).ToArray();

    private static (PastPaperSession? Session, int? Year) FindFileSession(string title)
    {
        var match = FileSessionPattern().Match(title);
        if (!match.Success)
        {
            return (null, null);
        }

        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        PastPaperSession session = match.Groups["nov"].Success
            ? PastPaperSession.November
            : match.Groups["mayjune"].Success
                ? PastPaperSession.MayJune
                : PastPaperSession.FebMarch;

        return (session, year);
    }

    private static int? FirstYear(string value)
    {
        var match = YearPattern().Match(value);
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : null;
    }

    private static int? FindGrade(string value)
    {
        var match = GradePattern().Match(value);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static int? FindPaperNumber(string title)
    {
        var match = PaperNumberPattern().Match(title);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static PastPaperKind DetectKind(string lowerTitle, string[] tokens, string fileType)
    {
        if (tokens.Any(OtherTokens.Contains) || lowerTitle.Contains("learner files", StringComparison.Ordinal))
        {
            return PastPaperKind.Other;
        }

        if (AnswerBookPattern().IsMatch(lowerTitle))
        {
            return PastPaperKind.AnswerBook;
        }

        if (AddendumPattern().IsMatch(lowerTitle))
        {
            return PastPaperKind.Addendum;
        }

        if (tokens.Any(MemoTokens.Contains) || MarkingPattern().IsMatch(lowerTitle))
        {
            return PastPaperKind.Memo;
        }

        return fileType == "zip" ? PastPaperKind.Other : PastPaperKind.QuestionPaper;
    }

    /// <summary>
    /// Detects the Eng/Afr markers. Full words ("English", "Afrikaans") only count for non-language
    /// subjects, because in English and Afrikaans papers they name the subject itself.
    /// </summary>
    private static PastPaperLanguage? DetectLanguage(string[] tokens, string subject)
    {
        var english = tokens.Contains("eng") || (subject != "English" && tokens.Contains("english"));
        var afrikaans = tokens.Contains("afr") || (subject != "Afrikaans" && tokens.Contains("afrikaans"));

        return (english, afrikaans) switch
        {
            (true, true) => PastPaperLanguage.EnglishAndAfrikaans,
            (true, false) => PastPaperLanguage.English,
            (false, true) => PastPaperLanguage.Afrikaans,
            _ => null
        };
    }

    private static string? DetectVariant(string title)
    {
        foreach (Match match in ParenthesisedPattern().Matches(title))
        {
            var content = CollapseWhitespace(match.Groups[1].Value);
            if (content.Count(char.IsLetter) < 3
                || VariantNoise.Contains(content)
                || content.Contains("password", StringComparison.OrdinalIgnoreCase)
                || content.Contains("read only", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (VariantAliases.TryGetValue(content, out var canonical))
            {
                return canonical;
            }

            return FindProvince(content) ?? content;
        }

        var version = VersionPattern().Match(title);
        if (version.Success)
        {
            return $"Version {version.Groups[1].Value}";
        }

        return FindProvince(title);
    }

    private static string? FindProvince(string value)
    {
        foreach (var (pattern, province) in Provinces)
        {
            if (pattern.IsMatch(value))
            {
                return province;
            }
        }

        return null;
    }

    private static string BuildStem(string lowerTitle)
    {
        var withoutDuplicateMarkers = DuplicateMarkerPattern().Replace(lowerTitle, " ");
        var tokens = Tokenize(withoutDuplicateMarkers)
            .Where(t => !StemNoise.Contains(t))
            .Order(StringComparer.Ordinal);
        return string.Join(' ', tokens);
    }

    private static Regex ProvincePattern(string alternatives) =>
        new($@"\b(?:{alternatives})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Matches a session marker immediately followed by a year, e.g. "Nov 2024", "May-June 2026", "Feb-March 2014".</summary>
    [GeneratedRegex(
        @"\b(?:(?<nov>(?:oct(?:ober)?[\s\-/]*)?nov(?:ember)?)|(?<mayjune>may[\s\-/]*june?|mayjune|june?|july)|(?<febmarch>feb(?:ruary)?[\s\-/]*mar(?:ch)?|febmarch|feb(?:ruary)?|march|maart|mar))\.?\s*(?<year>20\d{2})(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileSessionPattern();

    [GeneratedRegex(@"may[\s_\-/]*june|mayjune", RegexOptions.CultureInvariant)]
    private static partial Regex FolderMayJunePattern();

    [GeneratedRegex(@"feb[\s_\-/]*mar|february|supplementary", RegexOptions.CultureInvariant)]
    private static partial Regex FolderFebMarchPattern();

    [GeneratedRegex(@"(?<!\d)20\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex YearPattern();

    [GeneratedRegex(@"\b(?:grade|gr)\.?\s*(1[0-2])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GradePattern();

    [GeneratedRegex(@"\b(?:p|paper|v|vraestel)\s?([1-4])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PaperNumberPattern();

    [GeneratedRegex(@"\bversion\s*(\d)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"answer\s*book|answer\s*sheet|antwoord\s*boek|antwoordblad", RegexOptions.CultureInvariant)]
    private static partial Regex AnswerBookPattern();

    [GeneratedRegex(@"\b(?:addend(?:um|a)|annexure|bylae)\b", RegexOptions.CultureInvariant)]
    private static partial Regex AddendumPattern();

    [GeneratedRegex(@"\bmarking\s+(?:guideline|scheme)s?\b|\brubrics?\b", RegexOptions.CultureInvariant)]
    private static partial Regex MarkingPattern();

    [GeneratedRegex(@"\(([^()]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex ParenthesisedPattern();

    [GeneratedRegex(@"\(\s*\d+\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex DuplicateMarkerPattern();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenSplitPattern();

    [GeneratedRegex(@"\.(?:pdf|docx?|pptx?|xlsx?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InnerExtensionPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"\s+\d{4}(?:\s*-\s*\d{4})?$", RegexOptions.CultureInvariant)]
    private static partial Regex SubjectYearSuffixPattern();

    [GeneratedRegex(@"\s*\((?:final|preparatory|senior certificate)\)\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubjectStatusPattern();

    [GeneratedRegex(@"\s+(?:hl|version\s*\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubjectLevelSuffixPattern();
}
