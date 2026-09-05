using System.Text.RegularExpressions;

namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>
/// Immutable allowlist for one platform's oEmbed markup, derived from the live captures in
/// <c>tests/.../Fixtures</c> (2026-09-04). Everything not listed here is removed by <see cref="EmbedHtmlSanitizer"/>.
/// The sets are created once and never mutated, so a profile can be shared freely between threads.
/// </summary>
internal sealed class EmbedSanitizerProfile
{
    private const int MaxUrlLength = 2048;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private const RegexOptions IdPatternOptions =
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline;

    /// <summary>
    /// CSS properties Meta's placeholder markup uses (see the fixtures) plus their longhands and a few safe
    /// siblings. Deliberately absent: <c>position</c>, <c>z-index</c>, <c>top/right/bottom/left</c>, <c>opacity</c>,
    /// <c>pointer-events</c>, <c>visibility</c>, <c>clip-path</c>, <c>filter</c>, <c>mask</c>, <c>content</c>,
    /// <c>cursor</c>, animations and transitions, so the markup cannot overlay or hide parts of the host page.
    /// </summary>
    private static readonly string[] SafeCssProperties =
    [
        "align-items", "align-self",
        "background", "background-attachment", "background-clip", "background-color", "background-image",
        "background-origin", "background-position", "background-position-x", "background-position-y",
        "background-repeat", "background-repeat-x", "background-repeat-y", "background-size",
        "border", "border-bottom", "border-bottom-color", "border-bottom-left-radius", "border-bottom-right-radius",
        "border-bottom-style", "border-bottom-width", "border-color", "border-left", "border-left-color",
        "border-left-style", "border-left-width", "border-radius", "border-right", "border-right-color",
        "border-right-style", "border-right-width", "border-style", "border-top", "border-top-color",
        "border-top-left-radius", "border-top-right-radius", "border-top-style", "border-top-width", "border-width",
        "box-shadow", "box-sizing", "color", "column-gap", "display",
        "flex", "flex-basis", "flex-direction", "flex-flow", "flex-grow", "flex-shrink", "flex-wrap",
        "font", "font-family", "font-size", "font-stretch", "font-style", "font-variant", "font-weight",
        "gap", "height", "justify-content", "letter-spacing", "line-height",
        "margin", "margin-bottom", "margin-left", "margin-right", "margin-top",
        "max-height", "max-width", "min-height", "min-width",
        "overflow", "overflow-wrap", "overflow-x", "overflow-y",
        "padding", "padding-bottom", "padding-left", "padding-right", "padding-top", "row-gap",
        "text-align", "text-decoration", "text-decoration-color", "text-decoration-line", "text-decoration-style",
        "text-transform", "transform", "vertical-align", "white-space", "width", "word-break",
    ];

    private EmbedSanitizerProfile(
        string name,
        string[] tags,
        string[] attributes,
        string[] classes,
        string[] uriAttributes,
        string[] urlHosts,
        Regex? allowedIdPattern)
    {
        Name = name;
        Tags = tags;
        Attributes = attributes;
        Classes = classes;
        UriAttributes = uriAttributes;
        UrlHosts = urlHosts;
        AllowedIdPattern = allowedIdPattern;
    }

    /// <summary>Profile name as it appears in <c>MetaOEmbedEndpoint.SanitizerProfile</c>.</summary>
    public string Name { get; }

    /// <summary>Element names that survive. Everything else, including <c>script</c>, is removed with its content.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>Attribute names that survive on allowed elements. <c>on*</c>, <c>src</c>, <c>srcdoc</c> etc. are never listed.</summary>
    public IReadOnlyList<string> Attributes { get; }

    /// <summary>The only <c>class</c> tokens that survive. Unlisted tokens are dropped; an emptied attribute is removed.</summary>
    public IReadOnlyList<string> Classes { get; }

    /// <summary>Attributes whose value is a URL and must pass <see cref="IsAllowedUrl"/>.</summary>
    public IReadOnlyList<string> UriAttributes { get; }

    /// <summary>Registrable domains a URL may point at (the host must equal one of them or be a subdomain).</summary>
    public IReadOnlyList<string> UrlHosts { get; }

