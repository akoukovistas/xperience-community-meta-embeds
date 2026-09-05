using System.Diagnostics.CodeAnalysis;

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>
/// Default <see cref="IEmbedUrlMatcher"/>: a defensive parser for the URL an editor typed. It knows nothing about
/// Meta's hosts (that is the endpoint registry's job); it only guarantees the value is a plain, absolute http(s)
/// URL with a DNS host name and gives every equivalent spelling of one post the same cache form.
/// </summary>
public sealed class EmbedUrlMatcher : IEmbedUrlMatcher
{
    /// <summary>Longest input accepted, matching the widget's <c>MaxLengthValidationRule</c>.</summary>
    public const int MaxLength = 2048;

    /// <inheritdoc />
    public bool TryParse(string? input, [NotNullWhen(true)] out Uri? normalized, [NotNullWhen(false)] out string? reason)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            reason = "empty";
            return false;
        }

        var text = input.Trim();
        if (text.Length > MaxLength)
        {
            reason = $"longer than {MaxLength} characters";
            return false;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            reason = "not an absolute URL";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            reason = $"scheme '{uri.Scheme}' is not http(s)";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            reason = "userinfo is not allowed";
            return false;
        }

        if (!uri.IsDefaultPort)
        {
            reason = "explicit port is not allowed";
            return false;
        }

        if (uri.HostNameType != UriHostNameType.Dns || !IsPlainDnsHost(uri.Host))
        {
            reason = "host is not a plain DNS name";
            return false;
        }

        // Scheme and host are case-insensitive; path and query are kept exactly as typed (shortcodes are case-sensitive).
        // The fragment never reaches the server, so it is dropped here rather than sent to Meta.
        var host = uri.Host.ToLowerInvariant();
        if (!Uri.TryCreate($"{uri.Scheme}://{host}{uri.PathAndQuery}", UriKind.Absolute, out normalized))
        {
            reason = "could not normalise the URL";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public string ToCacheForm(Uri normalized)
    {
        ArgumentNullException.ThrowIfNull(normalized);

        var host = normalized.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        var path = normalized.AbsolutePath;
        if (path.Length == 0 || path[^1] != '/')
        {
            path += "/";
        }

        return $"https://{host}{path}";
    }

    /// <summary>ASCII letters, digits, hyphens and dots only; rejects IDN/Unicode hosts, which no Meta endpoint uses.</summary>
    private static bool IsPlainDnsHost(string host)
    {
        if (host.Length == 0 || host.Length > 253)
        {
            return false;
        }

        foreach (var c in host)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '.')
            {
                return false;
            }
        }

        return true;
    }
}
