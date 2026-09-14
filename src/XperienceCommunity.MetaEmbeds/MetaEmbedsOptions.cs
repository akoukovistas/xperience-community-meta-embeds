using System.Text.RegularExpressions;

namespace XperienceCommunity.MetaEmbeds;

/// <summary>
/// Configuration for the Meta embeds integration. Every member has a working default, so the
/// <c>XperienceCommunityMetaEmbeds</c> configuration section is optional.
/// </summary>
public sealed class MetaEmbedsOptions
{
    /// <summary>Name of the configuration section the options bind from.</summary>
    public const string SectionName = "XperienceCommunityMetaEmbeds";

    /// <summary>Graph API version used when <see cref="GraphApiVersion"/> is unset or malformed.</summary>
    internal const string DefaultGraphApiVersion = "v25.0";

    /// <summary>Facebook SDK locale used when <see cref="FacebookSdkLocale"/> is unset or malformed.</summary>
    internal const string DefaultFacebookSdkLocale = "en_US";

    /// <summary>
    /// Both values are interpolated into URLs, so they are constrained to the shapes Meta actually uses. Anything else
    /// - a leading slash, an <c>@</c>, a whole URL - could otherwise move an outbound call to a different host.
    /// </summary>
    private static readonly Regex GraphApiVersionPattern = new(
        @"^v\d{1,3}\.\d{1,3}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex FacebookSdkLocalePattern = new(
        @"^[A-Za-z]{2}_[A-Za-z]{2}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Optional Meta credentials. Empty (the default) means tokenless oEmbed calls. Any populated member switches
    /// the same calls to the authenticated route by appending an <c>access_token</c> parameter.
    /// </summary>
    public MetaCredentialsOptions Credentials { get; set; } = new();

    /// <summary>
    /// Graph API version segment used for graph.facebook.com endpoints (Instagram, Facebook) and in the Facebook SDK
    /// URL. Default <c>v25.0</c>, the version Meta's own WordPress plugin pins. A value that is not <c>v{major}.{minor}</c>
    /// is ignored in favour of the default - see <see cref="EffectiveGraphApiVersion"/>.
    /// </summary>
    public string GraphApiVersion { get; set; } = DefaultGraphApiVersion;

    /// <summary>
    /// Locale segment of the Facebook SDK URL (<c>connect.facebook.net/{locale}/sdk.js</c>). Default <c>en_US</c>.
    /// Also sent as the <c>Accept-Language</c> header of oEmbed requests (underscore converted to a hyphen). A value
    /// that is not <c>xx_XX</c> is ignored in favour of the default - see <see cref="EffectiveFacebookSdkLocale"/>.
    /// </summary>
    public string FacebookSdkLocale { get; set; } = DefaultFacebookSdkLocale;

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

    /// <summary>
    /// <see cref="GraphApiVersion"/> if it is a well-formed version segment, otherwise <see cref="DefaultGraphApiVersion"/>.
    /// Every URL built from the option uses this, so a typo cannot change the host an outbound call goes to.
    /// </summary>
    internal string EffectiveGraphApiVersion =>
        GraphApiVersion is not null && GraphApiVersionPattern.IsMatch(GraphApiVersion) ? GraphApiVersion : DefaultGraphApiVersion;

    /// <summary>
    /// <see cref="FacebookSdkLocale"/> if it is a well-formed <c>xx_XX</c> locale, otherwise
    /// <see cref="DefaultFacebookSdkLocale"/>. Every URL built from the option uses this.
    /// </summary>
    internal string EffectiveFacebookSdkLocale =>
        FacebookSdkLocale is not null && FacebookSdkLocalePattern.IsMatch(FacebookSdkLocale) ? FacebookSdkLocale : DefaultFacebookSdkLocale;
}
