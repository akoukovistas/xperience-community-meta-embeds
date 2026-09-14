namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>Known values of <see cref="EmbedRequest.SourceType"/>.</summary>
public static class EmbedSourceTypes
{
    /// <summary>A single post, reel or video identified by its public URL. The only value in v1.</summary>
    public const string Post = "post";

    /// <summary>
    /// Null, empty and whitespace resolve to <see cref="Post"/>; anything else is trimmed and lower-cased.
    /// Callers decide what to do with unknown values (the resolver falls back to <see cref="Post"/> and logs).
    /// </summary>
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Post : value.Trim().ToLowerInvariant();
}
