using XperienceCommunity.MetaEmbeds.Providers;

namespace XperienceCommunity.MetaEmbeds.Caching;

/// <summary>Default <see cref="IEmbedResultCache"/> over <c>CMS.Helpers.IProgressiveCache</c>.</summary>
public sealed class ProgressiveEmbedResultCache : IEmbedResultCache
{
    // STUB: implemented by the "core" work item. See the plan §7 (durations, dummy keys, CacheMinutes-in-delegate note).
    /// <inheritdoc />
    public Task<EmbedResult> GetOrAddAsync(
        EmbedCacheKey key,
        Func<CancellationToken, Task<EmbedResult>> factory,
        EmbedCachePolicy policy,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}
