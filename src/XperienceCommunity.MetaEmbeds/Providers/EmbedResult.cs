namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>Outcome of resolving an <see cref="EmbedRequest"/>: either items to render or a failure.</summary>
public sealed class EmbedResult
{
    private EmbedResult(IReadOnlyList<EmbedItem> items, EmbedFailure? failure)
    {
        Items = items;
        Failure = failure;
    }

    /// <summary>True when <see cref="Items"/> is populated and <see cref="Failure"/> is null.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>Exactly one item for posts; N for a future feed. Empty on failure.</summary>
    public IReadOnlyList<EmbedItem> Items { get; }

    /// <summary>Why resolution failed; null on success.</summary>
    public EmbedFailure? Failure { get; }

    /// <summary>Creates a successful result.</summary>
    public static EmbedResult Success(IReadOnlyList<EmbedItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new EmbedResult(items, null);
    }

    /// <summary>Creates a successful result with a single item.</summary>
    public static EmbedResult Success(EmbedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new EmbedResult([item], null);
    }

    /// <summary>Creates a failed result.</summary>
    public static EmbedResult Failed(EmbedFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new EmbedResult([], failure);
    }

    /// <summary>Shorthand for <see cref="Failed(EmbedFailure)"/>.</summary>
    public static EmbedResult Failed(EmbedFailureKind kind, string? providerMessage = null, Exception? exception = null) =>
        Failed(new EmbedFailure { Kind = kind, ProviderMessage = providerMessage, Exception = exception });
}
