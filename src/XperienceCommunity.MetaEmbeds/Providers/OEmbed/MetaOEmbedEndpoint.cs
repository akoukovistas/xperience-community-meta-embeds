using System.Text.RegularExpressions;

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>
/// One oEmbed endpoint definition: which URL shapes it accepts, where to call, which SDK the markup needs, and how
/// the sanitiser should treat the response. The four defaults live in <see cref="MetaOEmbedEndpoints"/>.
/// </summary>
/// <param name="Key">Stable key, e.g. <c>instagram</c>. Used in cache keys, CSS hooks and logs.</param>
/// <param name="DisplayName">Human name, e.g. <c>Instagram</c>.</param>
/// <param name="UrlPatterns">Anchored, case-insensitive, timeout-bounded patterns matched against the normalised URL.</param>
/// <param name="EndpointUri">Builds the oEmbed endpoint URI (without query) from the current options.</param>
/// <param name="SdkScriptUri">Builds the SDK script URI the page must load, from the current options.</param>
/// <param name="ExpectedRootSelector">CSS selector that must match after sanitising, e.g. <c>blockquote.instagram-media</c>. Fail closed if absent.</param>
/// <param name="SanitizerProfile">Sanitiser profile name: <c>instagram</c>, <c>threads</c> or <c>facebook</c>.</param>
public sealed record MetaOEmbedEndpoint(
    string Key,
    string DisplayName,
    IReadOnlyList<Regex> UrlPatterns,
    Func<MetaEmbedsOptions, Uri> EndpointUri,
    Func<MetaEmbedsOptions, Uri> SdkScriptUri,
    string ExpectedRootSelector,
    string SanitizerProfile)
{
    /// <summary>True when any pattern matches the given absolute URL.</summary>
    public bool Matches(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var text = url.AbsoluteUri;
        foreach (var pattern in UrlPatterns)
        {
            try
            {
                if (pattern.IsMatch(text))
                {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological input is treated as a non-match, never as an error.
            }
        }

        return false;
    }
}
