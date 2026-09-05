using Microsoft.AspNetCore.Http;

namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>
/// Default <see cref="IEmbedScriptRegistry"/>. The claim state lives in <c>HttpContext.Items</c>, so every widget and the
/// <c>&lt;meta-embeds-scripts /&gt;</c> tag helper in one request share it even though the service is scoped and MVC may
/// hand out more than one instance per request. Without an HTTP context (unit tests, background rendering) the state is
/// instance-local.
/// </summary>
public sealed class EmbedScriptRegistry : IEmbedScriptRegistry
{
    private static readonly object ItemsKey = new();

    private readonly IHttpContextAccessor httpContextAccessor;
    private State? localState;

    /// <summary>Creates the registry.</summary>
    public EmbedScriptRegistry(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        this.httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public bool TryClaim(Uri scriptUri)
    {
        ArgumentNullException.ThrowIfNull(scriptUri);
        if (!scriptUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Script URIs must be absolute.", nameof(scriptUri));
        }

        var state = GetState();
        lock (state.Sync)
        {
            // Uri.Equals ignores the fragment, but the Facebook SDK carries its configuration there, so compare the full text.
            if (!state.Scripts.Add(scriptUri.AbsoluteUri))
            {
                return false;
            }

            state.ClaimOrder.Add(scriptUri);
            return true;
        }
    }

    /// <inheritdoc />
    public bool TryClaimMarker(string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);

        var state = GetState();
        lock (state.Sync)
        {
            return state.Markers.Add(marker);
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Uri> Claimed
    {
        get
        {
            var state = GetState();
            lock (state.Sync)
            {
                return state.ClaimOrder.ToArray();
            }
        }
    }

    private State GetState()
    {
        var items = httpContextAccessor.HttpContext?.Items;
        if (items is null)
        {
            return localState ??= new State();
        }

        if (items.TryGetValue(ItemsKey, out var existing) && existing is State shared)
        {
            return shared;
        }

        var created = new State();
        items[ItemsKey] = created;
        return created;
    }

    private sealed class State
    {
        public object Sync { get; } = new();

        public HashSet<string> Scripts { get; } = new(StringComparer.Ordinal);

        public List<Uri> ClaimOrder { get; } = [];

        public HashSet<string> Markers { get; } = new(StringComparer.Ordinal);
    }
}
