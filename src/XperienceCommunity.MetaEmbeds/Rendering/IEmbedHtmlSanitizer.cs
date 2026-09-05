using XperienceCommunity.MetaEmbeds.Providers.OEmbed;

namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>
/// Reduces Meta's oEmbed <c>html</c> to the allowlisted markup for the endpoint's sanitiser profile. Always removes
/// <c>script</c>, <c>iframe</c>, <c>object</c>, <c>embed</c>, <c>style</c> elements, <c>on*</c> attributes and
/// <c>javascript:</c> / <c>data:</c> URLs. Runs before caching, so the cache only ever holds clean HTML.
/// </summary>
public interface IEmbedHtmlSanitizer
{
    /// <summary>Sanitises raw oEmbed markup using the profile named by <paramref name="endpoint"/>.</summary>
    SanitizedEmbedHtml Sanitize(string rawHtml, MetaOEmbedEndpoint endpoint);
}
