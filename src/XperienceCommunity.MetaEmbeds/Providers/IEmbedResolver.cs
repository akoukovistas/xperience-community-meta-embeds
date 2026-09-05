namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>
/// The single entry point the widget (or a consumer's own component) calls. Picks the first registered
/// <see cref="IEmbedProvider"/> that supports the request. Never throws.
/// </summary>
public interface IEmbedResolver
{
    /// <summary>Resolves the request. Returns a failed <see cref="EmbedResult"/> for every error path.</summary>
    Task<EmbedResult> ResolveAsync(EmbedRequest request, CancellationToken cancellationToken);
}
