using System.Text.RegularExpressions;

using CMS.Core;

using Kentico.Content.Web.Mvc;
using Kentico.PageBuilder.Web.Mvc;
using Kentico.Web.Mvc;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Rendering;
using XperienceCommunity.MetaEmbeds.Resources;
using XperienceCommunity.MetaEmbeds.Widgets;

[assembly: RegisterWidget(
    identifier: MetaEmbedWidget.IDENTIFIER,
    viewComponentType: typeof(MetaEmbedWidget),
    name: "{$xperiencecommunity.metaembeds.widget.name$}",
    propertiesType: typeof(MetaEmbedWidgetProperties),
    Description = "{$xperiencecommunity.metaembeds.widget.description$}",
    IconClass = "icon-brand-instagram",
    AllowCache = true)]

namespace XperienceCommunity.MetaEmbeds.Widgets;

/// <summary>
/// The "Meta embed" Page Builder widget. Resolves the configured URL through <see cref="IEmbedResolver"/>, renders the
/// sanitised markup, and emits each Meta SDK script at most once per request. Never throws: anything unexpected is
/// logged once and rendered as empty output.
/// </summary>
public sealed class MetaEmbedWidget : ViewComponent
{
    /// <summary>Widget identifier stored in page configuration. Never change it.</summary>
    public const string IDENTIFIER = "XperienceCommunity.MetaEmbeds.Embed";

    /// <summary>Path of the widget view inside this Razor class library.</summary>
    internal const string ViewPath = "~/Components/Widgets/MetaEmbedWidget/_MetaEmbedWidget.cshtml";

    /// <summary>Marker claimed by the first Facebook embed in a request, which then renders <c>&lt;div id="fb-root"&gt;</c>.</summary>
    internal const string FacebookRootMarker = "fb-root";

    private static readonly EventId RenderFailedEvent = new(0, "METAEMBEDS_WIDGET_RENDER_FAILED");

