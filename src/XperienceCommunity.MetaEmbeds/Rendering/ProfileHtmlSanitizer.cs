using System.Text.RegularExpressions;

using AngleSharp.Css.Dom;

using Ganss.Xss;

namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>
/// A <see cref="HtmlSanitizer"/> configured once from an <see cref="EmbedSanitizerProfile"/>. The allowed sets are
/// never touched after construction. The only per-call state is <see cref="RemovedTags"/>, which is why instances
/// are pooled by <see cref="EmbedHtmlSanitizer"/> and used by one thread at a time.
/// </summary>
internal sealed class ProfileHtmlSanitizer : HtmlSanitizer
{
    /// <summary>
    /// CSS values that must never survive, on top of the property allowlist: anything that loads a resource
    /// (<c>url()</c>, <c>image()</c>, <c>image-set()</c>, <c>src()</c>), legacy script vectors (<c>expression()</c>,
    /// <c>behavior</c>, <c>-moz-binding</c>, <c>javascript:</c>, <c>vbscript:</c>), <c>@import</c>, and characters
    /// that could smuggle any of those past the parser (backslash escapes, angle brackets, control characters).
    /// </summary>
    private static readonly Regex DisallowedCssValue = new(
        @"[<>\\\p{C}]|(?:url|image|image-set|src|expression)\s*\(|javascript:|vbscript:|-moz-binding|behavior|@import",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private readonly EmbedSanitizerProfile profile;
    private readonly List<string> removedTags = [];

    public ProfileHtmlSanitizer(EmbedSanitizerProfile profile)
        : base(CreateOptions(profile))
    {
        this.profile = profile;

        KeepChildNodes = false;
        AllowDataAttributes = false;
        AllowCssCustomProperties = false;
        DisallowCssPropertyValue = DisallowedCssValue;

        // Stateless: only reads the immutable profile, so attaching it once is safe.
        FilterUrl += OnFilterUrl;
    }

    /// <summary>Element names removed during the current <c>SanitizeDom</c> call, distinct, in document order.</summary>
    public IReadOnlyList<string> RemovedTags => removedTags;

    /// <summary>Clears per-call state before the instance goes back to the pool.</summary>
    public void Reset() => removedTags.Clear();

    /// <inheritdoc />
    protected override void OnRemovingTag(RemovingTagEventArgs e)
    {
        var name = e.Tag.LocalName;
        if (!string.IsNullOrEmpty(name) && !removedTags.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            removedTags.Add(name);
        }

        base.OnRemovingTag(e);
    }

    private void OnFilterUrl(object? sender, FilterUrlEventArgs e)
    {
        // Null means the library already rejected the URL (disallowed scheme, unparsable). Anything else must be an
        // absolute https URL on one of the profile's hosts; this also runs for url() inside style attributes, which
        // the value regex rejects before it gets here, so no CSS url() ever survives.
        if (e.SanitizedUrl is not null && !profile.IsAllowedUrl(e.SanitizedUrl))
        {
            e.SanitizedUrl = null;
        }
    }

    private static HtmlSanitizerOptions CreateOptions(EmbedSanitizerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(profile.Tags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(profile.Attributes, StringComparer.OrdinalIgnoreCase),
            AllowedCssClasses = new HashSet<string>(profile.Classes, StringComparer.Ordinal),
            AllowedCssProperties = new HashSet<string>(EmbedSanitizerProfile.AllowedCssProperties, StringComparer.OrdinalIgnoreCase),
            AllowedAtRules = new HashSet<CssRuleType>(),
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Uri.UriSchemeHttps },
            UriAttributes = new HashSet<string>(profile.UriAttributes, StringComparer.OrdinalIgnoreCase),
            UriListAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            AllowCssCustomProperties = false,
            AllowDataAttributes = false,
        };
    }
}
