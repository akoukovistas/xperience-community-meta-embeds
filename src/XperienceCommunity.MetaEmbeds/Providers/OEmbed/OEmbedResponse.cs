using System.Text.Json.Serialization;

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>
/// The subset of Meta's oEmbed response this package reads. Meta returns exactly <c>html</c>, <c>provider_name</c>,
/// <c>provider_url</c>, <c>type</c>, <c>version</c> and <c>width</c> (verified 2026-09-04); the deprecated author and
/// thumbnail fields are gone and anything unknown is ignored. Error payloads carry an <see cref="Error"/> object instead.
/// </summary>
internal sealed class OEmbedResponse
{
    /// <summary>The embed markup, including Meta's inline SDK <c>&lt;script&gt;</c>, which the sanitiser removes.</summary>
    [JsonPropertyName("html")]
    public string? Html { get; set; }

    /// <summary><c>Instagram</c>, <c>Threads</c> or <c>Facebook</c>.</summary>
    [JsonPropertyName("provider_name")]
    public string? ProviderName { get; set; }

    /// <summary>Provider home page.</summary>
    [JsonPropertyName("provider_url")]
    public string? ProviderUrl { get; set; }

    /// <summary><c>rich</c> or <c>video</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>oEmbed protocol version, <c>1.0</c>.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>Suggested width in pixels. Meta sends a number; a string is tolerated.</summary>
    [JsonPropertyName("width")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Width { get; set; }

    /// <summary>Present on error responses only.</summary>
    [JsonPropertyName("error")]
    public OEmbedError? Error { get; set; }
}
