using System.Text;

namespace XperienceCommunity.MetaEmbeds.Providers.OEmbed;

/// <summary>
/// The oEmbed query parameters this provider adds beyond <c>url</c> and <c>access_token</c>, derived from
/// <see cref="EmbedRequest.Parameters"/> for one endpoint. Parameters that change Meta's response are also the
/// cache <see cref="CacheVariant"/>.
/// </summary>
/// <param name="HideCaption">Instagram only: append <c>hidecaption=true</c>, which removes the caption from the markup.</param>
public sealed record OEmbedRequestParameters(bool HideCaption)
{
    /// <summary>No extra parameters.</summary>
    public static OEmbedRequestParameters None { get; } = new(false);

    /// <summary>Cache key variant segment: <c>hidecaption</c> or empty.</summary>
    public string CacheVariant => HideCaption ? "hidecaption" : string.Empty;

    /// <summary>Picks the parameters the given endpoint understands from a request's parameter set.</summary>
    public static OEmbedRequestParameters For(MetaOEmbedEndpoint endpoint, IReadOnlyDictionary<string, string>? requestParameters)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var hideCaption = string.Equals(endpoint.Key, MetaOEmbedEndpoints.Instagram.Key, StringComparison.OrdinalIgnoreCase)
            && EmbedRequestParameters.IsEnabled(requestParameters, EmbedRequestParameters.HideCaption);
        return hideCaption ? new OEmbedRequestParameters(true) : None;
    }

    /// <summary>Appends <c>&amp;name=value</c> pairs to a query being built.</summary>
    public void AppendTo(StringBuilder query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (HideCaption)
        {
            query.Append("&hidecaption=true");
        }
    }
}
