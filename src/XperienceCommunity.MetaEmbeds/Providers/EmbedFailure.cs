namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>Details of a failed resolution.</summary>
public sealed class EmbedFailure
{
    /// <summary>Failure category.</summary>
    public required EmbedFailureKind Kind { get; init; }

    /// <summary>
    /// Optional override of the message shown to editors in Page Builder. When null (the usual case) the widget shows its
    /// localised default message for <see cref="Kind"/>. Must never contain raw provider text.
    /// </summary>
    public string? EditorMessage { get; init; }

    /// <summary>Meta's <c>error_user_msg</c> / <c>message</c>, or our own diagnostic. Logged, never rendered.</summary>
    public string? ProviderMessage { get; init; }

    /// <summary>Meta's numeric <c>code</c>, when the failure came from an error payload.</summary>
    public int? ProviderCode { get; init; }

    /// <summary>Meta's numeric <c>error_subcode</c>, when present.</summary>
    public int? ProviderSubcode { get; init; }

    /// <summary>The exception that caused the failure, when there was one. Logged, never rendered.</summary>
    public Exception? Exception { get; init; }
}
