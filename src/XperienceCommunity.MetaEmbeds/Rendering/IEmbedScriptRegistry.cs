namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>
/// Per-request bookkeeping of which SDK scripts (and one-off markers such as Facebook's <c>fb-root</c> div) have already
/// been emitted, so a page with several embeds loads each SDK once. Scoped per request; backed by
/// <c>HttpContext.Items</c> when an HTTP context exists.
/// </summary>
public interface IEmbedScriptRegistry
{
    /// <summary>Returns true the first time a given script URI is claimed in this request, false afterwards.</summary>
    bool TryClaim(Uri scriptUri);

    /// <summary>Returns true the first time a given marker (e.g. <c>fb-root</c>) is claimed in this request, false afterwards.</summary>
    bool TryClaimMarker(string marker);

    /// <summary>Every script URI claimed so far in this request, in claim order.</summary>
    IReadOnlyCollection<Uri> Claimed { get; }
}
