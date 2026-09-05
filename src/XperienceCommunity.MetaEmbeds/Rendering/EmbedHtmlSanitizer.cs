using XperienceCommunity.MetaEmbeds.Providers.OEmbed;

namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>Default <see cref="IEmbedHtmlSanitizer"/> built on HtmlSanitizer (Ganss.Xss) with one profile per platform.</summary>
public sealed class EmbedHtmlSanitizer : IEmbedHtmlSanitizer
{
    // STUB: implemented by the "sanitizer" work item. See the plan §1.7, §9.
    /// <inheritdoc />
    public SanitizedEmbedHtml Sanitize(string rawHtml, MetaOEmbedEndpoint endpoint) => throw new NotImplementedException();
}
