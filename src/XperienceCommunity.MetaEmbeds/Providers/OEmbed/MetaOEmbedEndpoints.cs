using System.Text.RegularExpressions;

#pragma warning disable S1075 // Hard-coded URIs: this file is the table of Meta's fixed endpoint and SDK addresses.

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>
/// The four default endpoint definitions, a typed port of the provider table in Meta's WordPress plugin
/// (facebook/meta-embeds-for-wordpress 1.2.2) with shortcode character classes tightened to <c>[A-Za-z0-9_-]</c>.
/// </summary>
public static class MetaOEmbedEndpoints
{
    private const RegexOptions Options =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Threads posts: <c>threads.com/@user/post/{code}</c> and <c>threads.com/t/{code}</c> (also <c>.net</c>).</summary>
    public static MetaOEmbedEndpoint Threads { get; } = new(
        Key: "threads",
        DisplayName: "Threads",
        UrlPatterns:
        [
            Pattern(@"^https?://(www\.)?threads\.(com|net)/@[^/]+/post/[A-Za-z0-9_-]+/?(\?.*)?$"),
            Pattern(@"^https?://(www\.)?threads\.(com|net)/t/[A-Za-z0-9_-]+/?(\?.*)?$"),
        ],
        EndpointUri: _ => new Uri("https://graph.threads.com/oembed"),
        SdkScriptUri: _ => new Uri("https://www.threads.com/embed.js"),
        ExpectedRootSelector: "blockquote.text-post-media",
        SanitizerProfile: "threads");

    /// <summary>
    /// Instagram posts and reels (<c>/p/{code}</c>, <c>/reel/{code}</c>) plus profile URLs. Profiles are accepted for
    /// parity with Meta's plugin, but the tokenless endpoint currently rejects them (HTTP 400, subcode 2207047), which
    /// surfaces as <see cref="EmbedFailureKind.RejectedByProvider"/>.
    /// </summary>
    public static MetaOEmbedEndpoint Instagram { get; } = new(
        Key: "instagram",
        DisplayName: "Instagram",
        UrlPatterns:
        [
            Pattern(@"^https?://(www\.)?instagram\.com/(p|reel)/[A-Za-z0-9_-]+/?(\?.*)?$"),
            Pattern(@"^https?://(www\.)?instagram\.com/(?!stories/|explore/|accounts/|direct/|tv/|about/|legal/|developer/|api/|static/|nametag/|directory/)([A-Za-z0-9._]{1,30})/?(\?.*)?$"),
        ],
        EndpointUri: o => new Uri($"https://graph.facebook.com/{o.GraphApiVersion}/instagram_oembed"),
        SdkScriptUri: _ => new Uri("https://www.instagram.com/embed.js"),
        ExpectedRootSelector: "blockquote.instagram-media",
        SanitizerProfile: "instagram");

    /// <summary>Facebook posts: <c>facebook.com/{user}/posts/{id}</c>.</summary>
    public static MetaOEmbedEndpoint FacebookPost { get; } = new(
        Key: "facebook-post",
        DisplayName: "Facebook post",
        UrlPatterns:
        [
            Pattern(@"^https?://(www\.)?facebook\.com/[^/?#]+/posts/[^/?#]+/?(\?.*)?$"),
        ],
        EndpointUri: o => new Uri($"https://graph.facebook.com/{o.GraphApiVersion}/oembed_post"),
        SdkScriptUri: FacebookSdk,
        ExpectedRootSelector: "div.fb-post",
        SanitizerProfile: "facebook");

    /// <summary>Facebook reels / videos: <c>facebook.com/reel/{id}</c>.</summary>
    public static MetaOEmbedEndpoint FacebookVideo { get; } = new(
        Key: "facebook-video",
        DisplayName: "Facebook video",
        UrlPatterns:
        [
            Pattern(@"^https?://(www\.)?facebook\.com/reel/[0-9]+/?(\?.*)?$"),
        ],
        EndpointUri: o => new Uri($"https://graph.facebook.com/{o.GraphApiVersion}/oembed_video"),
        SdkScriptUri: FacebookSdk,
        ExpectedRootSelector: "div.fb-video",
        SanitizerProfile: "facebook");

    /// <summary>Default match order: Threads, Instagram, Facebook post, Facebook video.</summary>
    public static IReadOnlyList<MetaOEmbedEndpoint> Default { get; } = [Threads, Instagram, FacebookPost, FacebookVideo];

    /// <summary>The Facebook JS SDK URL for the configured locale and Graph version.</summary>
    public static Uri FacebookSdk(MetaEmbedsOptions options) =>
        new($"https://connect.facebook.net/{options.FacebookSdkLocale}/sdk.js#xfbml=1&version={options.GraphApiVersion}");

    private static Regex Pattern(string pattern) => new(pattern, Options, MatchTimeout);
}
