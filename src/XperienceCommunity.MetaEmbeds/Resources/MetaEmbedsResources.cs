using System.Resources;

using CMS.Base;
using CMS.Localization;

using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Resources;

// Builder: widget name, description, property labels and dropdown options resolved by the Page Builder dialogs.
// Server: the editor messages the widget resolves through ILocalizationService at render time.
[assembly: RegisterLocalizationResource(typeof(MetaEmbedsResources), LocalizationTarget.Builder, SystemContext.SYSTEM_CULTURE_NAME)]
[assembly: RegisterLocalizationResource(typeof(MetaEmbedsResources), LocalizationTarget.Server, SystemContext.SYSTEM_CULTURE_NAME)]

namespace XperienceCommunity.MetaEmbeds.Resources;

/// <summary>
/// Marker type for <c>MetaEmbedsResources.resx</c>. Xperience builds a <see cref="System.Resources.ResourceManager"/>
/// from this type, so its full name must equal the manifest resource base name
/// (<c>XperienceCommunity.MetaEmbeds.Resources.MetaEmbedsResources</c>). Every key starts with
/// <see cref="MetaEmbedsConstants.ResourcePrefix"/>.
/// </summary>
internal sealed class MetaEmbedsResources
{
    private MetaEmbedsResources()
    {
    }

    /// <summary>Direct access to the embedded strings (tests, diagnostics). Xperience reads them through its own manager.</summary>
    internal static ResourceManager ResourceManager { get; } =
        new(typeof(MetaEmbedsResources).FullName!, typeof(MetaEmbedsResources).Assembly);

    /// <summary>Resource key of the editor message for a failure kind, e.g. <c>xperiencecommunity.metaembeds.failure.notfound</c>.</summary>
    internal static string FailureKey(EmbedFailureKind kind) =>
        $"{MetaEmbedsConstants.ResourcePrefix}.failure.{kind.ToString().ToLowerInvariant()}";
}
