namespace XperienceCommunity.MetaEmbeds.Providers;

/// <summary>Why an embed could not be resolved. Drives the editor message, the log level and the negative-cache duration.</summary>
public enum EmbedFailureKind
{
    /// <summary>The widget has no input yet (empty URL).</summary>
    NotConfigured = 0,

    /// <summary>The input is not a well-formed absolute http(s) URL (or is too long, has userinfo, a port, an IP host...).</summary>
    InvalidInput = 1,

    /// <summary>Well-formed, but not a Threads / Instagram / Facebook URL shape any endpoint accepts.</summary>
    UnsupportedInput = 2,

    /// <summary>Meta says the media does not exist, is private, or is not embeddable (code 24 / subcode 2207045).</summary>
    NotFound = 3,

    /// <summary>Meta rejected the request as malformed or not embeddable (code 100, e.g. subcode 2207047). Profile URLs land here.</summary>
    RejectedByProvider = 4,

    /// <summary>Network error, timeout, 429, 5xx, non-JSON or oversized response. Worth retrying soon.</summary>
    Transient = 5,

    /// <summary>The sanitiser removed the expected root element, so nothing is rendered (fail closed).</summary>
    UnexpectedMarkup = 6,

    /// <summary>Any other exception. Logged with the exception.</summary>
    Internal = 7,
}
