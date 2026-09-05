namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>Default <see cref="IMetaOEmbedEndpointRegistry"/> over a fixed, ordered list.</summary>
public sealed class MetaOEmbedEndpointRegistry : IMetaOEmbedEndpointRegistry
{
    private readonly Dictionary<string, MetaOEmbedEndpoint> byKey;

    /// <summary>Creates a registry over the given endpoints, matched in the given order.</summary>
    public MetaOEmbedEndpointRegistry(IEnumerable<MetaOEmbedEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        Endpoints = endpoints.ToList();
        byKey = Endpoints.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<MetaOEmbedEndpoint> Endpoints { get; }

    /// <inheritdoc />
    public MetaOEmbedEndpoint? Match(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return Endpoints.FirstOrDefault(endpoint => endpoint.Matches(url));
    }

    /// <inheritdoc />
    public MetaOEmbedEndpoint? Get(string key) =>
        key is not null && byKey.TryGetValue(key, out var endpoint) ? endpoint : null;
}
