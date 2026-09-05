namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>The ordered set of oEmbed endpoints the provider knows. Replace via DI to add or remove platforms.</summary>
public interface IMetaOEmbedEndpointRegistry
{
    /// <summary>All endpoints, in match order.</summary>
    IReadOnlyList<MetaOEmbedEndpoint> Endpoints { get; }

    /// <summary>First endpoint whose pattern matches the URL, or null. Default order: Threads, Instagram, Facebook post, Facebook video.</summary>
    MetaOEmbedEndpoint? Match(Uri url);

    /// <summary>Endpoint by key, or null.</summary>
    MetaOEmbedEndpoint? Get(string key);
}
