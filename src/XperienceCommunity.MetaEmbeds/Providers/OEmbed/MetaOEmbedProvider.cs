using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.MetaEmbeds.Caching;
using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>
/// The one v1 <see cref="IEmbedProvider"/>: validates the URL, matches it to an endpoint, calls Meta's oEmbed API
/// (tokenless unless credentials are configured), sanitises the markup and caches the outcome per policy.
/// Every network, HTTP and JSON problem becomes a failed <see cref="EmbedResult"/>; the method only throws for
/// caller cancellation and programming errors, which <see cref="EmbedResolver"/> maps to <see cref="EmbedFailureKind.Internal"/>.
/// </summary>
public sealed class MetaOEmbedProvider : IEmbedProvider
{
    private const int DefaultMaxResponseBytes = 64 * 1024;
    private const int MediaNotFoundCode = 24;
    private const int MediaNotFoundSubcode = 2207045;
    private const int InvalidAccessTokenCode = 190;
    private const string RedactedToken = "***";

    private static readonly EventId FetchedEvent = new(1000, "METAEMBEDS_FETCHED");
    private static readonly EventId NotFoundEvent = new(1001, "METAEMBEDS_NOT_FOUND");
    private static readonly EventId RejectedEvent = new(1002, "METAEMBEDS_REJECTED");
    private static readonly EventId TransientEvent = new(1003, "METAEMBEDS_TRANSIENT");
    private static readonly EventId UnexpectedMarkupEvent = new(1004, "METAEMBEDS_UNEXPECTED_MARKUP");
    private static readonly EventId InternalEvent = new(1005, "METAEMBEDS_INTERNAL");
    private static readonly EventId InvalidOptionEvent = new(1006, "METAEMBEDS_INVALID_OPTION");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMetaOEmbedEndpointRegistry endpoints;
    private readonly IEmbedUrlMatcher urlMatcher;
    private readonly IEmbedHtmlSanitizer sanitizer;
    private readonly IEmbedResultCache cache;
    private readonly IOptionsMonitor<MetaEmbedsOptions> options;
    private readonly ILogger<MetaOEmbedProvider> logger;

    /// <summary>Option values already reported as unusable, so a misconfiguration is logged once rather than per render.</summary>
    private readonly ConcurrentDictionary<string, byte> reportedInvalidOptions = new(StringComparer.Ordinal);

    /// <summary>Creates the provider. All dependencies are registered by <c>AddXperienceCommunityMetaEmbeds</c>.</summary>
    public MetaOEmbedProvider(
        IHttpClientFactory httpClientFactory,
        IMetaOEmbedEndpointRegistry endpoints,
        IEmbedUrlMatcher urlMatcher,
        IEmbedHtmlSanitizer sanitizer,
        IEmbedResultCache cache,
        IOptionsMonitor<MetaEmbedsOptions> options,
        ILogger<MetaOEmbedProvider> logger)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
        this.urlMatcher = urlMatcher ?? throw new ArgumentNullException(nameof(urlMatcher));
        this.sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => MetaEmbedsConstants.OEmbedProviderName;

