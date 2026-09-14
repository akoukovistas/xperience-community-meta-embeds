using System.Reflection;

namespace XperienceCommunity.MetaEmbeds;

/// <summary>Well-known names used across the package.</summary>
public static class MetaEmbedsConstants
{
    /// <summary>Name of the <see cref="System.Net.Http.HttpClient"/> registered with <c>IHttpClientFactory</c>.</summary>
    public const string HttpClientName = "MetaEmbeds";

    /// <summary>Dummy cache key every cached embed depends on. Touch it to purge everything.</summary>
    public const string CacheKeyAll = "metaembeds|all";

    /// <summary>Prefix of the per-endpoint dummy cache key, followed by the endpoint key.</summary>
    public const string CacheKeyEndpointPrefix = "metaembeds|endpoint|";

    /// <summary>Prefix of every resource string key in <c>MetaEmbedsResources.resx</c>.</summary>
    public const string ResourcePrefix = "xperiencecommunity.metaembeds";

    /// <summary>Provider name of the built-in oEmbed provider.</summary>
    public const string OEmbedProviderName = "meta-oembed";

    /// <summary>Package version, read from the assembly's informational version (without build metadata).</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Product token of the User-Agent sent on every oEmbed request, paired with <see cref="Version"/>.</summary>
    public const string ProductName = "XperienceCommunity.MetaEmbeds";

    /// <summary>Dummy cache key for one endpoint, e.g. <c>metaembeds|endpoint|instagram</c>.</summary>
    public static string CacheKeyForEndpoint(string endpointKey) => CacheKeyEndpointPrefix + endpointKey;

    private static string ReadVersion()
    {
        var informational = typeof(MetaEmbedsConstants).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return typeof(MetaEmbedsConstants).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? informational[..plus] : informational;
    }
}
