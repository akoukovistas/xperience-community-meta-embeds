using CMS.Core;
using CMS.Helpers;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Caching;
using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Providers.OEmbed;
using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class ServiceRegistrationTests
{
    /// <summary>
    /// The host services the package's own registrations depend on but never register themselves: logging, and the
    /// Xperience services the default implementations take in their constructors (substituted here).
    /// </summary>
    private static ServiceCollection HostServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IProgressiveCache>());
        services.AddSingleton(Substitute.For<ILocalizationService>());
        return services;
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => $"{MetaEmbedsOptions.SectionName}:{v.Key}", v => (string?)v.Value))
            .Build();

    [Test]
    public void AddXperienceCommunityMetaEmbeds_ResolvesEverySeam()
    {
        var services = HostServices();
        services.AddXperienceCommunityMetaEmbeds(o => o.ScriptMode = EmbedScriptMode.TagHelper);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.Multiple(() =>
        {
            Assert.That(sp.GetService<IEmbedResolver>(), Is.InstanceOf<EmbedResolver>());
            Assert.That(sp.GetServices<IEmbedProvider>().ToList(), Has.Count.EqualTo(1).And.All.InstanceOf<MetaOEmbedProvider>());
            Assert.That(sp.GetService<IMetaOEmbedEndpointRegistry>()?.Endpoints, Has.Count.EqualTo(4));
            Assert.That(sp.GetService<IEmbedUrlMatcher>(), Is.InstanceOf<EmbedUrlMatcher>());
            Assert.That(sp.GetService<IEmbedHtmlSanitizer>(), Is.InstanceOf<EmbedHtmlSanitizer>());
            Assert.That(sp.GetService<IEmbedResultCache>(), Is.InstanceOf<ProgressiveEmbedResultCache>());
            Assert.That(sp.GetService<IEmbedScriptRegistry>(), Is.InstanceOf<EmbedScriptRegistry>());
            Assert.That(sp.GetService<IHttpContextAccessor>(), Is.Not.Null);
            Assert.That(sp.GetRequiredService<IOptionsMonitor<MetaEmbedsOptions>>().CurrentValue.ScriptMode, Is.EqualTo(EmbedScriptMode.TagHelper));
        });
    }

    [Test]
    public void ScriptRegistry_IsScoped_AndSingletonsAreShared()
    {
        var services = HostServices();
        services.AddXperienceCommunityMetaEmbeds(_ => { });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var firstResolve = scope1.ServiceProvider.GetRequiredService<IEmbedScriptRegistry>();
        var secondResolve = scope1.ServiceProvider.GetRequiredService<IEmbedScriptRegistry>();

        Assert.That(secondResolve, Is.SameAs(firstResolve), "scoped: one instance per scope");
        Assert.That(scope2.ServiceProvider.GetRequiredService<IEmbedScriptRegistry>(), Is.Not.SameAs(firstResolve), "scoped: a different instance in another scope");
        Assert.That(scope1.ServiceProvider.GetRequiredService<IEmbedResolver>(), Is.SameAs(scope2.ServiceProvider.GetRequiredService<IEmbedResolver>()));
    }

    [Test]
    public void NamedHttpClient_IsConfiguredFromOptions()
    {
        var services = HostServices();
        services.AddXperienceCommunityMetaEmbeds(o =>
        {
            o.HttpTimeout = TimeSpan.FromSeconds(7);
            o.FacebookSdkLocale = "de_DE";
        });
        using var provider = services.BuildServiceProvider();

        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(MetaEmbedsConstants.HttpClientName);

        Assert.Multiple(() =>
        {
            Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
            Assert.That(client.DefaultRequestHeaders.Accept.Select(a => a.MediaType), Is.EqualTo(new[] { "application/json" }).AsCollection);
            Assert.That(client.DefaultRequestHeaders.AcceptLanguage.Select(l => l.Value), Is.EqualTo(new[] { "de-DE" }).AsCollection);
            Assert.That(client.DefaultRequestHeaders.UserAgent.ToString(), Does.StartWith("XperienceCommunity.MetaEmbeds/"));
        });
    }

    [Test]
    public void CallingTheExtensionTwice_DoesNotDuplicateProvidersOrHttpClientConfiguration()
    {
        var services = HostServices();
        services.AddXperienceCommunityMetaEmbeds(o => o.ScriptMode = EmbedScriptMode.TagHelper);

        var httpClientConfigurations = CountHttpClientConfigurations(services);
        var descriptorsAfterFirstCall = CountNonOptionDescriptors(services);

        services.AddXperienceCommunityMetaEmbeds(_ => { });
        services.AddXperienceCommunityMetaEmbeds(Configuration());

        Assert.Multiple(() =>
        {
            Assert.That(CountHttpClientConfigurations(services), Is.EqualTo(httpClientConfigurations), "AddHttpClient configuration appended more than once");
            Assert.That(CountNonOptionDescriptors(services), Is.EqualTo(descriptorsAfterFirstCall), "only MetaEmbedsOptions configurators / change tokens may be added by repeated calls");
            Assert.That(services.Count(d => d.ServiceType == typeof(IEmbedProvider)), Is.EqualTo(1));
            Assert.That(services.Count(d => d.ServiceType == typeof(IEmbedResolver)), Is.EqualTo(1));
        });

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetServices<IEmbedProvider>().Count(), Is.EqualTo(1));
        Assert.That(provider.GetRequiredService<IOptionsMonitor<MetaEmbedsOptions>>().CurrentValue.ScriptMode, Is.EqualTo(EmbedScriptMode.TagHelper), "the first configuration still applies");
    }

    [Test]
    public void ConsumerRegistrationPlacedFirst_Wins()
    {
        var services = HostServices();
        var custom = Substitute.For<IEmbedHtmlSanitizer>();
        services.AddSingleton(custom);
        services.AddXperienceCommunityMetaEmbeds(_ => { });
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IEmbedHtmlSanitizer>(), Is.SameAs(custom));
    }

    [Test]
    public void AddXperienceCommunityMetaEmbeds_WithConfiguration_BindsTheSection()
    {
        var services = HostServices();
        services.AddXperienceCommunityMetaEmbeds(Configuration(
            ("ScriptMode", "None"),
            ("FacebookSdkLocale", "cs_CZ"),
            ("GraphApiVersion", "v26.0"),
            ("SuccessCacheDuration", "01:00:00"),
            ("Credentials:AppId", "123"),
            ("Credentials:ClientToken", "abc")));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<MetaEmbedsOptions>>().CurrentValue;

        Assert.Multiple(() =>
        {
            Assert.That(options.ScriptMode, Is.EqualTo(EmbedScriptMode.None));
            Assert.That(options.FacebookSdkLocale, Is.EqualTo("cs_CZ"));
            Assert.That(options.GraphApiVersion, Is.EqualTo("v26.0"));
            Assert.That(options.SuccessCacheDuration, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(options.Credentials.IsEmpty, Is.False);
            Assert.That(options.Credentials.ResolveAccessToken(), Is.EqualTo("123|abc"));
        });
    }

    [Test]
    public void Module_PreInit_RegistersServicesAndBindsOptionsFromTheHostConfiguration()
    {
        var services = HostServices();
        services.AddSingleton(Configuration(("ScriptMode", "TagHelper"), ("HttpTimeout", "00:00:03")));

        new MetaEmbedsModule().PreInit(new ModulePreInitParameters { Services = services });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var options = provider.GetRequiredService<IOptionsMonitor<MetaEmbedsOptions>>().CurrentValue;

        Assert.Multiple(() =>
        {
            Assert.That(scope.ServiceProvider.GetService<IEmbedResolver>(), Is.InstanceOf<EmbedResolver>());
            Assert.That(scope.ServiceProvider.GetService<IEmbedScriptRegistry>(), Is.InstanceOf<EmbedScriptRegistry>());
            Assert.That(options.ScriptMode, Is.EqualTo(EmbedScriptMode.TagHelper));
            Assert.That(options.HttpTimeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
        });
    }

    [Test]
    public void Module_PreInit_AfterExplicitRegistration_DoesNotDuplicate()
    {
        var services = HostServices();
        services.AddSingleton(Configuration());
        services.AddXperienceCommunityMetaEmbeds(o => o.ScriptMode = EmbedScriptMode.None);
        var httpClientConfigurations = CountHttpClientConfigurations(services);

        new MetaEmbedsModule().PreInit(new ModulePreInitParameters { Services = services });

        Assert.That(CountHttpClientConfigurations(services), Is.EqualTo(httpClientConfigurations));
        Assert.That(services.Count(d => d.ServiceType == typeof(IEmbedProvider)), Is.EqualTo(1));
    }

    [Test]
    public void Module_PreInit_WithoutServices_DoesNotThrow()
    {
        Assert.That(() => new MetaEmbedsModule().PreInit(new ModulePreInitParameters()), Throws.Nothing);
    }

    private static int CountHttpClientConfigurations(IServiceCollection services) =>
        services.Count(d => d.ServiceType == typeof(IConfigureOptions<HttpClientFactoryOptions>));

    /// <summary>Every descriptor except the ones each options call legitimately appends (a configurator and, for <c>Bind</c>, a change-token source).</summary>
    private static int CountNonOptionDescriptors(IServiceCollection services) =>
        services.Count(d =>
            d.ServiceType != typeof(IConfigureOptions<MetaEmbedsOptions>)
            && d.ServiceType != typeof(IOptionsChangeTokenSource<MetaEmbedsOptions>));
}
