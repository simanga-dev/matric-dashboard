using System.Security.Cryptography;
using System.Text;

namespace MatricDasbhoard.Application.Features.PastPapers.Parsing;

/// <summary>
/// Derives and validates opaque past paper identifiers. An identifier is the first 16 bytes of the
/// SHA-256 hash of the storage key, as lowercase hex, so keys are never exposed or accepted.
/// </summary>
public static class PastPaperId
{
    /// <summary>
    /// The exact length of a past paper identifier.
    /// </summary>
    public const int Length = 32;

    /// <summary>
    /// Computes the identifier for a storage key.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <returns>A 32 character lowercase hex identifier.</returns>
    public static string FromKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(hash.AsSpan(0, Length / 2));
    }

    /// <summary>
    /// Checks whether a value is a well-formed identifier (exactly 32 lowercase hex characters).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><c>true</c> when the value is well formed; otherwise <c>false</c>.</returns>
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != Length)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
