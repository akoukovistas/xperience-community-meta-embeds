namespace XperienceCommunity.MetaEmbeds;

/// <summary>How the Meta SDK <c>&lt;script&gt;</c> tags reach the rendered page.</summary>
public enum EmbedScriptMode
{
    /// <summary>
    /// Each widget emits the SDK tag(s) it is the first to claim in the current request (default, zero-config).
    /// With widget output caching on, two cached widgets may both carry the tag; prefer <see cref="TagHelper"/> then.
    /// </summary>
    Inline = 0,

    /// <summary>Widgets only claim scripts; the layout renders them once with the <c>&lt;meta-embeds-scripts /&gt;</c> tag helper.</summary>
    TagHelper = 1,

    /// <summary>Nothing is emitted; the site loads the Meta SDKs itself.</summary>
    None = 2,
}
