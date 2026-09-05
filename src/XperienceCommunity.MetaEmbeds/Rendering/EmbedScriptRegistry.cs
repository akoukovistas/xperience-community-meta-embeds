namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>Default <see cref="IEmbedScriptRegistry"/>, backed by <c>HttpContext.Items</c> when a request is active.</summary>
public sealed class EmbedScriptRegistry : IEmbedScriptRegistry
{
    // STUB: implemented by the "xperience" work item. See the plan §5.4, §6.
    /// <inheritdoc />
    public bool TryClaim(Uri scriptUri) => throw new NotImplementedException();

    /// <inheritdoc />
    public bool TryClaimMarker(string marker) => throw new NotImplementedException();

    /// <inheritdoc />
    public IReadOnlyCollection<Uri> Claimed => throw new NotImplementedException();
}
