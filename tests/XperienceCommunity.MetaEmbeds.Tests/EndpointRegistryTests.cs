using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Providers.OEmbed;

namespace XperienceCommunity.MetaEmbeds.Tests;

/// <summary>
/// Ports the URL tables of Meta's WordPress plugin (tests/MetaEmbedsTest.php, v1.2.2) through the real pipeline:
/// <see cref="EmbedUrlMatcher.TryParse"/> then <see cref="MetaOEmbedEndpointRegistry.Match"/>. The plugin tests each
/// pattern in isolation, so its "other site" rows (false there) resolve to the other platform's key here.
/// </summary>
[TestFixture]
public class EndpointRegistryTests
{
    /// <summary>Sentinel for "the matcher rejected the input before any endpoint was consulted".</summary>
    private const string Invalid = "<invalid>";

    private readonly EmbedUrlMatcher matcher = new();
    private readonly MetaOEmbedEndpointRegistry registry = new(MetaOEmbedEndpoints.Default);

    // threads_url_provider
    [TestCase("https://www.threads.com/@zuck/post/C1234567890", "threads")]
    [TestCase("https://threads.com/@zuck/post/C1234567890", "threads")]
    [TestCase("http://www.threads.com/@zuck/post/C1234567890", "threads")]
    [TestCase("https://www.threads.com/t/C1234567890", "threads")]
    [TestCase("https://threads.com/t/C1234567890", "threads")]
    [TestCase("http://www.threads.com/t/C1234567890", "threads")]
    [TestCase("https://www.threads.net/@zuck/post/DUGwwelEh_K", "threads")]
    [TestCase("https://threads.net/@zuck/post/DUGwwelEh_K", "threads")]
    [TestCase("https://www.threads.net/t/DUGwwelEh_K", "threads")]
    [TestCase("https://threads.net/t/DUGwwelEh_K", "threads")]
    [TestCase("https://www.threads.com/@zuck", null)]
    [TestCase("https://www.threads.com/", null)]
    [TestCase("https://www.threads.com/search", null)]
    [TestCase("https://www.instagram.com/p/ABC123", "instagram")]
    [TestCase("not a url", Invalid)]
    public void ThreadsTable(string input, string? expected) => AssertClassification(input, expected);

    // instagram_url_provider
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/", "instagram")]
    [TestCase("https://instagram.com/p/fA9uwTtkSN/", "instagram")]
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN", "instagram")]
    [TestCase("http://www.instagram.com/p/fA9uwTtkSN/", "instagram")]
    [TestCase("https://www.instagram.com/reel/ABC123/", "instagram")]
    [TestCase("https://instagram.com/reel/ABC123/", "instagram")]
    [TestCase("https://www.instagram.com/reel/ABC123", "instagram")]
    [TestCase("http://www.instagram.com/reel/ABC123/", "instagram")]
    [TestCase("https://www.instagram.com/zuck", "instagram")]
    [TestCase("https://instagram.com/zuck", "instagram")]
    [TestCase("https://www.instagram.com/zuck/", "instagram")]
    [TestCase("http://www.instagram.com/zuck", "instagram")]
    [TestCase("https://www.instagram.com/some.user", "instagram")]
    [TestCase("https://www.instagram.com/some_user", "instagram")]
    [TestCase("https://www.instagram.com/zuck?hl=en", "instagram")]
    [TestCase("https://www.instagram.com/zuck/?utm_source=share", "instagram")]
    [TestCase("https://www.instagram.com/", null)]
    [TestCase("https://www.instagram.com/stories/zuck/123456", null)]
    [TestCase("https://www.instagram.com/explore/", null)]
    [TestCase("https://www.instagram.com/accounts/login/", null)]
    [TestCase("https://www.instagram.com/direct/inbox/", null)]
    [TestCase("https://www.threads.com/@zuck/post/C123", "threads")]
    [TestCase("not a url", Invalid)]
    public void InstagramTable(string input, string? expected) => AssertClassification(input, expected);

