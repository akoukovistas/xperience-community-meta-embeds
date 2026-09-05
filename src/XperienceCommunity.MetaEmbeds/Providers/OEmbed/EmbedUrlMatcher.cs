using System.Diagnostics.CodeAnalysis;

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>Default <see cref="IEmbedUrlMatcher"/>.</summary>
public sealed class EmbedUrlMatcher : IEmbedUrlMatcher
{
    // STUB: implemented by the "core" work item. See the plan §5.3 / §9 for the rules.
    /// <inheritdoc />
    public bool TryParse(string? input, [NotNullWhen(true)] out Uri? normalized, [NotNullWhen(false)] out string? reason) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public string ToCacheForm(Uri normalized) => throw new NotImplementedException();
}