    /// <summary>
    /// True for the <see cref="EmbedSourceTypes.Post"/> source type, whatever the input looks like. URL classification
    /// happens in <see cref="ResolveAsync"/> so the resolver can report <see cref="EmbedFailureKind.InvalidInput"/>
    /// versus <see cref="EmbedFailureKind.UnsupportedInput"/> instead of silently having no provider.
    /// </summary>
    public bool Supports(EmbedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return string.Equals(EmbedSourceTypes.Normalize(request.SourceType), EmbedSourceTypes.Post, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public Task<EmbedResult> ResolveAsync(EmbedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return Task.FromResult(EmbedResult.Failed(EmbedFailureKind.NotConfigured));
        }

        if (!urlMatcher.TryParse(request.Input, out var normalized, out var reason))
        {
            return Task.FromResult(EmbedResult.Failed(EmbedFailureKind.InvalidInput, $"Input rejected: {reason}."));
        }

        var endpoint = endpoints.Match(normalized);
        if (endpoint is null)
        {
            return Task.FromResult(EmbedResult.Failed(
                EmbedFailureKind.UnsupportedInput,
                $"No oEmbed endpoint accepts URLs on host '{normalized.Host}' with this path shape."));
        }

        var current = options.CurrentValue;
        WarnIfIgnored(nameof(MetaEmbedsOptions.GraphApiVersion), current.GraphApiVersion, current.EffectiveGraphApiVersion);
        WarnIfIgnored(nameof(MetaEmbedsOptions.FacebookSdkLocale), current.FacebookSdkLocale, current.EffectiveFacebookSdkLocale);

        var parameters = OEmbedRequestParameters.For(endpoint, request.Parameters);
        var key = new EmbedCacheKey(
            endpoint.Key,
            urlMatcher.ToCacheForm(normalized),
            Authenticated: !current.Credentials.IsEmpty,
            current.EffectiveGraphApiVersion,
            Variant: parameters.CacheVariant);
        var policy = EmbedCachePolicy.For(current, endpoint.Key);

        return cache.GetOrAddAsync(key, ct => FetchAsync(endpoint, normalized, current, parameters, ct), policy, cancellationToken);
    }

    /// <summary>
    /// Both option values are interpolated into URLs, so an unusable one falls back to the package default rather than
    /// changing the host a call goes to. That fallback is silent by design in the URL builders, so say so once here.
    /// </summary>
    private void WarnIfIgnored(string option, string? configured, string effective)
    {
        if (string.Equals(configured, effective, StringComparison.Ordinal))
        {
            return;
        }

        // The value comes from appsettings, so it is operator-controlled rather than untrusted - but it still ends up
        // in a log line, so bound its length.
        var reported = configured is null ? "(null)" : configured[..Math.Min(configured.Length, 64)];
        if (reportedInvalidOptions.TryAdd($"{option}|{reported}", 0))
        {
            logger.LogWarning(
                InvalidOptionEvent,
                "MetaEmbeds option {Option} has the unusable value {Configured}; using {Effective} instead. URLs are built from the fallback.",
                option,
                reported,
                effective);
        }
    }

    /// <summary>One HTTP round trip to Meta plus sanitising. Only caller cancellation escapes as an exception.</summary>
    internal Task<EmbedResult> FetchAsync(MetaOEmbedEndpoint endpoint, Uri normalized, MetaEmbedsOptions current, CancellationToken cancellationToken) =>
        FetchAsync(endpoint, normalized, current, OEmbedRequestParameters.None, cancellationToken);

    /// <summary>One HTTP round trip to Meta plus sanitising, with extra oEmbed parameters. Only caller cancellation escapes as an exception.</summary>
    internal async Task<EmbedResult> FetchAsync(MetaOEmbedEndpoint endpoint, Uri normalized, MetaEmbedsOptions current, OEmbedRequestParameters parameters, CancellationToken cancellationToken)
    {
        var token = current.Credentials.ResolveAccessToken();
        var requestUri = BuildRequestUri(endpoint, normalized, current, token, parameters);
        var redactedUri = Redact(requestUri.AbsoluteUri, token);

        try
        {
            logger.LogDebug(FetchedEvent, "Meta oEmbed {Endpoint}: requesting {RequestUri}", endpoint.Key, redactedUri);

            var client = httpClientFactory.CreateClient(MetaEmbedsConstants.HttpClientName);
            using var response = await client
                .GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var status = (int)response.StatusCode;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            var maxBytes = current.MaxResponseBytes > 0 ? current.MaxResponseBytes : DefaultMaxResponseBytes;

            if (status == 429 || status >= 500 || status is >= 300 and < 400)
            {
                return Transient(endpoint, normalized, $"HTTP {status} from Meta.", null);
            }

            var body = await ReadBodyAsync(response.Content, maxBytes, cancellationToken).ConfigureAwait(false);
            if (body is null)
            {
                return Transient(endpoint, normalized, $"Response body exceeded {maxBytes} bytes (HTTP {status}).", null);
            }

            if (!IsJson(mediaType))
            {
                return Transient(endpoint, normalized, $"Unexpected content type '{mediaType ?? "(none)"}' (HTTP {status}).", null);
            }

            OEmbedResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<OEmbedResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                return Transient(endpoint, normalized, $"Response was not valid JSON (HTTP {status}).", ex);
            }

            if (payload is null)
            {
                return Transient(endpoint, normalized, $"Response JSON was null (HTTP {status}).", null);
            }

            if (status >= 400 || payload.Error is not null)
            {
                return MapError(endpoint, normalized, status, payload.Error, token);
            }

            if (string.IsNullOrWhiteSpace(payload.Html))
            {
                return Transient(endpoint, normalized, "Response had no html field.", null);
            }

            var sanitized = sanitizer.Sanitize(payload.Html, endpoint);
            if (!sanitized.RootElementPresent)
            {
                var removed = sanitized.RemovedTags.Count == 0 ? "(none)" : string.Join(", ", sanitized.RemovedTags);
                var message = $"Sanitiser did not find '{endpoint.ExpectedRootSelector}' in Meta's markup; removed tags: {removed}.";
                logger.LogError(UnexpectedMarkupEvent,
                    "Meta oEmbed {Endpoint}: unexpected markup for {Url}. {Message}", endpoint.Key, normalized, message);
                return EmbedResult.Failed(EmbedFailureKind.UnexpectedMarkup, message);
            }

            logger.LogDebug(FetchedEvent, "Meta oEmbed {Endpoint}: embedded {Url} ({Type}, width {Width})",
                endpoint.Key, normalized, payload.Type ?? "rich", payload.Width);

            return EmbedResult.Success(new EmbedItem
            {
                EndpointKey = endpoint.Key,
                ProviderName = string.IsNullOrWhiteSpace(payload.ProviderName) ? endpoint.DisplayName : payload.ProviderName,
                Html = sanitized.Html,
                Type = string.IsNullOrWhiteSpace(payload.Type) ? "rich" : payload.Type,
                Width = payload.Width,
                SourceUrl = normalized,
                RequiredScripts = [endpoint.SdkScriptUri(current)],
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up (request aborted). Not a Meta problem, so nothing to cache or log.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // HttpClient.Timeout surfaces as TaskCanceledException (with a TimeoutException inner on .NET 8).
            return Transient(endpoint, normalized, $"Request timed out after {current.HttpTimeout}.", ex);
        }
        catch (HttpRequestException ex)
        {
            var statusSuffix = ex.StatusCode is null ? string.Empty : $" (HTTP {(int)ex.StatusCode})";
            return Transient(endpoint, normalized, $"HTTP request failed{statusSuffix}.", ex);
        }
        catch (IOException ex)
        {
            return Transient(endpoint, normalized, "Reading the response failed.", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(InternalEvent, ex, "Meta oEmbed {Endpoint}: unexpected error while embedding {Url}", endpoint.Key, normalized);
            return EmbedResult.Failed(EmbedFailureKind.Internal, $"{ex.GetType().Name}: {Redact(ex.Message, token)}");
        }
    }

    /// <summary><c>{endpoint}?url={escaped}[&amp;hidecaption=true][&amp;access_token={escaped}]</c>.</summary>
    internal static Uri BuildRequestUri(MetaOEmbedEndpoint endpoint, Uri normalized, MetaEmbedsOptions current, string? token, OEmbedRequestParameters? parameters = null)
    {
        var endpointUri = endpoint.EndpointUri(current);
        var builder = new StringBuilder(endpointUri.GetLeftPart(UriPartial.Path));
        builder.Append(string.IsNullOrEmpty(endpointUri.Query) ? "?" : endpointUri.Query + "&");
        builder.Append("url=").Append(Uri.EscapeDataString(normalized.AbsoluteUri));
        (parameters ?? OEmbedRequestParameters.None).AppendTo(builder);
        if (token is not null)
        {
            builder.Append("&access_token=").Append(Uri.EscapeDataString(token));
        }

        return new Uri(builder.ToString(), UriKind.Absolute);
    }

    /// <summary>
    /// Logs the exception and returns a failure without it. Everything this provider returns is cached for up to
    /// <see cref="MetaEmbedsOptions.TransientFailureCacheDuration"/>, and an exception would pin its stack trace and
    /// whatever its object graph references in the memory cache for that long. The log already has the full detail.
    /// </summary>
    private EmbedResult Transient(MetaOEmbedEndpoint endpoint, Uri normalized, string message, Exception? exception)
    {
        logger.LogWarning(TransientEvent, exception, "Meta oEmbed {Endpoint}: transient failure for {Url}. {Message}",
            endpoint.Key, normalized, message);
        return EmbedResult.Failed(
            EmbedFailureKind.Transient,
            exception is null ? message : $"{message} ({exception.GetType().Name})");
    }

    private EmbedResult MapError(MetaOEmbedEndpoint endpoint, Uri normalized, int status, OEmbedError? error, string? token)
    {
        var code = error?.Code;
        var subcode = error?.ErrorSubcode;
        var metaMessage = Redact(error?.ErrorUserMsg ?? error?.Message ?? "(no message)", token);
        var trace = string.IsNullOrWhiteSpace(error?.FbTraceId) ? string.Empty : $" [fbtrace_id {error.FbTraceId}]";
        var detail = $"Meta error code {code?.ToString() ?? "-"}/{subcode?.ToString() ?? "-"} (HTTP {status}): {metaMessage}{trace}";

        EmbedFailureKind kind;
        string providerMessage;
        if (error?.IsTransient == true)
        {
            kind = EmbedFailureKind.Transient;
            providerMessage = $"Meta reports a transient error. {detail}";
        }
        else if (code == MediaNotFoundCode || subcode == MediaNotFoundSubcode)
        {
            kind = EmbedFailureKind.NotFound;
            providerMessage = detail;
        }
        else if (code == InvalidAccessTokenCode)
        {
            kind = EmbedFailureKind.RejectedByProvider;
            providerMessage = $"Meta rejected the configured credentials (check XperienceCommunityMetaEmbeds:Credentials). {detail}";
        }
        else
        {
            kind = EmbedFailureKind.RejectedByProvider;
            providerMessage = detail;
        }

        var failure = new EmbedFailure
        {
            Kind = kind,
            ProviderMessage = providerMessage,
            ProviderCode = code,
            ProviderSubcode = subcode,
        };

        switch (kind)
        {
            case EmbedFailureKind.Transient:
                logger.LogWarning(TransientEvent, "Meta oEmbed {Endpoint}: transient failure for {Url}. {Message}",
                    endpoint.Key, normalized, providerMessage);
                break;
            case EmbedFailureKind.NotFound:
                logger.LogInformation(NotFoundEvent, "Meta oEmbed {Endpoint}: media not found for {Url}. {Message}",
                    endpoint.Key, normalized, providerMessage);
                break;
            default:
                logger.LogInformation(RejectedEvent, "Meta oEmbed {Endpoint}: request rejected for {Url}. {Message}",
                    endpoint.Key, normalized, providerMessage);
                break;
        }

        return EmbedResult.Failed(failure);
    }

    private static bool IsJson(string? mediaType) =>
        mediaType is not null
        && (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

    /// <summary>Reads at most <paramref name="maxBytes"/>; returns null when the body is larger.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long declared && declared > maxBytes)
        {
            return null;
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    return null;
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        return buffer.ToArray();
    }

    /// <summary>Removes the access token (raw and percent-encoded) from anything that may be logged or stored.</summary>
    private static string Redact(string text, string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return text;
        }

        return text
            .Replace(Uri.EscapeDataString(token), RedactedToken, StringComparison.Ordinal)
            .Replace(token, RedactedToken, StringComparison.Ordinal);
    }
}