    /// <summary>When <c>id</c> is an allowed attribute, the pattern its value must match; otherwise the attribute is removed.</summary>
    public Regex? AllowedIdPattern { get; }

    /// <summary>CSS properties allowed inside <c>style</c> attributes, shared by all profiles.</summary>
    public static IReadOnlyList<string> AllowedCssProperties => SafeCssProperties;

    /// <summary>Instagram posts and reels: <c>blockquote.instagram-media</c> with an inline SVG logo.</summary>
    public static EmbedSanitizerProfile Instagram { get; } = new(
        name: "instagram",
        tags: ["blockquote", "div", "a", "svg", "g", "path"],
        attributes:
        [
            "class", "style", "href", "target", "rel",
            "data-instgrm-permalink", "data-instgrm-version", "data-instgrm-captioned",
            "width", "height", "viewBox", "version", "xmlns", "xmlns:xlink",
            "stroke", "stroke-width", "fill", "fill-rule", "transform", "d",
        ],
        classes: ["instagram-media"],
        uriAttributes: ["href", "data-instgrm-permalink"],
        urlHosts: ["instagram.com"],
        allowedIdPattern: null);

    /// <summary>Threads posts: <c>blockquote.text-post-media</c> with an <c>ig-tp-{code}</c> id and an inline SVG logo.</summary>
    public static EmbedSanitizerProfile Threads { get; } = new(
        name: "threads",
        tags: ["blockquote", "a", "div", "svg", "path"],
        attributes:
        [
            "class", "id", "style", "href", "target", "rel",
            "data-text-post-permalink", "data-text-post-version", "data-theme",
            "width", "height", "viewBox", "fill", "d", "xmlns", "aria-label", "role",
        ],
        classes: ["text-post-media"],
        uriAttributes: ["href", "data-text-post-permalink"],
        urlHosts: ["threads.com", "threads.net"],
        allowedIdPattern: new Regex("^ig-tp-[A-Za-z0-9_-]{1,64}$", IdPatternOptions, RegexTimeout));

    /// <summary>Facebook posts and videos: <c>div#fb-root</c> plus <c>div.fb-post</c> / <c>div.fb-video</c>. The strictest profile.</summary>
    public static EmbedSanitizerProfile Facebook { get; } = new(
        name: "facebook",
        tags: ["div"],
        attributes: ["id", "class", "data-href", "data-width"],
        classes: ["fb-post", "fb-video"],
        uriAttributes: ["data-href"],
        urlHosts: ["facebook.com"],
        allowedIdPattern: new Regex("^fb-root$", IdPatternOptions, RegexTimeout));

    /// <summary>All built-in profiles.</summary>
    public static IReadOnlyList<EmbedSanitizerProfile> All { get; } = [Instagram, Threads, Facebook];

    /// <summary>
    /// Finds the profile for an endpoint's <c>SanitizerProfile</c>. Unknown or empty names get <see cref="Facebook"/>,
    /// the strictest profile: only <c>div</c> elements survive, so any markup that is not Facebook-shaped loses its
    /// root element and the provider fails closed.
    /// </summary>
    public static EmbedSanitizerProfile Resolve(string? name) =>
        All.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Facebook;

    /// <summary>
    /// True when <paramref name="url"/> is an absolute <c>https</c> URL without userinfo or an explicit port whose
    /// host is one of <see cref="UrlHosts"/> or a subdomain of one. Relative and protocol-relative URLs are rejected.
    /// </summary>
    public bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength)
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
            || uri.HostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        var host = uri.Host;
        foreach (var allowed in UrlHosts)
        {
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
                || (host.Length > allowed.Length + 1
                    && host.EndsWith(allowed, StringComparison.OrdinalIgnoreCase)
                    && host[host.Length - allowed.Length - 1] == '.'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when an <c>id</c> attribute with this value may stay.</summary>
    public bool IsAllowedId(string? id)
    {
        if (AllowedIdPattern is null || string.IsNullOrEmpty(id))
        {
            return false;
        }

        try
        {
            return AllowedIdPattern.IsMatch(id);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
