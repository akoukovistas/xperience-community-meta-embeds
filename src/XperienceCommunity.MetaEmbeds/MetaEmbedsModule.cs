using CMS;
using CMS.Core;
using CMS.DataEngine;

using XperienceCommunity.MetaEmbeds;

[assembly: RegisterModule(typeof(MetaEmbedsModule))]

namespace XperienceCommunity.MetaEmbeds;

/// <summary>
/// Zero-configuration entry point. Xperience discovers this module through <c>[assembly: AssemblyDiscoverable]</c> and
/// calls <see cref="OnPreInit(ModulePreInitParameters)"/> before the service container is built, which is where the
/// package registers its services with <c>TryAdd</c> semantics and binds <see cref="MetaEmbedsOptions"/> from the
/// host's <c>IConfiguration</c>. Calling <c>AddXperienceCommunityMetaEmbeds</c> yourself is therefore optional.
/// </summary>
public sealed class MetaEmbedsModule : Module
{
    /// <summary>Module name reported to Xperience.</summary>
    public const string ModuleName = "XperienceCommunity.MetaEmbeds";

    /// <summary>Creates the module.</summary>
    public MetaEmbedsModule()
        : base(ModuleName)
    {
    }

    /// <inheritdoc />
    protected override void OnPreInit(ModulePreInitParameters parameters)
    {
        base.OnPreInit(parameters);

        // Services is null when the module is pre-initialised outside a DI host, for example by some test
        // harnesses. Consumers in that situation register the services with the explicit extension method.
        parameters?.Services?.AddXperienceCommunityMetaEmbedsFromHostConfiguration();
    }
}
