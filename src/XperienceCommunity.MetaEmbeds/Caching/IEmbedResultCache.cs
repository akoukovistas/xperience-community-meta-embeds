using XperienceCommunity.MetaEmbeds.Providers;

namespace XperienceCommunity.MetaEmbeds.Caching;

/// <summary>
/// Caches <see cref="EmbedResult"/>s per <see cref="EmbedCacheKey"/>. Successes and failures share a key; the policy
/// decides how long each kind lives. The default implementation wraps <c>CMS.Helpers.IProgressiveCache</c>, so
/// concurrent first renders of one URL make a single HTTP call. Replace via DI for a distributed cache.
/// </summary>
public interface IEmbedResultCache
{
    /// <summary>Returns the cached result for the key or runs <paramref name="factory"/> and caches its result per <paramref name="policy"/>.</summary>
    Task<EmbedResult> GetOrAddAsync(
        EmbedCacheKey key,
        Func<CancellationToken, Task<EmbedResult>> factory,
        EmbedCachePolicy policy,
        CancellationToken cancellationToken);
}
