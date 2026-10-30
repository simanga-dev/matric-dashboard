namespace MatricDasbhoard.Application.Features.FileStorage.Dtos;

/// <summary>
/// Describes a single object listed from the object store.
/// </summary>
/// <param name="Key">The object key (path) within the bucket.</param>
/// <param name="Size">The object size in bytes.</param>
/// <param name="LastModified">When the object was last modified, if reported by the store.</param>
public record StoredObjectInfo(string Key, long Size, DateTimeOffset? LastModified);
