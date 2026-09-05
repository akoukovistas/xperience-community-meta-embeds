namespace XperienceCommunity.MetaEmbeds;

/// <summary>
/// Configuration for the Meta embeds integration. Every member has a working default, so the
/// <c>XperienceCommunityMetaEmbeds</c> configuration section is optional.
/// </summary>
public sealed class MetaEmbedsOptions
{
    /// <summary>Name of the configuration section the options bind from.</summary>
    public const string SectionName = "XperienceCommunityMetaEmbeds";

    /// <summary>
    /// Optional Meta credentials. Empty (the default) means tokenless oEmbed calls. Any populated member switches
    /// the same calls to the authenticated route by appending an <c>access_token</c> parameter.
    /// </summary>
    public MetaCredentialsOptions Credentials { get; set; } = new();

    /// <summary>
    /// Graph API version segment used for graph.facebook.com endpoints (Instagram, Facebook) and in the Facebook SDK
    /// URL. Default <c>v25.0</c>, the version Meta's own WordPress plugin pins.
    /// </summary>
    public string GraphApiVersion { get; set; } = "v25.0";

    /// <summary>
    /// Locale segment of the Facebook SDK URL (<c>connect.facebook.net/{locale}/sdk.js</c>). Default <c>en_US</c>.
    /// Also sent as the <c>Accept-Language</c> header of oEmbed requests (underscore converted to a hyphen).
    /// </summary>
    public string FacebookSdkLocale { get; set; } = "en_US";

    /// <summary>How the SDK script tags reach the page. Default <see cref="EmbedScriptMode.Inline"/>.</summary>
    public EmbedScriptMode ScriptMode { get; set; } = EmbedScriptMode.Inline;

    /// <summary>How long a successful oEmbed response is cached. Default 12 hours.</summary>
    public TimeSpan SuccessCacheDuration { get; set; } = TimeSpan.FromHours(12);

    /// <summary>How long a "not found" / "rejected by Meta" failure is cached. Default 1 hour.</summary>
    public TimeSpan NotFoundCacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How long a transient failure (network, 5xx, 429, timeout, bad JSON) is cached. Default 2 minutes.</summary>
    public TimeSpan TransientFailureCacheDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Timeout for a single oEmbed HTTP call. Default 5 seconds.</summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum accepted oEmbed response body size in bytes. Default 64 KB.</summary>
    public int MaxResponseBytes { get; set; } = 64 * 1024;
}
