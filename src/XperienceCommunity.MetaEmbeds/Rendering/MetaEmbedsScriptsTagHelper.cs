using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>
/// <c>&lt;meta-embeds-scripts /&gt;</c>: renders one <c>&lt;script async defer crossorigin="anonymous"&gt;</c> tag per SDK
/// the widgets on the current page claimed, in claim order. Only active when
/// <see cref="MetaEmbedsOptions.ScriptMode"/> is <see cref="EmbedScriptMode.TagHelper"/>; in the other modes it renders
/// nothing. Place it in the layout right before <c>&lt;/body&gt;</c>, after every widget zone has rendered, and add
/// <c>@addTagHelper *, XperienceCommunity.MetaEmbeds</c> to <c>_ViewImports.cshtml</c>.
/// </summary>
[HtmlTargetElement("meta-embeds-scripts", TagStructure = TagStructure.WithoutEndTag)]
public sealed class MetaEmbedsScriptsTagHelper : TagHelper
{
    private readonly IEmbedScriptRegistry scripts;
    private readonly IOptionsMonitor<MetaEmbedsOptions> options;

    /// <summary>Creates the tag helper.</summary>
    public MetaEmbedsScriptsTagHelper(IEmbedScriptRegistry scripts, IOptionsMonitor<MetaEmbedsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(options);
        this.scripts = scripts;
        this.options = options;
    }

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        // Never render the custom element itself, only the script tags (or nothing).
        output.TagName = null;
        output.Attributes.Clear();

        if (options.CurrentValue.ScriptMode != EmbedScriptMode.TagHelper)
        {
            output.SuppressOutput();
            return;
        }

        foreach (var script in scripts.Claimed)
        {
            output.Content.AppendHtml(EmbedScriptTag.Render(script));
        }
    }
}

/// <summary>Builds the SDK script tag the widget (Inline mode) and the tag helper (TagHelper mode) both emit.</summary>
internal static class EmbedScriptTag
{
    /// <summary><c>&lt;script async defer crossorigin="anonymous" src="…"&gt;&lt;/script&gt;</c> with the URL attribute-encoded.</summary>
    public static string Render(Uri scriptUri)
    {
        ArgumentNullException.ThrowIfNull(scriptUri);
        var src = HtmlEncoder.Default.Encode(scriptUri.AbsoluteUri);
        return $"<script async defer crossorigin=\"anonymous\" src=\"{src}\"></script>";
    }
}
