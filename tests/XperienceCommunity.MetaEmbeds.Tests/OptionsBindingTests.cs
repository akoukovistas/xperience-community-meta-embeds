using NUnit.Framework;

using System.Net.Http.Headers;
using System.Text;

using CMS.Helpers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using XperienceCommunity.MetaEmbeds.Caching;
using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Providers.OEmbed;
using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class OptionsBindingTests
{
    /// <summary>The appsettings shape from the plan (section 10), with credentials and a non-default script mode filled in.</summary>
    private const string PlanJson = """
        {
          "XperienceCommunityMetaEmbeds": {
            "Credentials": { "AppId": "123456", "ClientToken": "abcdef", "AccessToken": "" },
            "GraphApiVersion": "v25.0",
            "FacebookSdkLocale": "en_US",
            "ScriptMode": "TagHelper",
            "SuccessCacheDuration": "12:00:00",
            "NotFoundCacheDuration": "01:00:00",
            "TransientFailureCacheDuration": "00:02:00"
          }
        }
        """;

    private const string EverythingNonDefaultJson = """
        {
          "XperienceCommunityMetaEmbeds": {
            "Credentials": { "AppId": "1", "ClientToken": "2", "AccessToken": "EAAB.explicit" },
            "GraphApiVersion": "v26.0",
            "FacebookSdkLocale": "cs_CZ",
            "ScriptMode": "None",
            "SuccessCacheDuration": "06:30:00",
            "NotFoundCacheDuration": "00:15:00",
            "TransientFailureCacheDuration": "00:00:30",
            "HttpTimeout": "00:00:03",
            "MaxResponseBytes": 32768
          }
        }
        """;

    [Test]
    public void Bind_PlanJson_ProducesExpectedOptions()
    {
        using var provider = BuildProvider(PlanJson);

        var options = provider.GetRequiredService<IOptions<MetaEmbedsOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(options.Credentials.AppId, Is.EqualTo("123456"));
            Assert.That(options.Credentials.ClientToken, Is.EqualTo("abcdef"));
            Assert.That(options.Credentials.AccessToken, Is.Empty);
            Assert.That(options.Credentials.IsEmpty, Is.False);
            Assert.That(options.Credentials.ResolveAccessToken(), Is.EqualTo("123456|abcdef"));
            Assert.That(options.GraphApiVersion, Is.EqualTo("v25.0"));
            Assert.That(options.FacebookSdkLocale, Is.EqualTo("en_US"));
            Assert.That(options.ScriptMode, Is.EqualTo(EmbedScriptMode.TagHelper));
            Assert.That(options.SuccessCacheDuration, Is.EqualTo(TimeSpan.FromHours(12)));
            Assert.That(options.NotFoundCacheDuration, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(options.TransientFailureCacheDuration, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(options.HttpTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)), "not in the JSON, keeps its default");
            Assert.That(options.MaxResponseBytes, Is.EqualTo(64 * 1024), "not in the JSON, keeps its default");
        });
    }

    [Test]
    public void Bind_EveryMemberNonDefault_IsRespected()
    {
        using var provider = BuildProvider(EverythingNonDefaultJson);

        var options = provider.GetRequiredService<IOptionsMonitor<MetaEmbedsOptions>>().CurrentValue;

        Assert.Multiple(() =>
        {
            Assert.That(options.Credentials.AccessToken, Is.EqualTo("EAAB.explicit"));
            Assert.That(options.Credentials.ResolveAccessToken(), Is.EqualTo("EAAB.explicit"), "an explicit token wins over AppId|ClientToken");
            Assert.That(options.GraphApiVersion, Is.EqualTo("v26.0"));
            Assert.That(options.FacebookSdkLocale, Is.EqualTo("cs_CZ"));
            Assert.That(options.ScriptMode, Is.EqualTo(EmbedScriptMode.None));
            Assert.That(options.SuccessCacheDuration, Is.EqualTo(new TimeSpan(6, 30, 0)));
            Assert.That(options.NotFoundCacheDuration, Is.EqualTo(TimeSpan.FromMinutes(15)));
            Assert.That(options.TransientFailureCacheDuration, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.HttpTimeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(options.MaxResponseBytes, Is.EqualTo(32768));
        });
    }

    [Test]
    public void Bind_NoSection_KeepsDefaults()
    {
        using var provider = BuildProvider("{}");

        var options = provider.GetRequiredService<IOptions<MetaEmbedsOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(options.Credentials.IsEmpty, Is.True);
            Assert.That(options.Credentials.ResolveAccessToken(), Is.Null);
            Assert.That(options.GraphApiVersion, Is.EqualTo("v25.0"));
            Assert.That(options.FacebookSdkLocale, Is.EqualTo("en_US"));
            Assert.That(options.ScriptMode, Is.EqualTo(EmbedScriptMode.Inline));
            Assert.That(options.SuccessCacheDuration, Is.EqualTo(TimeSpan.FromHours(12)));
            Assert.That(options.NotFoundCacheDuration, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(options.TransientFailureCacheDuration, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(options.HttpTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(options.MaxResponseBytes, Is.EqualTo(65536));
        });
    }

    [Test]
    public void Bind_OnlyAppIdWithoutClientToken_StaysTokenless()
    {
        using var provider = BuildProvider("""{ "XperienceCommunityMetaEmbeds": { "Credentials": { "AppId": "123456" } } }""");

        var options = provider.GetRequiredService<IOptions<MetaEmbedsOptions>>().Value;

        Assert.That(options.Credentials.IsEmpty, Is.True);
        Assert.That(options.Credentials.ResolveAccessToken(), Is.Null);
    }

    [Test]
    public void ActionOverload_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddXperienceCommunityMetaEmbeds(o =>
        {
            o.ScriptMode = EmbedScriptMode.TagHelper;
            o.SuccessCacheDuration = TimeSpan.FromMinutes(90);
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<MetaEmbedsOptions>>().Value;

        Assert.That(options.ScriptMode, Is.EqualTo(EmbedScriptMode.TagHelper));
        Assert.That(options.SuccessCacheDuration, Is.EqualTo(TimeSpan.FromMinutes(90)));
    }

    [Test]
    public void NamedHttpClient_FollowsOptions()
    {
        using var provider = BuildProvider(EverythingNonDefaultJson);

        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(MetaEmbedsConstants.HttpClientName);

        Assert.Multiple(() =>
        {
            Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(client.MaxResponseContentBufferSize, Is.EqualTo(32768));
            Assert.That(client.DefaultRequestHeaders.Accept, Is.EqualTo(new[] { new MediaTypeWithQualityHeaderValue("application/json") }));
            Assert.That(client.DefaultRequestHeaders.AcceptLanguage.Select(l => l.Value), Is.EqualTo(new[] { "cs-CZ" }));
            Assert.That(client.DefaultRequestHeaders.UserAgent.ToString(), Does.StartWith("XperienceCommunity.MetaEmbeds/"));
        });
    }

    [Test]
    public void ServiceGraph_ResolvesWithDefaultImplementations()
    {
        using var provider = BuildProvider(PlanJson, services => services.AddSingleton(Substitute.For<IProgressiveCache>()));

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<IEmbedResolver>(), Is.InstanceOf<EmbedResolver>());
            Assert.That(provider.GetServices<IEmbedProvider>().Select(p => p.GetType()), Is.EqualTo(new[] { typeof(MetaOEmbedProvider) }));
            Assert.That(provider.GetRequiredService<IEmbedUrlMatcher>(), Is.InstanceOf<EmbedUrlMatcher>());
            Assert.That(provider.GetRequiredService<IEmbedResultCache>(), Is.InstanceOf<ProgressiveEmbedResultCache>());
            Assert.That(provider.GetRequiredService<IEmbedHtmlSanitizer>(), Is.InstanceOf<EmbedHtmlSanitizer>());
            Assert.That(provider.GetRequiredService<IMetaOEmbedEndpointRegistry>().Endpoints.Select(e => e.Key),
                Is.EqualTo(new[] { "threads", "instagram", "facebook-post", "facebook-video" }));
            var firstResolve = provider.GetRequiredService<IEmbedResolver>();
            Assert.That(provider.GetRequiredService<IEmbedResolver>(), Is.SameAs(firstResolve), "singleton");
        });
    }

    [Test]
    public void Registration_IsIdempotent()
    {
        var configuration = BuildConfiguration(PlanJson);
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IProgressiveCache>());
        services.AddXperienceCommunityMetaEmbeds(configuration);
        services.AddXperienceCommunityMetaEmbeds(configuration);
        services.AddXperienceCommunityMetaEmbeds(o => o.ScriptMode = EmbedScriptMode.None);
        using var provider = services.BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetServices<IEmbedProvider>().Count(), Is.EqualTo(1));
            Assert.That(provider.GetServices<IEmbedResolver>().Count(), Is.EqualTo(1));
            Assert.That(provider.GetServices<IEmbedResultCache>().Count(), Is.EqualTo(1));
            Assert.That(provider.GetRequiredService<IOptions<MetaEmbedsOptions>>().Value.ScriptMode, Is.EqualTo(EmbedScriptMode.None), "later configure actions still apply");
        });
    }

    [Test]
    public void ConsumerRegistrationBeforeOurs_Wins()
    {
        var custom = Substitute.For<IEmbedUrlMatcher>();
        var services = new ServiceCollection();
        services.AddSingleton(custom);
        services.AddXperienceCommunityMetaEmbeds(BuildConfiguration(PlanJson));
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IEmbedUrlMatcher>(), Is.SameAs(custom));
    }

    private static IConfiguration BuildConfiguration(string json) =>
        new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

    private static ServiceProvider BuildProvider(string json, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        extra?.Invoke(services);
        services.AddXperienceCommunityMetaEmbeds(BuildConfiguration(json));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
