using MatricDasbhoard.Application.Features.PastPapers.Dtos;
using MatricDasbhoard.WebApi.Features.PastPapers.Dtos;
using MatricDasbhoard.WebApi.Features.PastPapers.Dtos.GetFacets;
using MatricDasbhoard.WebApi.Features.PastPapers.Dtos.ListPastPapers;

namespace MatricDasbhoard.WebApi.Features.PastPapers;

/// <summary>
/// Maps past paper DTOs between application and WebApi layers.
/// </summary>
internal static class PastPapersMapper
{
    /// <summary>
    /// Maps a list request to the application input.
    /// </summary>
    public static ListPastPapersInput ToInput(this ListPastPapersRequest request) => new(
        Search: request.Search,
        Subject: request.Subject,
        Year: request.Year,
        Session: request.Session,
        Grade: request.Grade,
        Language: request.Language,
        PaperNumber: request.PaperNumber,
        Kind: request.Kind,
        Sort: request.Sort,
        PageNumber: request.PageNumber,
        PageSize: request.PageSize);

    /// <summary>
    /// Maps a page of papers to the paginated response.
    /// </summary>
    public static ListPastPapersResponse ToResponse(this PastPaperListOutput output) => new()
    {
        Items = output.Items.Select(p => p.ToResponse()).ToList(),
        TotalCount = output.TotalCount,
        PageNumber = output.PageNumber,
        PageSize = output.PageSize
    };

    /// <summary>
    /// Maps a single paper.
    /// </summary>
    public static PastPaperResponse ToResponse(this PastPaperOutput output) => new()
    {
        Id = output.Id,
        Title = output.Title,
        Subject = output.Subject,
        Year = output.Year,
        Session = output.Session,
        Grade = output.Grade,
        PaperNumber = output.PaperNumber,
        Kind = output.Kind,
        Language = output.Language,
        Variant = output.Variant,
        FileType = output.FileType,
        SizeBytes = output.SizeBytes,
        MemoId = output.MemoId
    };

    /// <summary>
    /// Maps catalog facets.
    /// </summary>
    public static PastPaperFacetsResponse ToResponse(this PastPaperFacetsOutput output) => new()
    {
        Subjects = output.Subjects,
        Years = output.Years,
        Sessions = output.Sessions,
        Languages = output.Languages,
        Grades = output.Grades,
        Kinds = output.Kinds,
        PaperNumbers = output.PaperNumbers
    };
}