    // Meta's Facebook oEmbed markup starts with its own <div id="fb-root"></div>. The widget removes it and renders
    // exactly one per request itself, so two Facebook embeds on a page do not produce two roots.
    private static readonly Regex FacebookRootDiv = new(
        @"<div\s+id\s*=\s*[""']fb-root[""']\s*>\s*</div>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    private readonly IEmbedResolver resolver;
    private readonly IEmbedScriptRegistry scripts;
    private readonly IWidgetRenderModeDetector renderMode;
    private readonly IOptionsMonitor<MetaEmbedsOptions> options;
    private readonly ILogger<MetaEmbedWidget> logger;
    private readonly ILocalizationService localization;

    /// <summary>Creates the widget. Resolved by the MVC view component activator.</summary>
    public MetaEmbedWidget(
        IEmbedResolver resolver,
        IEmbedScriptRegistry scripts,
        IPageBuilderDataContextRetriever pageBuilderDataContextRetriever,
        IOptionsMonitor<MetaEmbedsOptions> options,
        ILogger<MetaEmbedWidget> logger,
        ILocalizationService localization)
        : this(
            resolver,
            scripts,
            new PageBuilderRenderModeDetector(pageBuilderDataContextRetriever),
            options,
            logger,
            localization)
    {
    }

    /// <summary>Test seam: lets the render mode detection be substituted.</summary>
    internal MetaEmbedWidget(
        IEmbedResolver resolver,
        IEmbedScriptRegistry scripts,
        IWidgetRenderModeDetector renderMode,
        IOptionsMonitor<MetaEmbedsOptions> options,
        ILogger<MetaEmbedWidget> logger,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(renderMode);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(localization);

        this.resolver = resolver;
        this.scripts = scripts;
        this.renderMode = renderMode;
        this.options = options;
        this.logger = logger;
        this.localization = localization;
    }

    /// <summary>Renders the widget. Returns empty content for every failure on the live site and for any unexpected exception.</summary>
    /// <param name="viewModel">Page Builder view model carrying the widget properties, page and cache dependencies.</param>
    public async Task<IViewComponentResult> InvokeAsync(ComponentViewModel<MetaEmbedWidgetProperties> viewModel)
    {
        try
        {
            return await RenderAsync(viewModel);
        }
        catch (Exception exception)
        {
            logger.LogError(RenderFailedEvent, exception, "The Meta embed widget failed to render and returned empty output.");
            return Content(string.Empty);
        }
    }

    private async Task<IViewComponentResult> RenderAsync(ComponentViewModel<MetaEmbedWidgetProperties>? viewModel)
    {
        var httpContext = HttpContext;
        var isEditContext = renderMode.IsEditOrPreview(httpContext);
        var properties = viewModel?.Properties ?? new MetaEmbedWidgetProperties();

        // Widget output cache (AllowCache = true): let CacheHelper.TouchKey("metaembeds|all") evict cached widget output too.
        if (viewModel?.CacheDependencies is { } cacheDependencies)
        {
            cacheDependencies.CacheKeys = [MetaEmbedsConstants.CacheKeyAll];
        }

        var presentation = EmbedPresentation.From(properties.Layout, properties.Theme, properties.HideCaption);
        var request = new EmbedRequest
        {
            SourceType = EmbedSourceTypes.Normalize(properties.SourceType),
            Input = properties.Url?.Trim() ?? string.Empty,
            Culture = viewModel?.Page?.LanguageName,
            Parameters = presentation.HideCaption ? HideCaptionParameters : EmbedRequestParameters.None,
        };

        var cancellationToken = httpContext?.RequestAborted ?? CancellationToken.None;
        var result = await resolver.ResolveAsync(request, cancellationToken);

        if (result is null || !result.Succeeded || result.Items.Count == 0)
        {
            if (!isEditContext)
            {
                return Content(string.Empty);
            }

            var failure = result?.Failure ?? new EmbedFailure { Kind = EmbedFailureKind.Internal };
            return View(ViewPath, new MetaEmbedWidgetViewModel
            {
                IsEditMode = true,
                EditorMessage = ResolveEditorMessage(failure),
            });
        }

        return View(ViewPath, BuildSuccessModel(result.Items, isEditContext, presentation));
    }

    /// <summary>Request parameters sent when the editor ticked "Hide caption" (Instagram honours it, the others ignore it).</summary>
    private static readonly IReadOnlyDictionary<string, string> HideCaptionParameters =
        new Dictionary<string, string>(1) { [EmbedRequestParameters.HideCaption] = "true" };

    private MetaEmbedWidgetViewModel BuildSuccessModel(IReadOnlyList<EmbedItem> items, bool isEditContext, EmbedPresentation presentation)
    {
        var endpointKey = items[0].EndpointKey ?? string.Empty;
        var isFacebook = endpointKey.StartsWith("facebook", StringComparison.OrdinalIgnoreCase);
        var scriptMode = options.CurrentValue.ScriptMode;

        var htmlParts = new List<string>(items.Count);
        var scriptsToEmit = new List<Uri>();

        foreach (var item in items)
        {
            var html = item.Html ?? string.Empty;
            html = isFacebook ? StripFacebookRoot(html) : html;
            htmlParts.Add(EmbedMarkupDecorator.Apply(html, item.EndpointKey, presentation));

            if (scriptMode == EmbedScriptMode.None)
            {
                continue;
            }

            foreach (var script in item.RequiredScripts ?? [])
            {
                if (script is null)
                {
                    continue;
                }

                // TagHelper mode: claim so <meta-embeds-scripts /> can render it, but emit nothing here.
                var claimed = scripts.TryClaim(script);
                if (claimed && scriptMode == EmbedScriptMode.Inline)
                {
                    scriptsToEmit.Add(script);
                }
            }
        }

        var emitFacebookRoot = isFacebook && scripts.TryClaimMarker(FacebookRootMarker);

        return new MetaEmbedWidgetViewModel
        {
            Html = string.Join("\n", htmlParts),
            EndpointKey = endpointKey,
            ScriptsToEmit = scriptsToEmit,
            EmitFacebookRoot = emitFacebookRoot,
            IsEditMode = isEditContext,
            Presentation = presentation,
            CssClass = presentation.CssClasses(endpointKey),
            WrapperStyle = presentation.WrapperStyle,
        };
    }

    private string ResolveEditorMessage(EmbedFailure failure)
    {
        // A provider may name a resource key instead of a literal, so a more specific message stays localisable.
        if (!string.IsNullOrWhiteSpace(failure.EditorMessage))
        {
            return failure.EditorMessage.StartsWith(MetaEmbedsConstants.ResourcePrefix, StringComparison.Ordinal)
                ? Localize(failure.EditorMessage)
                : failure.EditorMessage;
        }

        return Localize(MetaEmbedsResources.FailureKey(failure.Kind));
    }

    private string Localize(string key)
    {
        var text = localization.GetString(key);
        return string.IsNullOrWhiteSpace(text) ? key : text;
    }

    private static string StripFacebookRoot(string html)
    {
        try
        {
            return FacebookRootDiv.Replace(html, string.Empty);
        }
        catch (RegexMatchTimeoutException)
        {
            return html;
        }
    }
}

/// <summary>
/// Decides whether the widget renders for an editor (Page Builder <see cref="PageBuilderMode.Edit"/> /
/// <see cref="PageBuilderMode.ReadOnly"/>, or preview) or for a visitor. Internal seam so the widget is unit-testable
/// without Xperience infrastructure.
/// </summary>
internal interface IWidgetRenderModeDetector
{
    /// <summary>True when editor messages and the click-blocking overlay should be rendered.</summary>
    bool IsEditOrPreview(HttpContext? httpContext);
}

/// <summary>Default detector: <c>IPageBuilderDataContextRetriever.Retrieve().GetMode()</c> plus <c>HttpContext.Kentico().Preview().Enabled</c>.</summary>
internal sealed class PageBuilderRenderModeDetector : IWidgetRenderModeDetector
{
    private readonly IPageBuilderDataContextRetriever retriever;

    public PageBuilderRenderModeDetector(IPageBuilderDataContextRetriever retriever)
    {
        ArgumentNullException.ThrowIfNull(retriever);
        this.retriever = retriever;
    }

    public bool IsEditOrPreview(HttpContext? httpContext)
    {
        try
        {
            var mode = retriever.Retrieve()?.GetMode() ?? PageBuilderMode.Off;
            if (mode is PageBuilderMode.Edit or PageBuilderMode.ReadOnly)
            {
                return true;
            }
        }
        catch (Exception)
        {
            // No Page Builder context for this request (e.g. the widget is rendered outside a Page Builder page): live.
        }

        try
        {
            return httpContext?.Kentico()?.Preview()?.Enabled == true;
        }
        catch (Exception)
        {
            // Preview feature not available on this request: live.
            return false;
        }
    }
}