    // facebook_post_url_provider
    [TestCase("https://www.facebook.com/kevinloveofficial/posts/pfbid0nWhZeiMVjz", "facebook-post")]
    [TestCase("https://facebook.com/kevinloveofficial/posts/pfbid0nWhZeiMVjz", "facebook-post")]
    [TestCase("https://www.facebook.com/kevinloveofficial/posts/pfbid0nWhZeiMVjz/", "facebook-post")]
    [TestCase("http://www.facebook.com/kevinloveofficial/posts/pfbid0nWhZeiMVjz", "facebook-post")]
    [TestCase("https://www.facebook.com/123456789/posts/987654321", "facebook-post")]
    [TestCase("https://www.facebook.com/", null)]
    [TestCase("https://www.facebook.com/kevinloveofficial", null)]
    [TestCase("https://www.facebook.com/reel/3305054673010377", "facebook-video")]
    [TestCase("https://www.instagram.com/p/ABC123", "instagram")]
    [TestCase("not a url", Invalid)]
    public void FacebookPostTable(string input, string? expected) => AssertClassification(input, expected);

    // facebook_video_url_provider
    [TestCase("https://www.facebook.com/reel/3305054673010377", "facebook-video")]
    [TestCase("https://facebook.com/reel/3305054673010377", "facebook-video")]
    [TestCase("https://www.facebook.com/reel/3305054673010377/", "facebook-video")]
    [TestCase("http://www.facebook.com/reel/3305054673010377", "facebook-video")]
    [TestCase("https://www.facebook.com/", null)]
    [TestCase("https://www.facebook.com/kevinloveofficial", null)]
    [TestCase("https://www.facebook.com/kevinloveofficial/posts/pfbid0nWhZeiMVjz", "facebook-post")]
    [TestCase("https://www.instagram.com/reel/ABC123", "instagram")]
    [TestCase("not a url", Invalid)]
    public void FacebookVideoTable(string input, string? expected) => AssertClassification(input, expected);

    // Extras beyond the WordPress tables.
    [TestCase("https://www.instagram.com/reels/ABC123/", null, TestName = "Extras(plural reels is not a post)")]
    [TestCase("https://www.instagram.com/tv/ABC123/", null, TestName = "Extras(tv is excluded, parity with the plugin)")]
    [TestCase("https://instagr.am/p/fA9uwTtkSN/", null, TestName = "Extras(instagr.am short host is unsupported)")]
    [TestCase("https://www.facebook.com/reel/abc", null, TestName = "Extras(reel id must be numeric)")]
    [TestCase("https://www.facebook.com/watch/?v=3305054673010377", null, TestName = "Extras(watch URLs are unsupported)")]
    [TestCase("https://www.threads.com/@zuck/post/C123?utm_source=share", "threads", TestName = "Extras(threads query kept)")]
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/?igsh=abc#frag", "instagram", TestName = "Extras(fragment and query tolerated)")]
    [TestCase("https://WWW.INSTAGRAM.COM/P/fA9uwTtkSN/", "instagram", TestName = "Extras(case-insensitive match)")]
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/extra", null, TestName = "Extras(trailing segment rejected)")]
    [TestCase("https://www.instagram.com/p/fA9uw TtkSN/", null, TestName = "Extras(space in shortcode rejected)")]
    [TestCase("https://user@www.instagram.com/p/fA9uwTtkSN/", Invalid, TestName = "Extras(userinfo rejected before matching)")]
    [TestCase("https://www.instagram.com:8080/p/fA9uwTtkSN/", Invalid, TestName = "Extras(port rejected before matching)")]
    [TestCase("https://93.184.216.34/p/fA9uwTtkSN/", Invalid, TestName = "Extras(IP host rejected before matching)")]
    [TestCase("javascript:alert(1)", Invalid, TestName = "Extras(javascript scheme rejected)")]
    [TestCase("ftp://www.instagram.com/p/fA9uwTtkSN/", Invalid, TestName = "Extras(ftp scheme rejected)")]
    [TestCase("/p/fA9uwTtkSN/", Invalid, TestName = "Extras(relative rejected)")]
    [TestCase("", Invalid, TestName = "Extras(blank rejected)")]
    public void Extras(string input, string? expected) => AssertClassification(input, expected);

