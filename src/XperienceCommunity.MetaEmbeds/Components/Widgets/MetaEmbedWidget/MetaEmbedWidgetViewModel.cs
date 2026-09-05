using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Widgets;

/// <summary>Everything <c>_MetaEmbedWidget.cshtml</c> needs. The view has no logic that can throw.</summary>
public sealed class MetaEmbedWidgetViewModel
{
    /// <summary>Sanitised embed markup with the presentation applied, rendered raw. Empty when there is nothing to show.</summary>
    public string Html { get; init; } = string.Empty;

    /// <summary>
    /// SDK scripts this widget instance emits, i.e. the ones it was the first to claim in the current request. Populated
    /// only in <see cref="EmbedScriptMode.Inline"/> mode.
    /// </summary>
    public IReadOnlyList<Uri> ScriptsToEmit { get; init; } = [];

    /// <summary>Message shown instead of the embed. Non-null only in Page Builder edit / read-only mode or preview.</summary>
    public string? EditorMessage { get; init; }

    /// <summary>True in Page Builder edit / read-only mode or preview: adds a transparent overlay so the embed cannot swallow clicks and drags.</summary>
    public bool IsEditMode { get; init; }

    /// <summary>Endpoint key used as a CSS hook: <c>meta-embed meta-embed--instagram</c>.</summary>
    public string EndpointKey { get; init; } = string.Empty;

    /// <summary>True when this widget instance renders the page's single <c>&lt;div id="fb-root"&gt;&lt;/div&gt;</c> (first Facebook embed in the request).</summary>
    public bool EmitFacebookRoot { get; init; }

    /// <summary>The editor's appearance choices, normalised.</summary>
    public EmbedPresentation Presentation { get; init; } = EmbedPresentation.Default;

    /// <summary>Full class attribute of the wrapper, e.g. <c>meta-embed meta-embed--threads meta-embed--layout-centered meta-embed--theme-dark</c>.</summary>
    public string CssClass { get; init; } = "meta-embed";

    /// <summary>Inline style the layout needs (centered only), or null.</summary>
    public string? WrapperStyle { get; init; }
}
