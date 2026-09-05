using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>
/// Default <see cref="IEmbedResolver"/>: normalises the source type, hands the request to the first
/// <see cref="IEmbedProvider"/> that supports it and turns every exception into a failed <see cref="EmbedResult"/>.
/// </summary>
public sealed class EmbedResolver : IEmbedResolver
{
    private static readonly EventId SourceTypeDefaultedEvent = new(1100, "METAEMBEDS_SOURCE_TYPE_DEFAULTED");
    private static readonly EventId UnknownSourceTypeEvent = new(1101, "METAEMBEDS_UNKNOWN_SOURCE_TYPE");
    private static readonly EventId ProviderFailedEvent = new(1102, "METAEMBEDS_PROVIDER_FAILED");
    private static readonly EventId NoProviderEvent = new(1103, "METAEMBEDS_NO_PROVIDER");

    private static readonly TimeSpan UnknownSourceTypeLogPeriod = TimeSpan.FromHours(1);
    private static readonly TimeSpan ProviderFailureLogPeriod = TimeSpan.FromMinutes(10);

    private readonly IReadOnlyList<IEmbedProvider> providers;
    private readonly ILogger<EmbedResolver> logger;
    private readonly LogThrottle throttle;

    /// <summary>Creates the resolver over every registered provider, in registration order.</summary>
    public EmbedResolver(IEnumerable<IEmbedProvider> providers, ILogger<EmbedResolver> logger)
        : this(providers, logger, TimeProvider.System)
    {
    }

    internal EmbedResolver(IEnumerable<IEmbedProvider> providers, ILogger<EmbedResolver> logger, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(providers);
        this.providers = providers.Where(p => p is not null).ToList();
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        throttle = new LogThrottle(timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc />
    public async Task<EmbedResult> ResolveAsync(EmbedRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return EmbedResult.Failed(EmbedFailureKind.NotConfigured, "The request was null.");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.Input))
            {
                return EmbedResult.Failed(EmbedFailureKind.NotConfigured);
            }

            var sourceType = EmbedSourceTypes.Normalize(request.SourceType);
            if (string.IsNullOrWhiteSpace(request.SourceType))
            {
                logger.LogDebug(SourceTypeDefaultedEvent, "Embed request without a source type; using '{SourceType}'.", sourceType);
            }

            var normalizedRequest = WithSourceType(request, sourceType);
            var provider = FindProvider(normalizedRequest);

            if (provider is null && !string.Equals(sourceType, EmbedSourceTypes.Post, StringComparison.Ordinal))
            {
                // Unknown (or not yet registered) source type: degrade to the v1 default rather than render nothing.
                if (throttle.ShouldLog($"source-type:{sourceType}", UnknownSourceTypeLogPeriod))
                {
                    logger.LogWarning(UnknownSourceTypeEvent,
                        "No embed provider supports source type '{SourceType}'; falling back to '{Fallback}'.",
                        sourceType, EmbedSourceTypes.Post);
                }

                normalizedRequest = WithSourceType(request, EmbedSourceTypes.Post);
                provider = FindProvider(normalizedRequest);
            }

            if (provider is null)
            {
                logger.LogDebug(NoProviderEvent, "No embed provider supports source type '{SourceType}'.", normalizedRequest.SourceType);
                return EmbedResult.Failed(
                    EmbedFailureKind.UnsupportedInput,
                    $"No embed provider supports source type '{normalizedRequest.SourceType}'.");
            }

            var result = await provider.ResolveAsync(normalizedRequest, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                LogProviderFailure(provider, $"Provider '{provider.Name}' returned null.", null);
                return EmbedResult.Failed(EmbedFailureKind.Internal, $"Provider '{provider.Name}' returned null.");
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return EmbedResult.Failed(EmbedFailureKind.Transient, "The request was cancelled.");
        }
        catch (OperationCanceledException ex)
        {
            return EmbedResult.Failed(EmbedFailureKind.Transient, "The operation was cancelled or timed out.", ex);
        }
        catch (Exception ex)
        {
            LogProviderFailure(null, ex.Message, ex);
            return EmbedResult.Failed(EmbedFailureKind.Internal, ex.Message, ex);
        }
    }

    private IEmbedProvider? FindProvider(EmbedRequest request)
    {
        foreach (var provider in providers)
        {
            if (provider.Supports(request))
            {
                return provider;
            }
        }

        return null;
    }

    private void LogProviderFailure(IEmbedProvider? provider, string message, Exception? exception)
    {
        var key = $"provider-failure:{exception?.GetType().FullName}:{message}";
        if (throttle.ShouldLog(key, ProviderFailureLogPeriod))
        {
            logger.LogError(ProviderFailedEvent, exception,
                "Embed provider {Provider} failed: {Message}", provider?.Name ?? "(unknown)", message);
        }
    }

    private static EmbedRequest WithSourceType(EmbedRequest request, string sourceType) =>
        string.Equals(request.SourceType, sourceType, StringComparison.Ordinal)
            ? request
            : new EmbedRequest { SourceType = sourceType, Input = request.Input, Culture = request.Culture };

    /// <summary>Small in-process "log at most once per period per key" memo. Bounded so garbage keys cannot grow it forever.</summary>
    private sealed class LogThrottle
    {
        private const int MaxEntries = 1000;
        private readonly ConcurrentDictionary<string, DateTimeOffset> lastLogged = new(StringComparer.Ordinal);
        private readonly TimeProvider timeProvider;

        public LogThrottle(TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
        }

        public bool ShouldLog(string key, TimeSpan period)
        {
            var now = timeProvider.GetUtcNow();
            if (lastLogged.Count >= MaxEntries && !lastLogged.ContainsKey(key))
            {
                lastLogged.Clear();
            }

            while (true)
            {
                if (!lastLogged.TryGetValue(key, out var previous))
                {
                    if (lastLogged.TryAdd(key, now))
                    {
                        return true;
                    }

                    continue;
                }

                if (now - previous < period)
                {
                    return false;
                }

                if (lastLogged.TryUpdate(key, now, previous))
                {
                    return true;
                }
            }
        }
    }
}
