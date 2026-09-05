namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>One renderable embed. <see cref="Html"/> is already sanitised and free of <c>&lt;script&gt;</c> tags.</summary>
public sealed class EmbedItem
{
    /// <summary><c>instagram</c>, <c>threads</c>, <c>facebook-post</c> or <c>facebook-video</c>.</summary>
    public required string EndpointKey { get; init; }

    /// <summary>oEmbed <c>provider_name</c>, e.g. <c>Instagram</c>.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Sanitised markup, scripts removed. Safe to render raw.</summary>
    public required string Html { get; init; }

    /// <summary>oEmbed <c>type</c>: <c>rich</c> or <c>video</c>.</summary>
    public required string Type { get; init; }

    /// <summary>oEmbed <c>width</c>, when present.</summary>
    public int? Width { get; init; }

    /// <summary>The normalised input URL.</summary>
    public required Uri SourceUrl { get; init; }

    /// <summary>SDK script(s) the page must load for the markup to hydrate. Our URIs, never Meta's inline tag.</summary>
    public required IReadOnlyList<Uri> RequiredScripts { get; init; }
}
