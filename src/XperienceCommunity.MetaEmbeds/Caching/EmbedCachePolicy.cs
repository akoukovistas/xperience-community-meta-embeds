using XperienceCommunity.MetaEmbeds.Providers;

namespace XperienceCommunity.MetaEmbeds.Caching;

/// <summary>How long each kind of result lives, and which dummy keys every entry depends on.</summary>
/// <param name="Success">Lifetime of a successful result.</param>
/// <param name="NotFound">Lifetime of <see cref="EmbedFailureKind.NotFound"/> and <see cref="EmbedFailureKind.RejectedByProvider"/> results.</param>
/// <param name="Transient">Lifetime of <see cref="EmbedFailureKind.Transient"/> results (and any other failure kind).</param>
/// <param name="DependencyKeys">Dummy cache keys, e.g. <c>metaembeds|all</c> and <c>metaembeds|endpoint|instagram</c>.</param>
public sealed record EmbedCachePolicy(TimeSpan Success, TimeSpan NotFound, TimeSpan Transient, IReadOnlyList<string> DependencyKeys)
{
    /// <summary>Builds the policy for one endpoint from the options.</summary>
    public static EmbedCachePolicy For(MetaEmbedsOptions options, string endpointKey)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(endpointKey);
        return new EmbedCachePolicy(
            options.SuccessCacheDuration,
            options.NotFoundCacheDuration,
            options.TransientFailureCacheDuration,
            [MetaEmbedsConstants.CacheKeyAll, MetaEmbedsConstants.CacheKeyForEndpoint(endpointKey)]);
    }

    /// <summary>Lifetime to apply to a concrete result. Zero or negative means "do not cache".</summary>
    public TimeSpan DurationFor(EmbedResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Succeeded)
        {
            return Success;
        }

        return result.Failure!.Kind switch
        {
            EmbedFailureKind.NotFound or EmbedFailureKind.RejectedByProvider or EmbedFailureKind.UnsupportedInput => NotFound,
            EmbedFailureKind.NotConfigured or EmbedFailureKind.InvalidInput => TimeSpan.Zero,
            _ => Transient,
        };
    }
}