    [Test]
    public void FixtureSourceUrls_MapToTheirEndpoints()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Classify(TestFixtures.SourceUrls[TestFixtures.InstagramPost]), Is.EqualTo("instagram"));
            Assert.That(Classify(TestFixtures.SourceUrls[TestFixtures.InstagramReel]), Is.EqualTo("instagram"));
            Assert.That(Classify(TestFixtures.SourceUrls[TestFixtures.ThreadsPost]), Is.EqualTo("threads"));
            Assert.That(Classify(TestFixtures.SourceUrls[TestFixtures.FacebookPost]), Is.EqualTo("facebook-post"));
            Assert.That(Classify(TestFixtures.SourceUrls[TestFixtures.FacebookVideo]), Is.EqualTo("facebook-video"));
        });
    }

    [Test]
    public void TooLongInput_IsRejectedBeforeMatching()
    {
        var input = "https://www.instagram.com/p/" + new string('a', 2100);

        Assert.That(Classify(input), Is.EqualTo(Invalid));
    }

    [Test]
    public void DefaultOrder_IsThreadsInstagramFacebookPostFacebookVideo()
    {
        Assert.That(registry.Endpoints.Select(e => e.Key), Is.EqualTo(new[] { "threads", "instagram", "facebook-post", "facebook-video" }));
    }

    [Test]
    public void Get_IsCaseInsensitiveAndNullSafe()
    {
        Assert.Multiple(() =>
        {
            Assert.That(registry.Get("instagram"), Is.SameAs(MetaOEmbedEndpoints.Instagram));
            Assert.That(registry.Get("INSTAGRAM"), Is.SameAs(MetaOEmbedEndpoints.Instagram));
            Assert.That(registry.Get("Facebook-Video"), Is.SameAs(MetaOEmbedEndpoints.FacebookVideo));
            Assert.That(registry.Get("nope"), Is.Null);
            Assert.That(registry.Get(null!), Is.Null);
        });
    }

    [Test]
    public void Endpoints_ExposeExpectedUrisForDefaultOptions()
    {
        var options = new MetaEmbedsOptions();

        Assert.Multiple(() =>
        {
            Assert.That(MetaOEmbedEndpoints.Threads.EndpointUri(options).AbsoluteUri, Is.EqualTo("https://graph.threads.com/oembed"));
            Assert.That(MetaOEmbedEndpoints.Instagram.EndpointUri(options).AbsoluteUri, Is.EqualTo("https://graph.facebook.com/v25.0/instagram_oembed"));
            Assert.That(MetaOEmbedEndpoints.FacebookPost.EndpointUri(options).AbsoluteUri, Is.EqualTo("https://graph.facebook.com/v25.0/oembed_post"));
            Assert.That(MetaOEmbedEndpoints.FacebookVideo.EndpointUri(options).AbsoluteUri, Is.EqualTo("https://graph.facebook.com/v25.0/oembed_video"));
            Assert.That(MetaOEmbedEndpoints.Threads.SdkScriptUri(options).AbsoluteUri, Is.EqualTo("https://www.threads.com/embed.js"));
            Assert.That(MetaOEmbedEndpoints.Instagram.SdkScriptUri(options).AbsoluteUri, Is.EqualTo("https://www.instagram.com/embed.js"));
            Assert.That(MetaOEmbedEndpoints.FacebookPost.SdkScriptUri(options).AbsoluteUri, Is.EqualTo("https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v25.0"));
            Assert.That(MetaOEmbedEndpoints.FacebookVideo.SdkScriptUri(options).AbsoluteUri, Is.EqualTo("https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v25.0"));
        });
    }

    [Test]
    public void Endpoints_FollowGraphVersionAndLocaleOptions()
    {
        var options = new MetaEmbedsOptions { GraphApiVersion = "v26.0", FacebookSdkLocale = "cs_CZ" };

        Assert.Multiple(() =>
        {
            Assert.That(MetaOEmbedEndpoints.Instagram.EndpointUri(options).AbsoluteUri, Is.EqualTo("https://graph.facebook.com/v26.0/instagram_oembed"));
            Assert.That(MetaOEmbedEndpoints.FacebookPost.SdkScriptUri(options).AbsoluteUri, Is.EqualTo("https://connect.facebook.net/cs_CZ/sdk.js#xfbml=1&version=v26.0"));
            Assert.That(MetaOEmbedEndpoints.Threads.EndpointUri(options).AbsoluteUri, Is.EqualTo("https://graph.threads.com/oembed"));
        });
    }

    [TestCase("/v26.0")]
    [TestCase("v26.0/../../evil")]
    [TestCase("evil.com")]
    [TestCase("@evil.com")]
    [TestCase("v26")]
    [TestCase("26.0")]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("https://evil.com/v26.0")]
    public void Endpoints_MalformedGraphVersion_FallsBackToDefaultAndKeepsTheHost(string version)
    {
        var options = new MetaEmbedsOptions { GraphApiVersion = version };

        Assert.Multiple(() =>
        {
            foreach (var endpoint in new[] { MetaOEmbedEndpoints.Instagram, MetaOEmbedEndpoints.FacebookPost, MetaOEmbedEndpoints.FacebookVideo })
            {
                var uri = endpoint.EndpointUri(options);
                Assert.That(uri.Host, Is.EqualTo("graph.facebook.com"), endpoint.Key);
                Assert.That(uri.AbsolutePath, Does.StartWith("/v25.0/"), endpoint.Key);
            }

            Assert.That(MetaOEmbedEndpoints.FacebookSdk(options).AbsoluteUri, Does.EndWith("version=v25.0"));
        });
    }

    [TestCase("/en_US")]
    [TestCase("en")]
    [TestCase("en_USA")]
    [TestCase("en-US")]
    [TestCase("@evil.com")]
    [TestCase("")]
    [TestCase("../../evil")]
    public void Endpoints_MalformedSdkLocale_FallsBackToDefaultAndKeepsTheHost(string locale)
    {
        var sdk = MetaOEmbedEndpoints.FacebookSdk(new MetaEmbedsOptions { FacebookSdkLocale = locale });

        Assert.Multiple(() =>
        {
            Assert.That(sdk.Host, Is.EqualTo("connect.facebook.net"));
            Assert.That(sdk.AbsoluteUri, Is.EqualTo("https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v25.0"));
        });
    }

    [Test]
    public void Endpoints_WellFormedGraphVersionAndLocale_AreUsedVerbatim()
    {
        var options = new MetaEmbedsOptions { GraphApiVersion = "v3.11", FacebookSdkLocale = "pt_BR" };

        Assert.Multiple(() =>
        {
            Assert.That(MetaOEmbedEndpoints.Instagram.EndpointUri(options).AbsoluteUri, Is.EqualTo("https://graph.facebook.com/v3.11/instagram_oembed"));
            Assert.That(MetaOEmbedEndpoints.FacebookSdk(options).AbsoluteUri, Is.EqualTo("https://connect.facebook.net/pt_BR/sdk.js#xfbml=1&version=v3.11"));
        });
    }

    private void AssertClassification(string input, string? expected) =>
        Assert.That(Classify(input), Is.EqualTo(expected), input);

    private string? Classify(string input)
    {
        if (!matcher.TryParse(input, out var normalized, out _))
        {
            return Invalid;
        }

        return registry.Match(normalized)?.Key;
    }
}
