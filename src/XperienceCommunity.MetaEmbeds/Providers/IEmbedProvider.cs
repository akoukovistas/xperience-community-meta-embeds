namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>
/// Resolves an <see cref="EmbedRequest"/> into renderable output. Register additional implementations via DI to add
/// source types; replace the built-in one to substitute it. Providers may throw for truly unexpected conditions;
/// <see cref="IEmbedResolver"/> catches and maps those to <see cref="EmbedFailureKind.Internal"/>.
/// </summary>
public interface IEmbedProvider
{
    /// <summary>Stable provider name, e.g. <c>meta-oembed</c>.</summary>
    string Name { get; }

    /// <summary>Pure check on source type and input shape. No I/O.</summary>
    bool Supports(EmbedRequest request);

    /// <summary>Resolves the request. Should return a failed <see cref="EmbedResult"/> rather than throw for expected failures.</summary>
    Task<EmbedResult> ResolveAsync(EmbedRequest request, CancellationToken cancellationToken);
}
