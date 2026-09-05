namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>The one v1 <see cref="IEmbedProvider"/>: matches the URL to an endpoint, calls Meta's oEmbed API, sanitises, caches.</summary>
public sealed class MetaOEmbedProvider : IEmbedProvider
{
    // STUB: implemented by the "core" work item. See the plan §5.2, §7, §8.
    /// <inheritdoc />
    public string Name => MetaEmbedsConstants.OEmbedProviderName;

    /// <inheritdoc />
    public bool Supports(EmbedRequest request) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<EmbedResult> ResolveAsync(EmbedRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
