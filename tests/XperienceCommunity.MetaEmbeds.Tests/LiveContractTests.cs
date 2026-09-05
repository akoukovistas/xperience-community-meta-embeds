using NUnit.Framework;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace XperienceCommunity.MetaEmbeds.Tests;

/// <summary>
/// Drift alarm against Meta's real endpoints. Skipped unless the environment variable <c>METAEMBEDS_LIVE</c> is
/// <c>1</c>, so CI stays offline and deterministic. Run locally with e.g. <c>$env:METAEMBEDS_LIVE = "1"; dotnet test --filter Category=Live</c>.
/// </summary>
[TestFixture]
[Category("Live")]
public class LiveContractTests
{
    private static readonly string[] ExpectedFields = ["html", "provider_name", "provider_url", "type", "version", "width"];

    private static readonly string[] DeprecatedFields =
        ["author_name", "author_url", "author_id", "media_id", "title", "thumbnail_url", "thumbnail_width", "thumbnail_height"];

    private HttpClient client = null!;

    [SetUp]
    public void SetUp()
    {
        if (Environment.GetEnvironmentVariable("METAEMBEDS_LIVE") != "1")
        {
            Assert.Ignore("Set METAEMBEDS_LIVE=1 to run the live contract tests against Meta's endpoints.");
        }

        client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XperienceCommunity.MetaEmbeds.Tests", MetaEmbedsConstants.Version));
    }

    [TearDown]
    public void TearDown() => client?.Dispose();

    [TestCase(TestFixtures.InstagramPost, "https://graph.facebook.com/v25.0/instagram_oembed", "instagram-media", "rich")]
    [TestCase(TestFixtures.InstagramReel, "https://graph.facebook.com/v25.0/instagram_oembed", "instagram-media", "rich")]
    [TestCase(TestFixtures.ThreadsPost, "https://graph.threads.com/oembed", "text-post-media", "rich")]
    [TestCase(TestFixtures.FacebookPost, "https://graph.facebook.com/v25.0/oembed_post", "fb-post", "rich")]
    [TestCase(TestFixtures.FacebookVideo, "https://graph.facebook.com/v25.0/oembed_video", "fb-video", "video")]
    public async Task Endpoint_ReturnsExactlyTheSixKnownFields(string fixture, string endpoint, string rootClass, string expectedType)
    {
        var sourceUrl = TestFixtures.SourceUrls[fixture];
        var requestUri = new Uri($"{endpoint}?url={Uri.EscapeDataString(sourceUrl)}");

        using var response = await client.GetAsync(requestUri);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var fields = root.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(fields, Is.EqualTo(ExpectedFields), "Meta changed the oEmbed field set");
            Assert.That(root.GetProperty("html").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(root.GetProperty("html").GetString(), Does.Contain(rootClass));
            Assert.That(root.GetProperty("type").GetString(), Is.EqualTo(expectedType));
            Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("1.0"));
            Assert.That(root.GetProperty("width").ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(root.GetProperty("provider_name").GetString(), Is.Not.Empty);
            foreach (var deprecated in DeprecatedFields)
            {
                Assert.That(root.TryGetProperty(deprecated, out _), Is.False, $"deprecated field '{deprecated}' came back");
            }
        });
    }

    [Test]
    public async Task InstagramProfileUrl_IsRejectedAsNotEmbeddable()
    {
        // The pattern accepts profile URLs for parity with Meta's plugin; this is the observed behaviour we document.
        var requestUri = new Uri("https://graph.facebook.com/v25.0/instagram_oembed?url=" + Uri.EscapeDataString("https://www.instagram.com/zuck/"));

        using var response = await client.GetAsync(requestUri);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
        using var document = JsonDocument.Parse(body);
        var error = document.RootElement.GetProperty("error");
        Assert.Multiple(() =>
        {
            Assert.That(error.GetProperty("code").GetInt32(), Is.EqualTo(100));
            Assert.That(error.GetProperty("error_subcode").GetInt32(), Is.EqualTo(2207047));
            Assert.That(error.TryGetProperty("is_transient", out var transient) && transient.GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task InstagramNonexistentPost_IsMediaNotFound()
    {
        var requestUri = new Uri("https://graph.facebook.com/v25.0/instagram_oembed?url=" + Uri.EscapeDataString("https://www.instagram.com/p/zzzzzzzzzzzzzzzzzzzzzzzzzzz/"));

        using var response = await client.GetAsync(requestUri);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
        using var document = JsonDocument.Parse(body);
        var error = document.RootElement.GetProperty("error");
        Assert.That(error.GetProperty("code").GetInt32(), Is.EqualTo(24).Or.EqualTo(100), body);
    }

    [Test]
    public async Task TokenlessRoute_StillWorksWithoutAccessToken()
    {
        // The whole package rests on this: no access_token parameter, HTTP 200.
        var requestUri = new Uri("https://graph.facebook.com/v25.0/instagram_oembed?url=" + Uri.EscapeDataString(TestFixtures.SourceUrls[TestFixtures.InstagramPost]));

        using var response = await client.GetAsync(requestUri);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(requestUri.Query, Does.Not.Contain("access_token"));
    }
}
