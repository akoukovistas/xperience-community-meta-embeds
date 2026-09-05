using Kentico.PageBuilder.Web.Mvc;
using Kentico.Xperience.Admin.Base.FormAnnotations;

using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Widgets;

/// <summary>
/// Properties of the Meta embed widget. <see cref="Url"/> is the only required field. The "Appearance" category holds
/// the layout, caption and theme choices; the <see cref="SourceType"/> dropdown sits in a collapsed "Advanced"
/// category and has a single option in this version.
/// </summary>
/// <remarks>
/// Widget configuration is stored as JSON and deserialised onto this class, so a configuration saved before a
/// property existed gets that property's C# initializer. Anything that reads the string properties must still pass
/// them through their <c>Normalize</c> helpers, because a stored <c>null</c> overrides the initializer.
/// </remarks>
[FormCategory(
    Label = "{$xperiencecommunity.metaembeds.properties.category.appearance$}",
    Order = 20,
    Collapsible = true,
    IsCollapsed = false)]
[FormCategory(
    Label = "{$xperiencecommunity.metaembeds.properties.category.advanced$}",
    Order = 90,
    Collapsible = true,
    IsCollapsed = true)]
public sealed class MetaEmbedWidgetProperties : IWidgetProperties
{
    /// <summary>Public URL of the Threads, Instagram or Facebook post to embed.</summary>
    [TextInputComponent(
        Label = "{$xperiencecommunity.metaembeds.properties.url.label$}",
        ExplanationText = "{$xperiencecommunity.metaembeds.properties.url.explanationText$}",
        Order = 10)]
    [RequiredValidationRule]
    [UrlValidationRule]
    [MaxLengthValidationRule(2048)]
    public string Url { get; set; } = string.Empty;

    /// <summary>How the embed sits in its column: <c>natural</c>, <c>centered</c> or <c>fluid</c>. See <see cref="EmbedLayouts"/>.</summary>
    [DropDownComponent(
        Label = "{$xperiencecommunity.metaembeds.properties.layout.label$}",
        ExplanationText = "{$xperiencecommunity.metaembeds.properties.layout.explanationText$}",
        Options = EmbedLayouts.Natural + ";{$xperiencecommunity.metaembeds.properties.layout.options.natural$}\n"
                + EmbedLayouts.Centered + ";{$xperiencecommunity.metaembeds.properties.layout.options.centered$}\n"
                + EmbedLayouts.Fluid + ";{$xperiencecommunity.metaembeds.properties.layout.options.fluid$}",
        Order = 30)]
    public string Layout { get; set; } = EmbedLayouts.Natural;

    /// <summary>Instagram only: hide the caption under the media (oEmbed <c>hidecaption=true</c>). Ignored for other platforms.</summary>
    [CheckBoxComponent(
        Label = "{$xperiencecommunity.metaembeds.properties.hideCaption.label$}",
        ExplanationText = "{$xperiencecommunity.metaembeds.properties.hideCaption.explanationText$}",
        Order = 40)]
    public bool HideCaption { get; set; }

    /// <summary>Threads only: <c>light</c> or <c>dark</c> embed styling. Ignored for other platforms. See <see cref="EmbedThemes"/>.</summary>
    [DropDownComponent(
        Label = "{$xperiencecommunity.metaembeds.properties.theme.label$}",
        ExplanationText = "{$xperiencecommunity.metaembeds.properties.theme.explanationText$}",
        Options = EmbedThemes.Light + ";{$xperiencecommunity.metaembeds.properties.theme.options.light$}\n"
                + EmbedThemes.Dark + ";{$xperiencecommunity.metaembeds.properties.theme.options.dark$}",
        Order = 50)]
    public string Theme { get; set; } = EmbedThemes.Light;

    /// <summary>What the URL points to. <c>post</c> (the only value in this version) means a single post, reel or video.</summary>
    [DropDownComponent(
        Label = "{$xperiencecommunity.metaembeds.properties.sourceType.label$}",
        ExplanationText = "{$xperiencecommunity.metaembeds.properties.sourceType.explanationText$}",
        Options = EmbedSourceTypes.Post + ";{$xperiencecommunity.metaembeds.properties.sourceType.options.post$}",
        Order = 100)]
    public string SourceType { get; set; } = EmbedSourceTypes.Post;
}
