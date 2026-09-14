using System.Net.Http.Headers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using XperienceCommunity.MetaEmbeds.Caching;
using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Providers.OEmbed;
using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds;

/// <summary>
/// Registration entry points. Calling them is optional: the package's Xperience module registers the same services
/// with <c>TryAdd</c> semantics during application start, so a consumer's own registration placed before
/// <c>AddKentico()</c>, or a <c>Replace</c> after it, always wins. The same holds for options configured in code,
/// which run after the configuration binder whichever order the calls are made in.
/// </summary>
public static class MetaEmbedsServiceCollectionExtensions
{
    /// <summary>Binds <see cref="MetaEmbedsOptions"/> from the <c>XperienceCommunityMetaEmbeds</c> section and registers the services.</summary>
    public static IServiceCollection AddXperienceCommunityMetaEmbeds(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MetaEmbedsOptions>().Bind(configuration.GetSection(MetaEmbedsOptions.SectionName));
        return services.TryAddMetaEmbedsServices();
    }

    /// <summary>
    /// Configures <see cref="MetaEmbedsOptions"/> in code and registers the services. The callback runs as a
    /// <c>PostConfigure</c> action, so it wins over the <c>XperienceCommunityMetaEmbeds</c> section however the two
    /// are ordered - the module's binder is registered during <c>AddKentico()</c> and would otherwise run last.
    /// </summary>
    public static IServiceCollection AddXperienceCommunityMetaEmbeds(this IServiceCollection services, Action<MetaEmbedsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<MetaEmbedsOptions>().PostConfigure(configure);
        return services.TryAddMetaEmbedsServices();
    }

    /// <summary>
    /// Zero-config path used by the module: binds the options from whatever <see cref="IConfiguration"/> the host
    /// registers (resolved lazily at runtime) and registers the services.
    /// </summary>
    internal static IServiceCollection AddXperienceCommunityMetaEmbedsFromHostConfiguration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MetaEmbedsOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection(MetaEmbedsOptions.SectionName).Bind(options));
        return services.TryAddMetaEmbedsServices();
    }

    /// <summary>All default registrations, each with <c>TryAdd</c> semantics. Safe to call more than once.</summary>
    internal static IServiceCollection TryAddMetaEmbedsServices(this IServiceCollection services)
    {
        services.AddOptions<MetaEmbedsOptions>();
        services.AddHttpContextAccessor();

        services.TryAddSingleton<IEmbedResolver, EmbedResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEmbedProvider, MetaOEmbedProvider>());
        services.TryAddSingleton<IMetaOEmbedEndpointRegistry>(_ => new MetaOEmbedEndpointRegistry(MetaOEmbedEndpoints.Default));
        services.TryAddSingleton<IEmbedUrlMatcher, EmbedUrlMatcher>();
        services.TryAddSingleton<IEmbedHtmlSanitizer, EmbedHtmlSanitizer>();
        services.TryAddSingleton<IEmbedResultCache, ProgressiveEmbedResultCache>();
        services.TryAddScoped<IEmbedScriptRegistry, EmbedScriptRegistry>();

        // AddHttpClient is not idempotent (every call appends configuration), so guard it with a marker.
        if (!services.Any(d => d.ServiceType == typeof(MetaEmbedsHttpClientMarker)))
        {
            services.AddSingleton<MetaEmbedsHttpClientMarker>();
            services.AddHttpClient(MetaEmbedsConstants.HttpClientName)
                .ConfigureHttpClient((provider, client) =>
                {
                    var options = provider.GetRequiredService<IOptionsMonitor<MetaEmbedsOptions>>().CurrentValue;
                    client.Timeout = options.HttpTimeout > TimeSpan.Zero ? options.HttpTimeout : TimeSpan.FromSeconds(5);
                    client.MaxResponseContentBufferSize = options.MaxResponseBytes > 0 ? options.MaxResponseBytes : 64 * 1024;
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.AcceptLanguage.Clear();
                    client.DefaultRequestHeaders.AcceptLanguage.Add(
                        new StringWithQualityHeaderValue(ToLanguageTag(options.EffectiveFacebookSdkLocale)));
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue(MetaEmbedsConstants.ProductName, MetaEmbedsConstants.Version));
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                });
        }

        return services;
    }

    /// <summary><c>en_US</c> → <c>en-US</c>; falls back to <c>en-US</c> for anything that is not a plausible tag.</summary>
    internal static string ToLanguageTag(string? sdkLocale)
    {
        if (string.IsNullOrWhiteSpace(sdkLocale))
        {
            return "en-US";
        }

        var tag = sdkLocale.Trim().Replace('_', '-');
        return tag.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && tag.Length <= 35 ? tag : "en-US";
    }

    /// <summary>Registered once so repeated <c>TryAddMetaEmbedsServices</c> calls do not re-run <c>AddHttpClient</c>.</summary>
#pragma warning disable S2094 // Intentionally empty: the type's presence in the service collection is the information.
    private sealed class MetaEmbedsHttpClientMarker;
#pragma warning restore S2094
}
