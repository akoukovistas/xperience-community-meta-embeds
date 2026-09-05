using System.Security.Cryptography;
using System.Text;

namespace XperienceCommunity.MetaEmbeds.Caching;

/// <summary>Identity of one cached oEmbed result.</summary>
/// <param name="EndpointKey">Endpoint key, e.g. <c>instagram</c>.</param>
/// <param name="NormalizedUrl">Cache form of the URL (see <c>IEmbedUrlMatcher.ToCacheForm</c>).</param>
/// <param name="Authenticated">True when credentials were sent. Tokenless and authenticated results are kept apart.</param>
/// <param name="GraphApiVersion">Graph API version segment used for the call.</param>
/// <param name="Variant">
/// Request parameters that change the provider's response, e.g. <c>hidecaption</c>. Empty (the default) for the plain
/// request; each distinct variant is cached separately.
/// </param>
public sealed record EmbedCacheKey(string EndpointKey, string NormalizedUrl, bool Authenticated, string GraphApiVersion, string Variant = "")
{
    /// <summary>
    /// Cache item name: <c>metaembeds|oembed|{endpoint}|{graphVersion}|{auth|anon}|{sha256(url)}[|{variant}]</c>.
    /// The URL is hashed so the name stays short and free of separator characters; the variant segment appears only
    /// when a variant is set, so names of plain requests are unchanged.
    /// </summary>
    public string ToCacheItemName()
    {
        var name = $"metaembeds|oembed|{EndpointKey.ToLowerInvariant()}|{GraphApiVersion.ToLowerInvariant()}|{(Authenticated ? "auth" : "anon")}|{Sha256Hex(NormalizedUrl)}";
        return string.IsNullOrWhiteSpace(Variant) ? name : $"{name}|{Variant.Trim().ToLowerInvariant()}";
    }

    /// <summary>Lower-case hex SHA-256 of the UTF-8 bytes of <paramref name="value"/>.</summary>
    public static string Sha256Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
