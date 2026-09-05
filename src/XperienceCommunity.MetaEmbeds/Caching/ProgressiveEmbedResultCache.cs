using CMS.Helpers;

using XperienceCommunity.MetaEmbeds.Providers;

namespace XperienceCommunity.MetaEmbeds.Caching;

/// <summary>
/// Default <see cref="IEmbedResultCache"/> over <see cref="IProgressiveCache"/>. Successes and failures share one cache
/// item; the delegate sets <see cref="CacheSettings.CacheMinutes"/> from the policy after the factory has run, so a
/// "not found" lives an hour and a network blip two minutes while a success lives twelve hours (defaults). Results whose
/// policy duration is zero are not cached at all. Every entry depends on the dummy keys in
/// <see cref="EmbedCachePolicy.DependencyKeys"/>, so <c>CacheHelper.TouchKey("metaembeds|all")</c> purges everything.
/// </summary>
/// <remarks>
/// The factory receives <see cref="CancellationToken.None"/>: a progressive load is shared by every concurrent caller,
/// so one aborted page request must not cancel the fetch for the others. The caller's token is honoured before the load
/// starts, and the named <c>HttpClient</c>'s timeout bounds the fetch itself.
/// </remarks>
public sealed class ProgressiveEmbedResultCache : IEmbedResultCache
{
    private readonly IProgressiveCache progressiveCache;

    /// <summary>Creates the cache over Xperience's progressive cache.</summary>
    public ProgressiveEmbedResultCache(IProgressiveCache progressiveCache)
    {
        this.progressiveCache = progressiveCache ?? throw new ArgumentNullException(nameof(progressiveCache));
    }

    /// <inheritdoc />
    public Task<EmbedResult> GetOrAddAsync(
        EmbedCacheKey key,
        Func<CancellationToken, Task<EmbedResult>> factory,
        EmbedCachePolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        // The initial duration only decides whether CacheSettings.Cached starts out true; the real value is set below.
        var settings = new CacheSettings(InitialCacheMinutes(policy), key.ToCacheItemName())
        {
            AllowProgressiveCaching = true,
        };

        return progressiveCache.LoadAsync(
            async cacheSettings =>
            {
                var result = await factory(CancellationToken.None).ConfigureAwait(false)
                    ?? EmbedResult.Failed(EmbedFailureKind.Internal, "The embed factory returned null.");

                var duration = policy.DurationFor(result);
                if (duration <= TimeSpan.Zero)
                {
                    cacheSettings.Cached = false;
                }
                else
                {
                    cacheSettings.CacheMinutes = duration.TotalMinutes;
                    cacheSettings.CacheDependency = CacheHelper.GetCacheDependency(policy.DependencyKeys.ToArray());
                }

                return result;
            },
            settings);
    }

    private static double InitialCacheMinutes(EmbedCachePolicy policy)
    {
        var longest = new[] { policy.Success, policy.NotFound, policy.Transient }.Max();
        return longest > TimeSpan.Zero ? longest.TotalMinutes : 1;
    }
}
