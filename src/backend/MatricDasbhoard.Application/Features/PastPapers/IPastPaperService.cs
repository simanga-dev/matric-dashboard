using MatricDasbhoard.Application.Features.PastPapers.Dtos;
using MatricDasbhoard.Shared;

namespace MatricDasbhoard.Application.Features.PastPapers;

/// <summary>
/// Read-only catalog of NSC past exam papers held in object storage.
/// </summary>
public interface IPastPaperService
{
    /// <summary>
    /// Returns a filtered, sorted page of past papers.
    /// </summary>
    /// <param name="input">Filters, sort order, and paging.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The requested page, or a <c>ServiceUnavailable</c> failure when the catalog cannot be loaded.</returns>
    Task<Result<PastPaperListOutput>> ListAsync(ListPastPapersInput input, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the distinct filter values present in the catalog.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The facets, or a <c>ServiceUnavailable</c> failure when the catalog cannot be loaded.</returns>
    Task<Result<PastPaperFacetsOutput>> GetFacetsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Opens a read stream over a past paper file. The caller must dispose the result.
    /// </summary>
    /// <param name="id">The paper identifier (32 lowercase hex characters).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The open file, a <c>Validation</c> failure for a malformed identifier, or a <c>NotFound</c>
    /// failure when no paper has the identifier.
    /// </returns>
    Task<Result<PastPaperFileOutput>> OpenFileAsync(string id, CancellationToken cancellationToken);
}
