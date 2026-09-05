using NUnit.Framework;

using System.Net;
using System.Text;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using XperienceCommunity.MetaEmbeds.Caching;
using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Providers.OEmbed;
using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class OEmbedProviderTests
{
    private const string InstagramPostUrl = "https://www.instagram.com/p/fA9uwTtkSN/";

    // ---- Supports -------------------------------------------------------------------------------------------------

    [TestCase("post")]
    [TestCase("POST")]
    [TestCase("  Post  ")]
    [TestCase(null)]
    [TestCase("")]
    public void Supports_PostSourceType_IsTrue(string? sourceType)
    {
        var h = new Harness();

        Assert.That(h.Provider.Supports(new EmbedRequest { SourceType = sourceType!, Input = "anything" }), Is.True);
    }

    [TestCase("feed")]
    [TestCase("story")]
    [TestCase("posts")]
    public void Supports_OtherSourceType_IsFalse(string sourceType)
    {
        var h = new Harness();

        Assert.That(h.Provider.Supports(new EmbedRequest { SourceType = sourceType, Input = InstagramPostUrl }), Is.False);
    }

    [Test]
    public void Supports_DoesNotInspectInput()
    {
        var h = new Harness();

        Assert.Multiple(() =>
        {
            Assert.That(h.Provider.Supports(new EmbedRequest { SourceType = "post", Input = "garbage" }), Is.True);
            Assert.That(h.Provider.Supports(new EmbedRequest { SourceType = "post", Input = "" }), Is.True);
            Assert.That(h.Provider.Name, Is.EqualTo(MetaEmbedsConstants.OEmbedProviderName));
        });
    }

    // ---- Input classification --------------------------------------------------------------------------------------

    [TestCase("")]
    [TestCase("   ")]
    public async Task Resolve_BlankInput_NotConfigured_NoHttp(string input)
    {
        var h = new Harness();

        var result = await h.ResolveAsync(input);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.NotConfigured));
        Assert.That(h.Handler.Requests, Is.Empty);
        Assert.That(h.Cache.Calls, Is.Empty);
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("https://user@www.instagram.com/p/fA9uwTtkSN/")]
    [TestCase("https://www.instagram.com:8443/p/fA9uwTtkSN/")]
    [TestCase("https://127.0.0.1/p/fA9uwTtkSN/")]
    [TestCase("not a url")]
    public async Task Resolve_MalformedInput_InvalidInput_NoHttp(string input)
    {
        var h = new Harness();

        var result = await h.ResolveAsync(input);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.InvalidInput));
        Assert.That(result.Failure!.ProviderMessage, Is.Not.Empty);
        Assert.That(h.Handler.Requests, Is.Empty);
        Assert.That(h.Cache.Calls, Is.Empty);
    }

    [TestCase("https://instagr.am/p/fA9uwTtkSN/")]
    [TestCase("https://example.com/p/fA9uwTtkSN/")]
    [TestCase("https://www.instagram.com/reels/fA9uwTtkSN/")]
    [TestCase("https://www.instagram.com/")]
    [TestCase("https://www.facebook.com/zuck")]
    public async Task Resolve_UnknownShape_UnsupportedInput_NoHttp(string input)
    {
        var h = new Harness();

        var result = await h.ResolveAsync(input);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.UnsupportedInput));
        Assert.That(h.Handler.Requests, Is.Empty);
        Assert.That(h.Cache.Calls, Is.Empty);
    }

    // ---- Success fixtures ------------------------------------------------------------------------------------------

    [TestCase(TestFixtures.InstagramPost, "instagram", "rich", 658, "Instagram", "https://www.instagram.com/embed.js")]
    [TestCase(TestFixtures.InstagramReel, "instagram", "rich", 658, "Instagram", "https://www.instagram.com/embed.js")]
    [TestCase(TestFixtures.ThreadsPost, "threads", "rich", 658, "Threads", "https://www.threads.com/embed.js")]
    [TestCase(TestFixtures.FacebookPost, "facebook-post", "rich", 552, "Facebook", "https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v25.0")]
    [TestCase(TestFixtures.FacebookVideo, "facebook-video", "video", 500, "Facebook", "https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v25.0")]
    public async Task Resolve_SuccessFixture_ReturnsOneItem(string fixture, string endpointKey, string type, int width, string providerName, string script)
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(fixture));
        var sourceUrl = TestFixtures.SourceUrls[fixture];

        var result = await h.ResolveAsync(sourceUrl);

        Assert.That(result.Succeeded, Is.True, result.Failure?.ProviderMessage);
        Assert.That(result.Items, Has.Count.EqualTo(1));
        var item = result.Items[0];
        Assert.Multiple(() =>
        {
            Assert.That(item.EndpointKey, Is.EqualTo(endpointKey));
            Assert.That(item.Type, Is.EqualTo(type));
            Assert.That(item.Width, Is.EqualTo(width));
            Assert.That(item.ProviderName, Is.EqualTo(providerName));
            Assert.That(item.RequiredScripts.Select(u => u.AbsoluteUri), Is.EqualTo(new[] { script }));
            Assert.That(item.Html, Is.EqualTo(TestFixtures.ReadHtml(fixture)));
            Assert.That(item.SourceUrl.AbsoluteUri, Is.EqualTo(sourceUrl));
        });
        Assert.That(h.Handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Resolve_SanitizerReceivesRawHtmlAndEndpoint()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        await h.ResolveAsync(InstagramPostUrl);

        h.Sanitizer.Received(1).Sanitize(TestFixtures.ReadHtml(TestFixtures.InstagramPost), MetaOEmbedEndpoints.Instagram);
    }

    [Test]
    public async Task Resolve_UsesSanitizedHtmlNotRaw()
    {
        var sanitizer = Substitute.For<IEmbedHtmlSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>(), Arg.Any<MetaOEmbedEndpoint>())
            .Returns(new SanitizedEmbedHtml("<blockquote class=\"instagram-media\">clean</blockquote>", true, ["script"]));
        var h = new Harness(sanitizer: sanitizer);
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Items[0].Html, Is.EqualTo("<blockquote class=\"instagram-media\">clean</blockquote>"));
    }

    [Test]
    public async Task Resolve_UnknownFieldsAndStringWidth_AreTolerated()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK,
            """{"version":"1.0","provider_name":"Instagram","author_name":"gone","thumbnail_url":"https://x/y.jpg","nested":{"a":[1,2]},"type":"rich","width":"640","html":"<blockquote class=\"instagram-media\"></blockquote>"}""");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Succeeded, Is.True, result.Failure?.ProviderMessage);
        Assert.That(result.Items[0].Width, Is.EqualTo(640));
    }

    [Test]
    public async Task Resolve_MissingProviderNameAndType_FallBackToEndpointDefaults()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, """{"html":"<blockquote class=\"instagram-media\"></blockquote>"}""");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Items[0].ProviderName, Is.EqualTo("Instagram"));
            Assert.That(result.Items[0].Type, Is.EqualTo("rich"));
            Assert.That(result.Items[0].Width, Is.Null);
        });
    }

    // ---- Request composition ---------------------------------------------------------------------------------------

    [TestCase(TestFixtures.InstagramPost, "https://graph.facebook.com/v25.0/instagram_oembed")]
    [TestCase(TestFixtures.InstagramReel, "https://graph.facebook.com/v25.0/instagram_oembed")]
    [TestCase(TestFixtures.ThreadsPost, "https://graph.threads.com/oembed")]
    [TestCase(TestFixtures.FacebookPost, "https://graph.facebook.com/v25.0/oembed_post")]
    [TestCase(TestFixtures.FacebookVideo, "https://graph.facebook.com/v25.0/oembed_video")]
    public async Task Resolve_CallsEndpointWithEncodedUrlAndNoTokenByDefault(string fixture, string endpoint)
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(fixture));
        var sourceUrl = TestFixtures.SourceUrls[fixture];

        await h.ResolveAsync(sourceUrl);

        var request = h.Handler.Requests.Single();
        var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.RequestUri.GetLeftPart(UriPartial.Path), Is.EqualTo(endpoint));
            Assert.That(query["url"].ToString(), Is.EqualTo(sourceUrl));
            Assert.That(query.ContainsKey("access_token"), Is.False);
            Assert.That(request.RequestUri.Query, Does.Contain("url=" + Uri.EscapeDataString(sourceUrl)));
        });
    }

    [Test]
    public async Task Resolve_UrlParameterIsPercentEncoded()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        await h.ResolveAsync("https://www.instagram.com/p/fA9uwTtkSN/?igsh=a&hl=en");

        var query = h.Handler.Requests.Single().RequestUri!.Query;
        Assert.That(query, Is.EqualTo("?url=https%3A%2F%2Fwww.instagram.com%2Fp%2FfA9uwTtkSN%2F%3Figsh%3Da%26hl%3Den"));
    }

    [Test]
    public async Task Resolve_WithAccessToken_SendsItVerbatim()
    {
        var h = new Harness(o => o.Credentials.AccessToken = "EAAB.secret-token_1|x");
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        await h.ResolveAsync(InstagramPostUrl);

        var query = QueryHelpers.ParseQuery(h.Handler.Requests.Single().RequestUri!.Query);
        Assert.That(query["access_token"].ToString(), Is.EqualTo("EAAB.secret-token_1|x"));
        Assert.That(query["url"].ToString(), Is.EqualTo(InstagramPostUrl));
    }

    [Test]
    public async Task Resolve_WithAppIdAndClientToken_SendsPipeJoinedAppToken()
    {
        var h = new Harness(o =>
        {
            o.Credentials.AppId = "123456";
            o.Credentials.ClientToken = "abcdef";
        });
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        await h.ResolveAsync(InstagramPostUrl);

        var uri = h.Handler.Requests.Single().RequestUri!;
        Assert.That(QueryHelpers.ParseQuery(uri.Query)["access_token"].ToString(), Is.EqualTo("123456|abcdef"));
        Assert.That(uri.Query, Does.Contain("access_token=123456%7Cabcdef"));
    }

    [Test]
    public async Task Resolve_AccessTokenWinsOverAppCredentials()
    {
        var h = new Harness(o =>
        {
            o.Credentials.AppId = "123456";
            o.Credentials.ClientToken = "abcdef";
            o.Credentials.AccessToken = "explicit";
        });
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        await h.ResolveAsync(InstagramPostUrl);

        Assert.That(QueryHelpers.ParseQuery(h.Handler.Requests.Single().RequestUri!.Query)["access_token"].ToString(), Is.EqualTo("explicit"));
    }

    [Test]
    public async Task Resolve_CustomGraphApiVersion_IsUsedInPathAndFacebookSdk()
    {
        var h = new Harness(o => o.GraphApiVersion = "v26.0");
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.FacebookPost));

        var result = await h.ResolveAsync(TestFixtures.SourceUrls[TestFixtures.FacebookPost]);

        Assert.That(h.Handler.Requests.Single().RequestUri!.GetLeftPart(UriPartial.Path), Is.EqualTo("https://graph.facebook.com/v26.0/oembed_post"));
        Assert.That(result.Items[0].RequiredScripts[0].AbsoluteUri, Is.EqualTo("https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v26.0"));
    }

    [Test]
    public async Task Resolve_CustomFacebookSdkLocale_IsUsedInSdkUri()
    {
        var h = new Harness(o => o.FacebookSdkLocale = "cs_CZ");
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.FacebookVideo));

        var result = await h.ResolveAsync(TestFixtures.SourceUrls[TestFixtures.FacebookVideo]);

        Assert.That(result.Items[0].RequiredScripts[0].AbsoluteUri, Is.EqualTo("https://connect.facebook.net/cs_CZ/sdk.js#xfbml=1&version=v25.0"));
    }

    [Test]
    public async Task Resolve_UsesTheNamedHttpClient()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        await h.ResolveAsync(InstagramPostUrl);

        h.HttpClientFactory.Received(1).CreateClient(MetaEmbedsConstants.HttpClientName);
    }

    // ---- Meta error payloads ---------------------------------------------------------------------------------------

    [TestCase(TestFixtures.ErrorMediaNotFound, EmbedFailureKind.NotFound, 24, 2207045, "does not exist or you don't have permission")]
    [TestCase(TestFixtures.ErrorInvalidUrl, EmbedFailureKind.RejectedByProvider, 100, 2207047, "does not refer to an embeddable media")]
    [TestCase(TestFixtures.ErrorInvalidDimensions, EmbedFailureKind.RejectedByProvider, 100, 2207049, "must be an integer between 320 and 658")]
    public async Task Resolve_ErrorFixture_MapsKindAndCodes(string fixture, EmbedFailureKind kind, int code, int subcode, string userMessageFragment)
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.BadRequest, TestFixtures.ReadJson(fixture));

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Succeeded, Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(result.Failure!.Kind, Is.EqualTo(kind));
            Assert.That(result.Failure.ProviderCode, Is.EqualTo(code));
            Assert.That(result.Failure.ProviderSubcode, Is.EqualTo(subcode));
            Assert.That(result.Failure.ProviderMessage, Does.Contain(userMessageFragment));
            Assert.That(result.Failure.EditorMessage, Is.Null);
            Assert.That(result.Items, Is.Empty);
        });
    }

    [Test]
    public async Task Resolve_NotFound_LogsInformationWithEventName()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.BadRequest, TestFixtures.ReadJson(TestFixtures.ErrorMediaNotFound));

        await h.ResolveAsync(InstagramPostUrl);

        var entry = h.Logger.Entries.Single(e => e.Level >= LogLevel.Information);
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Information));
        Assert.That(entry.Id.Name, Is.EqualTo("METAEMBEDS_NOT_FOUND"));
    }

    [Test]
    public async Task Resolve_Rejected_LogsInformationWithEventName()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.BadRequest, TestFixtures.ReadJson(TestFixtures.ErrorInvalidUrl));

        await h.ResolveAsync(InstagramPostUrl);

        var entry = h.Logger.Entries.Single(e => e.Level >= LogLevel.Information);
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Information));
        Assert.That(entry.Id.Name, Is.EqualTo("METAEMBEDS_REJECTED"));
    }

    [Test]
    public async Task Resolve_ProfileUrl_IsAcceptedThenRejectedByMeta()
    {
        // Parity with Meta's WordPress plugin: the pattern accepts profile URLs, Meta's endpoint rejects them today.
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.BadRequest, TestFixtures.ReadJson(TestFixtures.ErrorInvalidUrl));

        var result = await h.ResolveAsync("https://www.instagram.com/zuck/");

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.RejectedByProvider));
        Assert.That(result.Failure!.ProviderSubcode, Is.EqualTo(2207047));
        Assert.That(h.Handler.Requests.Single().RequestUri!.AbsolutePath, Does.EndWith("/instagram_oembed"));
    }

    [Test]
    public async Task Resolve_Code190_RejectedWithCredentialHint()
    {
        var h = new Harness(o => o.Credentials.AccessToken = "bad");
        h.Handler.RespondWithJson(HttpStatusCode.BadRequest,
            """{"error":{"message":"Invalid OAuth access token - Cannot parse access token","type":"OAuthException","code":190,"fbtrace_id":"AbC"}}""");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.RejectedByProvider));
            Assert.That(result.Failure!.ProviderCode, Is.EqualTo(190));
            Assert.That(result.Failure.ProviderSubcode, Is.Null);
            Assert.That(result.Failure.ProviderMessage, Does.Contain("credentials").IgnoreCase);
            Assert.That(result.Failure.ProviderMessage, Does.Contain("Cannot parse access token"));
        });
    }

    [Test]
    public async Task Resolve_Code100WithoutSubcode_Rejected()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.BadRequest,
            """{"error":{"message":"(#100) The parameter url is required.","type":"OAuthException","code":100,"fbtrace_id":"x"}}""");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.RejectedByProvider));
        Assert.That(result.Failure!.ProviderCode, Is.EqualTo(100));
        Assert.That(result.Failure.ProviderMessage, Does.Contain("The parameter url is required"));
    }

    [Test]
    public async Task Resolve_4xxWithoutErrorObject_Rejected()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.NotFound, "{}");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.RejectedByProvider));
        Assert.That(result.Failure!.ProviderCode, Is.Null);
        Assert.That(result.Failure.ProviderMessage, Does.Contain("HTTP 404"));
    }

    [Test]
    public async Task Resolve_ErrorMarkedTransient_IsTransient()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.BadRequest,
            """{"error":{"message":"Please retry your request later.","type":"OAuthException","code":2,"is_transient":true}}""");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.ProviderCode, Is.EqualTo(2));
    }

    [Test]
    public async Task Resolve_200WithErrorObject_IsStillMapped()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.ErrorMediaNotFound));

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.NotFound));
    }

    // ---- Transient conditions --------------------------------------------------------------------------------------

    [TestCase(429)]
    [TestCase(500)]
    [TestCase(502)]
    [TestCase(503)]
    [TestCase(301)]
    public async Task Resolve_HttpStatus_IsTransient(int status)
    {
        var h = new Harness();
        h.Handler.RespondWithJson((HttpStatusCode)status, "<html>oops</html>", "text/html");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.ProviderMessage, Does.Contain($"HTTP {status}"));
        Assert.That(h.Logger.Entries.Single(e => e.Level >= LogLevel.Information).Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(h.Logger.Entries.Single(e => e.Level >= LogLevel.Information).Id.Name, Is.EqualTo("METAEMBEDS_TRANSIENT"));
    }

    [Test]
    public async Task Resolve_HttpRequestException_IsTransientWithException()
    {
        var h = new Harness();
        h.Handler.Responder = (_, _) => throw new HttpRequestException("No such host is known.");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.Exception, Is.InstanceOf<HttpRequestException>());
        var entry = h.Logger.Entries.Single(e => e.Level == LogLevel.Warning);
        Assert.That(entry.Exception, Is.InstanceOf<HttpRequestException>());
    }

    [Test]
    public async Task Resolve_HandlerTaskCanceledWithoutCallerCancellation_IsTransient()
    {
        var h = new Harness();
        h.Handler.Responder = (_, _) => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException());

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.ProviderMessage, Does.Contain("timed out"));
    }

    [Test]
    public async Task Resolve_RealHttpClientTimeout_IsTransient()
    {
        var h = new Harness(o => o.HttpTimeout = TimeSpan.FromMilliseconds(100));
        h.Handler.Responder = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return CoreFakeHttpHandler.Json(HttpStatusCode.OK, "{}");
        };

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.Exception, Is.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Resolve_CallerCancelled_PropagatesCancellationWithoutCachingOrLogging()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(() => h.ResolveAsync(InstagramPostUrl, cts.Token));
        Assert.That(h.Logger.Entries.Where(e => e.Level >= LogLevel.Information), Is.Empty);
    }

    [TestCase("text/html")]
    [TestCase("text/plain")]
    [TestCase("application/xml")]
    public async Task Resolve_NonJsonContentType_IsTransient(string mediaType)
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost), mediaType);

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.ProviderMessage, Does.Contain(mediaType));
    }

    [Test]
    public async Task Resolve_MissingContentType_IsTransient()
    {
        var h = new Harness();
        h.Handler.Responder = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(TestFixtures.ReadJson(TestFixtures.InstagramPost))),
        });

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
    }

    [Test]
    public async Task Resolve_OversizedBodyWithContentLength_IsTransient()
    {
        var h = new Harness(o => o.MaxResponseBytes = 100);
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.ProviderMessage, Does.Contain("exceeded 100 bytes"));
    }

    [Test]
    public async Task Resolve_OversizedBodyWithoutContentLength_IsTransient()
    {
        var h = new Harness(o => o.MaxResponseBytes = 100);
        h.Handler.Responder = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthJsonContent(Encoding.UTF8.GetBytes(TestFixtures.ReadJson(TestFixtures.InstagramPost))),
        });

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.ProviderMessage, Does.Contain("exceeded 100 bytes"));
    }

    [Test]
    public async Task Resolve_BodyWithinLimitWithoutContentLength_Succeeds()
    {
        var h = new Harness();
        h.Handler.Responder = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthJsonContent(Encoding.UTF8.GetBytes(TestFixtures.ReadJson(TestFixtures.InstagramPost))),
        });

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Succeeded, Is.True, result.Failure?.ProviderMessage);
    }

    [TestCase("""{"html": "<blockquote""")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("")]
    public async Task Resolve_UnparseableJson_IsTransient(string body)
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, body);

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient), body);
    }

    [TestCase("""{"html":"","type":"rich"}""")]
    [TestCase("""{"html":"   ","type":"rich"}""")]
    [TestCase("""{"html":null,"type":"rich"}""")]
    [TestCase("""{"type":"rich","width":658}""")]
    public async Task Resolve_MissingOrEmptyHtml_IsTransient(string body)
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, body);

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient), body);
        Assert.That(result.Failure!.ProviderMessage, Does.Contain("html"));
    }

    // ---- Sanitiser fail-closed -------------------------------------------------------------------------------------

    [Test]
    public async Task Resolve_SanitizerReportsMissingRoot_IsUnexpectedMarkup()
    {
        var sanitizer = Substitute.For<IEmbedHtmlSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>(), Arg.Any<MetaOEmbedEndpoint>())
            .Returns(new SanitizedEmbedHtml(string.Empty, false, ["script", "iframe"]));
        var h = new Harness(sanitizer: sanitizer);
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.UnexpectedMarkup));
            Assert.That(result.Failure!.ProviderMessage, Does.Contain("script").And.Contain("iframe").And.Contain("blockquote.instagram-media"));
            Assert.That(result.Items, Is.Empty);
        });
        var entry = h.Logger.Entries.Single(e => e.Level >= LogLevel.Information);
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry.Id.Name, Is.EqualTo("METAEMBEDS_UNEXPECTED_MARKUP"));
    }

    // ---- Cache interaction -----------------------------------------------------------------------------------------

    [Test]
    public async Task Resolve_CacheHit_SkipsHttpAndSanitizer()
    {
        var h = new Harness();
        var canned = EmbedResult.Success(new EmbedItem
        {
            EndpointKey = "instagram",
            ProviderName = "Instagram",
            Html = "<blockquote class=\"instagram-media\">cached</blockquote>",
            Type = "rich",
            SourceUrl = new Uri(InstagramPostUrl),
            RequiredScripts = [new Uri("https://www.instagram.com/embed.js")],
        });
        h.Cache.CannedResult = canned;

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result, Is.SameAs(canned));
        Assert.That(h.Handler.Requests, Is.Empty);
        h.Sanitizer.DidNotReceiveWithAnyArgs().Sanitize(default!, default!);
        h.HttpClientFactory.DidNotReceiveWithAnyArgs().CreateClient(default!);
    }

    [Test]
    public async Task Resolve_CacheKey_UsesCacheFormAnonAndVersion()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        await h.ResolveAsync("http://instagram.com/p/fA9uwTtkSN?igsh=abc#x");

        var (key, policy) = h.Cache.Calls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(key.EndpointKey, Is.EqualTo("instagram"));
            Assert.That(key.NormalizedUrl, Is.EqualTo("https://instagram.com/p/fA9uwTtkSN/"));
            Assert.That(key.Authenticated, Is.False);
            Assert.That(key.GraphApiVersion, Is.EqualTo("v25.0"));
            Assert.That(policy.DependencyKeys, Is.EqualTo(new[] { "metaembeds|all", "metaembeds|endpoint|instagram" }));
            Assert.That(policy.Success, Is.EqualTo(TimeSpan.FromHours(12)));
            Assert.That(policy.NotFound, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(policy.Transient, Is.EqualTo(TimeSpan.FromMinutes(2)));
        });
    }

    [Test]
    public async Task Resolve_CacheKey_MarksAuthenticatedWhenCredentialsConfigured()
    {
        var h = new Harness(o =>
        {
            o.Credentials.AppId = "1";
            o.Credentials.ClientToken = "2";
            o.GraphApiVersion = "v26.0";
            o.SuccessCacheDuration = TimeSpan.FromMinutes(5);
        });
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.ThreadsPost));

        await h.ResolveAsync(TestFixtures.SourceUrls[TestFixtures.ThreadsPost]);

        var (key, policy) = h.Cache.Calls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(key.EndpointKey, Is.EqualTo("threads"));
            Assert.That(key.Authenticated, Is.True);
            Assert.That(key.GraphApiVersion, Is.EqualTo("v26.0"));
            Assert.That(policy.Success, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(policy.DependencyKeys, Does.Contain("metaembeds|endpoint|threads"));
        });
    }

    [Test]
    public async Task Resolve_QueryVariantsOfOnePost_ShareOneCacheKey()
    {
        var h = new Harness();
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        await h.ResolveAsync("https://www.instagram.com/p/fA9uwTtkSN/");
        await h.ResolveAsync("http://instagram.com/p/fA9uwTtkSN?igsh=MzRlODBiNWFlZA==&hl=en");

        Assert.That(h.Cache.Calls.Select(c => c.Key).Distinct().Count(), Is.EqualTo(1));
    }

    // ---- Token redaction and never-throws --------------------------------------------------------------------------

    [Test]
    public async Task Resolve_TokenNeverAppearsInLogMessagesOrProviderMessage_OnNetworkFailure()
    {
        const string token = "SECRET-TOKEN-XYZ";
        var h = new Harness(o => o.Credentials.AccessToken = token);
        h.Handler.Responder = (request, _) => throw new HttpRequestException($"failed: {request.RequestUri}");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.ProviderMessage, Does.Not.Contain(token));
        Assert.That(h.Logger.Entries.Select(e => e.Message), Has.None.Contains(token));
        Assert.That(h.Logger.Entries.Select(e => e.Message), Has.Some.Contains("access_token=***"));
    }

    [Test]
    public async Task Resolve_TokenEchoedByMeta_IsRedactedFromProviderMessage()
    {
        const string token = "SECRET-TOKEN-XYZ";
        var h = new Harness(o => o.Credentials.AccessToken = token);
        h.Handler.RespondWithJson(HttpStatusCode.BadRequest,
            $$$"""{"error":{"message":"Invalid OAuth access token {{{token}}}","type":"OAuthException","code":190}}""");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.RejectedByProvider));
        Assert.That(result.Failure!.ProviderMessage, Does.Not.Contain(token).And.Contain("***"));
        Assert.That(h.Logger.Entries.Select(e => e.Message), Has.None.Contains(token));
    }

    [Test]
    public async Task Resolve_UnexpectedHandlerException_IsInternalNotThrown()
    {
        var h = new Harness();
        h.Handler.Responder = (_, _) => throw new InvalidOperationException("kaboom");

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Internal));
        Assert.That(result.Failure!.Exception, Is.InstanceOf<InvalidOperationException>());
        var entry = h.Logger.Entries.Single(e => e.Level >= LogLevel.Information);
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry.Id.Name, Is.EqualTo("METAEMBEDS_INTERNAL"));
    }

    [Test]
    public async Task Resolve_SanitizerThrows_IsInternalNotThrown()
    {
        var sanitizer = Substitute.For<IEmbedHtmlSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>(), Arg.Any<MetaOEmbedEndpoint>()).Returns(_ => throw new NotSupportedException("profile"));
        var h = new Harness(sanitizer: sanitizer);
        h.Handler.RespondWithJson(HttpStatusCode.OK, TestFixtures.ReadJson(TestFixtures.InstagramPost));

        var result = await h.ResolveAsync(InstagramPostUrl);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Internal));
    }

    [Test]
    public void BuildRequestUri_AppendsToExistingQuery()
    {
        var endpoint = MetaOEmbedEndpoints.Instagram with { EndpointUri = _ => new Uri("https://example.test/oembed?fields=html") };

        var uri = MetaOEmbedProvider.BuildRequestUri(endpoint, new Uri(InstagramPostUrl), new MetaEmbedsOptions(), "t");

        Assert.That(uri.AbsoluteUri, Is.EqualTo("https://example.test/oembed?fields=html&url=https%3A%2F%2Fwww.instagram.com%2Fp%2FfA9uwTtkSN%2F&access_token=t"));
    }

    // ---- Harness ---------------------------------------------------------------------------------------------------

    private sealed class Harness
    {
        public Harness(Action<MetaEmbedsOptions>? configure = null, IEmbedHtmlSanitizer? sanitizer = null)
        {
            configure?.Invoke(Options);
            Sanitizer = sanitizer ?? PassThroughSanitizer();

            HttpClientFactory = Substitute.For<IHttpClientFactory>();
            HttpClientFactory.CreateClient(MetaEmbedsConstants.HttpClientName)
                .Returns(_ => new HttpClient(Handler, disposeHandler: false) { Timeout = Options.HttpTimeout });

            var monitor = Substitute.For<IOptionsMonitor<MetaEmbedsOptions>>();
            monitor.CurrentValue.Returns(Options);

            Provider = new MetaOEmbedProvider(
                HttpClientFactory,
                new MetaOEmbedEndpointRegistry(MetaOEmbedEndpoints.Default),
                new EmbedUrlMatcher(),
                Sanitizer,
                Cache,
                monitor,
                Logger);
        }

        public MetaEmbedsOptions Options { get; } = new();

        public CoreFakeHttpHandler Handler { get; } = new();

        public CoreTestLogger<MetaOEmbedProvider> Logger { get; } = new();

        public PassThroughEmbedResultCache Cache { get; } = new();

        public IEmbedHtmlSanitizer Sanitizer { get; }

        public IHttpClientFactory HttpClientFactory { get; }

        public MetaOEmbedProvider Provider { get; }

        public Task<EmbedResult> ResolveAsync(string input, CancellationToken cancellationToken = default) =>
            Provider.ResolveAsync(new EmbedRequest { SourceType = EmbedSourceTypes.Post, Input = input }, cancellationToken);

        private static IEmbedHtmlSanitizer PassThroughSanitizer()
        {
            var sanitizer = Substitute.For<IEmbedHtmlSanitizer>();
            sanitizer.Sanitize(Arg.Any<string>(), Arg.Any<MetaOEmbedEndpoint>())
                .Returns(call => new SanitizedEmbedHtml(call.Arg<string>(), true, []));
            return sanitizer;
        }
    }

    /// <summary>JSON content that reports no Content-Length, to exercise the streaming size cap.</summary>
    private sealed class UnknownLengthJsonContent : HttpContent
    {
        private readonly byte[] bytes;

        public UnknownLengthJsonContent(byte[] bytes)
        {
            this.bytes = bytes;
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}

/// <summary>Records every request and answers with a configurable responder. Honours cancellation like a real handler.</summary>
internal sealed class CoreFakeHttpHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Responder { get; set; } =
        (_, _) => Task.FromResult(Json(HttpStatusCode.NotFound, "{}"));

    public static HttpResponseMessage Json(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    public void RespondWithJson(HttpStatusCode status, string body, string mediaType = "application/json") =>
        Responder = (_, _) => Task.FromResult(Json(status, body, mediaType));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Responder(request, cancellationToken);
    }
}

/// <summary>Captures formatted log entries so tests can assert levels, event names and redaction.</summary>
internal sealed class CoreTestLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, EventId Id, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, eventId, formatter(state, exception), exception));
}

/// <summary>Cache double: records calls and either runs the factory or returns a canned result.</summary>
internal sealed class PassThroughEmbedResultCache : IEmbedResultCache
{
    public List<(EmbedCacheKey Key, EmbedCachePolicy Policy)> Calls { get; } = [];

    public EmbedResult? CannedResult { get; set; }

    public Task<EmbedResult> GetOrAddAsync(
        EmbedCacheKey key,
        Func<CancellationToken, Task<EmbedResult>> factory,
        EmbedCachePolicy policy,
        CancellationToken cancellationToken)
    {
        Calls.Add((key, policy));
        return CannedResult is not null ? Task.FromResult(CannedResult) : factory(cancellationToken);
    }
}
