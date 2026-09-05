using Kentico.PageBuilder.Web.Mvc;
using Kentico.Xperience.Admin.Base.FormAnnotations;

using XperienceCommunity.MetaEmbeds.Providers;

namespace XperienceCommunity.MetaEmbeds.Widgets;

/// <summary>
/// Properties of the Meta embed widget. <see cref="Url"/> is the only field editors normally touch; the
/// <see cref="SourceType"/> dropdown sits in a collapsed "Advanced" category and has a single option in this version.
/// </summary>
/// <remarks>
/// Widget configuration is stored as JSON and deserialised onto this class, so a configuration saved before a
/// property existed gets that property's C# initializer. Anything that reads <see cref="SourceType"/> must still pass
/// it through <see cref="EmbedSourceTypes.Normalize"/>, because a stored <c>null</c> overrides the initializer.
/// </remarks>
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

    /// <summary>What the URL points to. <c>post</c> (the only value in this version) means a single post, reel or video.</summary>
    [DropDownComponent(
        Label = "{$xperiencecommunity.metaembeds.properties.sourceType.label$}",
        ExplanationText = "{$xperiencecommunity.metaembeds.properties.sourceType.explanationText$}",
        Options = EmbedSourceTypes.Post + ";{$xperiencecommunity.metaembeds.properties.sourceType.options.post$}",
        Order = 100)]
    public string SourceType { get; set; } = EmbedSourceTypes.Post;
}
