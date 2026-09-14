using System.Text.Json.Serialization;

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>
/// Meta's Graph API error object, as returned with HTTP 400 by the oEmbed endpoints. Observed shapes (2026-09-04):
/// code 24 / subcode 2207045 "Media Not Found", code 100 / subcode 2207047 "Invalid URL", code 100 / subcode 2207049
/// "Invalid Dimensions", code 100 without subcode for a missing parameter, code 190 for a bad access token.
/// </summary>
internal sealed class OEmbedError
{
    /// <summary>Developer-facing message, e.g. <c>Invalid parameter</c>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>Graph API error code.</summary>
    [JsonPropertyName("code")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Code { get; set; }

    /// <summary>Graph API error subcode, when present.</summary>
    [JsonPropertyName("error_subcode")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? ErrorSubcode { get; set; }

    /// <summary>Meta's own hint that a retry may succeed.</summary>
    [JsonPropertyName("is_transient")]
    public bool? IsTransient { get; set; }

    /// <summary>User-facing explanation. Logged, never rendered.</summary>
    [JsonPropertyName("error_user_msg")]
    public string? ErrorUserMsg { get; set; }

    /// <summary>Meta's trace id. Included in the logged detail so it can be quoted when raising a ticket with Meta.</summary>
    [JsonPropertyName("fbtrace_id")]
    public string? FbTraceId { get; set; }
}
