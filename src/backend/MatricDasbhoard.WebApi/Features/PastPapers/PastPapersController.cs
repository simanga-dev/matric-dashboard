using MatricDasbhoard.Application.Features.PastPapers;
using MatricDasbhoard.Application.Features.PastPapers.Dtos;
using MatricDasbhoard.WebApi.Features.PastPapers.Dtos.GetFacets;
using MatricDasbhoard.WebApi.Features.PastPapers.Dtos.ListPastPapers;
using MatricDasbhoard.WebApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace MatricDasbhoard.WebApi.Features.PastPapers;

/// <summary>
/// Public, read-only catalog of NSC past exam papers and memos.
/// </summary>
[ApiController]
[Route("api/v1/past-papers")]
[Tags("PastPapers")]
[AllowAnonymous]
public class PastPapersController(IPastPaperService pastPaperService) : ControllerBase
{
    /// <summary>
    /// Clients and shared caches may reuse a paper file for one day; published papers do not change.
    /// </summary>
    private const string FileCacheControl = "public, max-age=86400";

    /// <summary>
    /// Returns a filtered, sorted, paginated list of past papers.
    /// </summary>
    /// <param name="request">Filters, sort order, and pagination parameters.</param>
    /// <response code="200">Returns a page of past papers.</response>
    /// <response code="400">If a query parameter is invalid (e.g. pageSize above 100).</response>
    /// <response code="429">If the caller exceeds the rate limit.</response>
    /// <response code="503">If the catalog cannot be loaded from storage.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ListPastPapersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ListPastPapersResponse>> List(
        [FromQuery] ListPastPapersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pastPaperService.ListAsync(request.ToInput(), cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        return Ok(result.Value.ToResponse());
    }

    /// <summary>
    /// Returns the distinct subjects, years, sessions, languages, grades, kinds, and paper numbers
    /// present in the catalog, for filter dropdowns.
    /// </summary>
    /// <response code="200">Returns the catalog facets.</response>
    /// <response code="429">If the caller exceeds the rate limit.</response>
    /// <response code="503">If the catalog cannot be loaded from storage.</response>
    [HttpGet("facets")]
    [ProducesResponseType(typeof(PastPaperFacetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PastPaperFacetsResponse>> GetFacets(CancellationToken cancellationToken)
    {
        var result = await pastPaperService.GetFacetsAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        return Ok(result.Value.ToResponse());
    }

    /// <summary>
    /// Streams a past paper file. PDFs are served inline unless <paramref name="download"/> is true;
    /// every other file type is always served as an attachment. Supports <c>If-None-Match</c> and
    /// <c>If-Modified-Since</c>: when the caller already has a fresh copy, a 304 is returned without
    /// opening the storage stream.
    /// </summary>
    /// <param name="id">The paper identifier (32 lowercase hex characters).</param>
    /// <param name="download">When true, forces <c>Content-Disposition: attachment</c>.</param>
    /// <response code="200">Returns the file content.</response>
    /// <response code="304">If the client's cached copy is still valid.</response>
    /// <response code="400">If the identifier is malformed.</response>
    /// <response code="404">If no paper has the identifier.</response>
    /// <response code="429">If the caller exceeds the file download rate limit.</response>
    /// <response code="503">If the catalog or storage is unavailable.</response>
    [HttpGet("{id}/file")]
    [EnableRateLimiting(RateLimitPolicies.PastPaperFiles)]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "application/pdf", "application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetFile(
        string id,
        [FromQuery] bool download = false,
        CancellationToken cancellationToken = default)
    {
        // Resolve the file's metadata first (from the cached catalog, no storage read) so a 304
        // short-circuit can avoid opening the storage stream entirely.
        var infoResult = await pastPaperService.GetFileInfoAsync(id, cancellationToken);
        if (!infoResult.IsSuccess)
        {
            return ProblemFactory.Create(infoResult.Error, infoResult.ErrorType);
        }

        var info = infoResult.Value;
        var etag = BuildETag(info);

        if (etag is not null)
        {
            Response.Headers.ETag = etag;
        }

        if (info.LastModified is { } lastModified)
        {
            Response.GetTypedHeaders().LastModified = lastModified;
        }

        if (IsNotModified(info, etag))
        {
            Response.StatusCode = StatusCodes.Status304NotModified;
            Response.Headers.CacheControl = FileCacheControl;
            return new EmptyResult();
        }

        var result = await pastPaperService.OpenFileAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        var file = result.Value;

        var disposition = new ContentDispositionHeaderValue(
            !download && file.CanDisplayInline ? "inline" : "attachment");
        disposition.SetHttpFileName(file.FileName);

        Response.Headers.ContentDisposition = disposition.ToString();
        Response.Headers.CacheControl = FileCacheControl;
        if (file.ContentLength is { } contentLength)
        {
            Response.ContentLength = contentLength;
        }

        // The storage stream is not seekable, so byte ranges are not supported: every request gets the
        // full body with 200 and no Accept-Ranges header.
        return File(file.Content, file.ContentType);
    }

    /// <summary>
    /// Serves the generated first page of a PDF as a cacheable WebP image.
    /// </summary>
    [HttpGet("{id}/thumbnail")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetThumbnail(string id, CancellationToken cancellationToken)
    {
        var result = await pastPaperService.OpenThumbnailAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        Response.Headers.CacheControl = FileCacheControl;
        return File(result.Value.Content, "image/webp");
    }

    /// <summary>
    /// Builds a stable strong ETag for a paper from its opaque id and last-modified time, so the tag
    /// changes only when the stored file actually changes.
    /// </summary>
    private static string? BuildETag(PastPaperFileInfo info)
    {
        var suffix = info.LastModified is { } modified
            ? $"{modified.UtcTicks:x}"
            : "0";
        // 32 hex id + "_" + ticks => "0x-hex". Surround with quotes for the strong ETag syntax.
        return $"\"{info.Id}_{suffix}\"";
    }

    /// <summary>
    /// Determines whether the client's cached copy matches the current file, honoring
    /// <c>If-None-Match</c> (strong ETag) then <c>If-Modified-Since</c>.
    /// </summary>
    private bool IsNotModified(PastPaperFileInfo info, string? etag)
    {
        if (etag is not null)
        {
            // If-None-Match may carry a comma/whitespace separated list of ETags.
            var ifNoneMatch = string.Join(',', Request.Headers.IfNoneMatch.ToArray());
            if (ifNoneMatch.Contains(etag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (info.LastModified is { } lastModified && Request.GetTypedHeaders().IfModifiedSince is { } since)
        {
            // The requirement is "not modified since", so a file modified *at or just after* the
            // stated time is not fresh; only a strictly earlier modification is a match.
            return lastModified <= since;
        }

        return false;
    }
}
