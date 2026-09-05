namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>Default <see cref="IEmbedResolver"/>: normalises the source type, picks a provider, never throws.</summary>
public sealed class EmbedResolver : IEmbedResolver
{
    // STUB: implemented by the "core" work item. See the plan §5.2, §5.6 (SourceType fallback), §8.
    /// <inheritdoc />
    public Task<EmbedResult> ResolveAsync(EmbedRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
