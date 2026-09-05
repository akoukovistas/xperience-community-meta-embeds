namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>Well-known keys of <see cref="EmbedRequest.Parameters"/>.</summary>
public static class EmbedRequestParameters
{
    /// <summary>
    /// <c>"true"</c> asks the provider to omit the post caption. Honoured by the Instagram oEmbed endpoint
    /// (<c>hidecaption=true</c>); ignored by the others.
    /// </summary>
    public const string HideCaption = "hidecaption";

    /// <summary>An empty, read-only parameter set.</summary>
    public static IReadOnlyDictionary<string, string> None { get; } = new Dictionary<string, string>(0);

    /// <summary>True when <paramref name="parameters"/> carries <paramref name="key"/> with a truthy value (<c>true</c>, <c>1</c>, <c>yes</c>).</summary>
    public static bool IsEnabled(IReadOnlyDictionary<string, string>? parameters, string key)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("1", StringComparison.Ordinal)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
