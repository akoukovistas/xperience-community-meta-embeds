using System.Diagnostics.CodeAnalysis;

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>Validates and normalises the URL an editor typed, before any endpoint matching or network call.</summary>
public interface IEmbedUrlMatcher
{
    /// <summary>
    /// Accepts absolute http(s) URLs up to 2048 characters with a DNS host name; rejects userinfo, explicit ports,
    /// IP-address hosts and anything else. On success returns the normalised URL: lower-case host, fragment removed,
    /// path and query kept as typed.
    /// </summary>
    /// <param name="input">Raw editor input; may be null or blank.</param>
    /// <param name="normalized">The normalised absolute URL when the method returns true.</param>
    /// <param name="reason">A short diagnostic (for logs, never for editors) when the method returns false.</param>
    bool TryParse(string? input, [NotNullWhen(true)] out Uri? normalized, [NotNullWhen(false)] out string? reason);

    /// <summary>
    /// Canonical form used for cache keys: scheme forced to https, host lower-cased without a leading <c>www.</c>,
    /// query and fragment dropped, path kept with a trailing slash. <c>?igsh=…</c> variants of one post share a key.
    /// </summary>
    string ToCacheForm(Uri normalized);
}
