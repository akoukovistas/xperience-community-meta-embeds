using System.Text.RegularExpressions;

using XperienceCommunity.MetaEmbeds.Providers.OEmbed;

namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>Values of the widget's <c>Layout</c> property.</summary>
public static class EmbedLayouts
{
    /// <summary>Meta's own width and left alignment (default).</summary>
    public const string Natural = "natural";

    /// <summary>Meta's own width, centered in the column.</summary>
    public const string Centered = "centered";

    /// <summary>
    /// Stretch to the column. Facebook posts get <c>data-width="auto"</c>; Instagram and Threads markup is already
    /// fluid up to Meta's 658px maximum, so for them this equals <see cref="Natural"/>.
    /// </summary>
    public const string Fluid = "fluid";

    /// <summary>Null, blank or unknown values resolve to <see cref="Natural"/>.</summary>
    public static string Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is Centered or Fluid ? normalized : Natural;
    }
}

/// <summary>Values of the widget's <c>Theme</c> property.</summary>
public static class EmbedThemes
{
    /// <summary>Meta's default (light) styling.</summary>
    public const string Light = "light";

    /// <summary>Dark styling. Honoured by Threads (<c>data-theme="dark"</c>); other platforms ignore it.</summary>
    public const string Dark = "dark";

    /// <summary>Null, blank or unknown values resolve to <see cref="Light"/>.</summary>
    public static string Normalize(string? value) =>
        string.Equals(value?.Trim(), Dark, StringComparison.OrdinalIgnoreCase) ? Dark : Light;
}

/// <summary>The appearance choices an editor made for one widget instance, normalised.</summary>
/// <param name="Layout">One of <see cref="EmbedLayouts"/>.</param>
/// <param name="Theme">One of <see cref="EmbedThemes"/>.</param>
/// <param name="HideCaption">Instagram only: the caption was requested to be hidden.</param>
public sealed record EmbedPresentation(string Layout, string Theme, bool HideCaption)
{
    /// <summary>Defaults: natural layout, light theme, caption shown.</summary>
    public static EmbedPresentation Default { get; } = new(EmbedLayouts.Natural, EmbedThemes.Light, false);

    /// <summary>Normalises raw property values.</summary>
    public static EmbedPresentation From(string? layout, string? theme, bool hideCaption) =>
        new(EmbedLayouts.Normalize(layout), EmbedThemes.Normalize(theme), hideCaption);

    /// <summary>
    /// CSS classes of the wrapper element: <c>meta-embed meta-embed--{endpoint} meta-embed--layout-{layout}</c>, plus
    /// <c>meta-embed--theme-dark</c> and <c>meta-embed--no-caption</c> when those options are on. Sites style these.
    /// </summary>
    public string CssClasses(string? endpointKey)
    {
        var classes = new List<string>(5) { "meta-embed" };
        if (!string.IsNullOrWhiteSpace(endpointKey))
        {
            classes.Add("meta-embed--" + endpointKey.Trim().ToLowerInvariant());
        }

        classes.Add("meta-embed--layout-" + Layout);
        if (Theme == EmbedThemes.Dark)
        {
            classes.Add("meta-embed--theme-dark");
        }

        if (HideCaption)
        {
            classes.Add("meta-embed--no-caption");
        }

        return string.Join(' ', classes);
    }

    /// <summary>
    /// The one inline style a layout needs to work without site CSS: <see cref="EmbedLayouts.Centered"/> centers the
    /// embed with flexbox. Other layouts need nothing on the wrapper.
    /// </summary>
    public string? WrapperStyle => Layout == EmbedLayouts.Centered ? "display:flex;justify-content:center;" : null;
}

/// <summary>
/// Applies presentation choices to sanitised embed markup by rewriting the attributes Meta's SDKs read. Works on the
/// sanitised HTML only, with fixed replacement values, so it cannot introduce anything the sanitiser removed.
/// </summary>
public static class EmbedMarkupDecorator
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex ThreadsTheme = new(@"\sdata-theme\s*=\s*""(light|dark)""", Options, Timeout);
    private static readonly Regex FacebookWidth = new(@"\sdata-width\s*=\s*""[^""]*""", Options, Timeout);
    private static readonly Regex FacebookPostRoot = new(@"<div\s+class\s*=\s*""fb-post""", Options, Timeout);

    /// <summary>Returns the markup with the presentation applied. Unknown endpoints and defaults return the input unchanged.</summary>
    public static string Apply(string html, string? endpointKey, EmbedPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (string.IsNullOrEmpty(html) || string.IsNullOrWhiteSpace(endpointKey))
        {
            return html ?? string.Empty;
        }

        try
        {
            var result = html;
            if (presentation.Theme == EmbedThemes.Dark && Is(endpointKey, MetaOEmbedEndpoints.Threads))
            {
                result = ThreadsTheme.Replace(result, " data-theme=\"dark\"", 1);
            }

            if (presentation.Layout == EmbedLayouts.Fluid && Is(endpointKey, MetaOEmbedEndpoints.FacebookPost))
            {
                result = FacebookWidth.IsMatch(result)
                    ? FacebookWidth.Replace(result, " data-width=\"auto\"", 1)
                    : FacebookPostRoot.Replace(result, "<div class=\"fb-post\" data-width=\"auto\"", 1);
            }

            return result;
        }
        catch (RegexMatchTimeoutException)
        {
            return html;
        }
    }

    private static bool Is(string endpointKey, MetaOEmbedEndpoint endpoint) =>
        string.Equals(endpointKey.Trim(), endpoint.Key, StringComparison.OrdinalIgnoreCase);
}
