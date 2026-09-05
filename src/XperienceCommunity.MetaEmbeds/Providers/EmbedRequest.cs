namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>
/// What the editor asked for. A future feed provider reuses this with <c>SourceType = "feed"</c> and a handle or id as input.
/// </summary>
public sealed class EmbedRequest
{
    /// <summary>Source type, see <see cref="EmbedSourceTypes"/>. Always pass it through <see cref="EmbedSourceTypes.Normalize"/>.</summary>
    public required string SourceType { get; init; }

    /// <summary>Raw string from the widget. A URL for posts.</summary>
    public required string Input { get; init; }

    /// <summary>Page language, for future locale-aware SDKs. Optional.</summary>
    public string? Culture { get; init; }
}
