namespace XperienceCommunity.MetaEmbeds;

/// <summary>
/// Optional Meta app credentials. The shape is shared with the planned Feeds package, which adds its own members
/// (for example an app secret) next to these.
/// </summary>
public class MetaCredentialsOptions
{
    /// <summary>Meta app id. Combined with <see cref="ClientToken"/> into the app access token <c>APP_ID|CLIENT_TOKEN</c>.</summary>
    public string? AppId { get; set; }

    /// <summary>Meta client token. Combined with <see cref="AppId"/> into the app access token <c>APP_ID|CLIENT_TOKEN</c>.</summary>
    public string? ClientToken { get; set; }

    /// <summary>An explicit access token. When set it is sent verbatim and wins over <see cref="AppId"/> + <see cref="ClientToken"/>.</summary>
    public string? AccessToken { get; set; }

    /// <summary>True when no usable credential is configured, i.e. calls are tokenless.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(AccessToken)
        && (string.IsNullOrWhiteSpace(AppId) || string.IsNullOrWhiteSpace(ClientToken));

    /// <summary>
    /// The <c>access_token</c> query value to send, or <c>null</c> for tokenless calls.
    /// </summary>
    public string? ResolveAccessToken()
    {
        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            return AccessToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(ClientToken))
        {
            return $"{AppId.Trim()}|{ClientToken.Trim()}";
        }

        return null;
    }
}
