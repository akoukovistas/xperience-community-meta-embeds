using System.Text.RegularExpressions;

using AngleSharp.Dom;
using AngleSharp.Html.Parser;

using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Providers.OEmbed;
using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class SanitizerTests
{
    private static readonly string[] ScriptMarkers = ["<script", "embeds.js", "embed.js", "sdk.js", "platform.instagram.com", "connect.facebook.net"];

    private static readonly Regex UrlAttributes = new(
        "(?<name>href|data-instgrm-permalink|data-text-post-permalink|data-href)=\"(?<value>[^\"]*)\"",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private EmbedHtmlSanitizer sanitizer = null!;

    /// <summary>The five success fixtures with the endpoint each was captured for.</summary>
    public static IEnumerable<TestCaseData> Fixtures()
    {
        yield return new TestCaseData(TestFixtures.InstagramPost, MetaOEmbedEndpoints.Instagram).SetName("{m}(instagram-post)");
        yield return new TestCaseData(TestFixtures.InstagramReel, MetaOEmbedEndpoints.Instagram).SetName("{m}(instagram-reel)");
        yield return new TestCaseData(TestFixtures.ThreadsPost, MetaOEmbedEndpoints.Threads).SetName("{m}(threads-post)");
        yield return new TestCaseData(TestFixtures.FacebookPost, MetaOEmbedEndpoints.FacebookPost).SetName("{m}(facebook-post)");
        yield return new TestCaseData(TestFixtures.FacebookVideo, MetaOEmbedEndpoints.FacebookVideo).SetName("{m}(facebook-video)");
    }

    [SetUp]
    public void SetUp() => sanitizer = new EmbedHtmlSanitizer();

    // ---------------------------------------------------------------------------------------------------------
    // Golden fixtures
    // ---------------------------------------------------------------------------------------------------------

    [TestCaseSource(nameof(Fixtures))]
    public void Fixture_ScriptTagIsGoneAndListedInRemovedTags(string fixture, MetaOEmbedEndpoint endpoint)
    {
        var raw = TestFixtures.ReadHtml(fixture);
        Assume.That(raw, Does.Contain("<script"), "every live capture ends with Meta's script tag");

        var result = sanitizer.Sanitize(raw, endpoint);

        Assert.Multiple(() =>
        {
            foreach (var marker in ScriptMarkers)
            {
                Assert.That(result.Html, Does.Not.Contain(marker).IgnoreCase, marker);
            }

            Assert.That(result.RemovedTags, Has.Member("script"));
            Assert.That(result.RemovedTags, Is.Unique);
        });
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Fixture_RootElementIsPresent(string fixture, MetaOEmbedEndpoint endpoint)
    {
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(fixture), endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True);
            Assert.That(result.Html, Is.Not.Empty);
            Assert.That(Parse(result.Html).QuerySelector(endpoint.ExpectedRootSelector), Is.Not.Null);
        });
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Fixture_UrlAttributesArePreservedByteForByte(string fixture, MetaOEmbedEndpoint endpoint)
    {
        var raw = TestFixtures.ReadHtml(fixture);
        var expected = UrlAttributes.Matches(raw)
            .Select(m => m.Value)
            .Where(v => !v.Contains("sdk.js", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        Assume.That(expected, Is.Not.Empty);

        var result = sanitizer.Sanitize(raw, endpoint);

        Assert.Multiple(() =>
        {
            foreach (var attribute in expected)
            {
                Assert.That(result.Html, Does.Contain(attribute), attribute);
            }

            if (fixture is not (TestFixtures.FacebookPost or TestFixtures.FacebookVideo))
            {
                Assert.That(result.Html, Does.Contain("utm_source="));
                Assert.That(result.Html, Does.Contain("&amp;utm_campaign="), "the entity in the query string must be kept as written");
            }
        });
    }

    [TestCase(TestFixtures.InstagramPost)]
    [TestCase(TestFixtures.InstagramReel)]
    public void Instagram_KeepsCaptionedBooleanAttributeAndSvgLogo(string fixture)
    {
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(fixture), MetaOEmbedEndpoints.Instagram);
        var root = Parse(result.Html).QuerySelector("blockquote.instagram-media");

        Assert.Multiple(() =>
        {
            Assert.That(root, Is.Not.Null);
            Assert.That(root!.HasAttribute("data-instgrm-captioned"), Is.True);
            Assert.That(root.GetAttribute("data-instgrm-version"), Is.EqualTo("14"));
            Assert.That(result.Html, Does.Contain("<svg "));
            Assert.That(result.Html, Does.Contain("viewBox=\"0 0 60 60\""));
            Assert.That(result.Html, Does.Contain("xmlns:xlink="));
            Assert.That(result.Html, Does.Contain("<path d=\"M556.869"));
            Assert.That(result.Html, Does.Contain("fill-rule=\"evenodd\""));
        });
    }

    [Test]
    public void Threads_KeepsIdThemeAndVersion()
    {
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(TestFixtures.ThreadsPost), MetaOEmbedEndpoints.Threads);
        var root = Parse(result.Html).QuerySelector("blockquote.text-post-media");

        Assert.Multiple(() =>
        {
            Assert.That(root, Is.Not.Null);
            Assert.That(root!.GetAttribute("id"), Is.EqualTo("ig-tp-DWjTI0cgH5O"));
            Assert.That(root.GetAttribute("data-theme"), Is.EqualTo("light"));
            Assert.That(root.GetAttribute("data-text-post-version"), Is.EqualTo("0"));
            Assert.That(result.Html, Does.Contain("<svg "));
            Assert.That(result.Html, Does.Contain("View on Threads"));
        });
    }

    [TestCase(TestFixtures.FacebookPost, "div.fb-post")]
    [TestCase(TestFixtures.FacebookVideo, "div.fb-video")]
    public void Facebook_KeepsFbRootAndDataAttributes(string fixture, string rootSelector)
    {
        var endpoint = fixture == TestFixtures.FacebookPost ? MetaOEmbedEndpoints.FacebookPost : MetaOEmbedEndpoints.FacebookVideo;
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(fixture), endpoint);
        var document = Parse(result.Html);

        Assert.Multiple(() =>
        {
            Assert.That(document.QuerySelector("div#fb-root"), Is.Not.Null, "the widget de-duplicates fb-root per page, the sanitiser keeps it");
            var root = document.QuerySelector(rootSelector);
            Assert.That(root, Is.Not.Null);
            Assert.That(root!.GetAttribute("data-href"), Does.StartWith("https://www.facebook.com/"));
            if (fixture == TestFixtures.FacebookPost)
            {
                Assert.That(root.GetAttribute("data-width"), Is.EqualTo("552"));
            }

            Assert.That(document.Body!.QuerySelectorAll("*").Count(e => e.LocalName != "div"), Is.Zero, "only div survives the facebook profile");
        });
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Fixture_IsIdempotent(string fixture, MetaOEmbedEndpoint endpoint)
    {
        var first = sanitizer.Sanitize(TestFixtures.ReadHtml(fixture), endpoint);
        var second = sanitizer.Sanitize(first.Html, endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(second.Html, Is.EqualTo(first.Html));
            Assert.That(second.RootElementPresent, Is.True);
            Assert.That(second.RemovedTags, Is.Empty, "a clean document has nothing left to remove");
        });
    }

    [TestCase(TestFixtures.InstagramPost)]
    [TestCase(TestFixtures.ThreadsPost)]
    public void Fixture_AnchorsOpeningNewWindowGetNoopenerNoreferrer(string fixture)
    {
        var endpoint = fixture == TestFixtures.InstagramPost ? MetaOEmbedEndpoints.Instagram : MetaOEmbedEndpoints.Threads;
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(fixture), endpoint);
        var anchors = Parse(result.Html).QuerySelectorAll("a[target]").ToList();

        Assert.That(anchors, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var anchor in anchors)
            {
                Assert.That(anchor.GetAttribute("rel"), Is.EqualTo("noopener noreferrer"));
                Assert.That(anchor.GetAttribute("target"), Is.EqualTo("_blank"));
            }
        });
    }

    [Test]
    public void Fixture_InlineStylesSurviveTheCssFilter()
    {
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(TestFixtures.InstagramPost), MetaOEmbedEndpoints.Instagram);

        Assert.Multiple(() =>
        {
            Assert.That(result.Html, Does.Contain("display: flex"));
            Assert.That(result.Html, Does.Contain("background: rgba(255, 255, 255, 1)"), "the shorthand must stay intact; expansion into longhands means a longhand is missing from the allowlist");
            Assert.That(result.Html, Does.Contain("border-radius: 3px"));
            Assert.That(result.Html, Does.Contain("max-width: 658px"));
            Assert.That(result.Html, Does.Contain("transform: rotate(-45deg)"));
            Assert.That(result.Html, Does.Contain("View this post on Instagram"));
        });
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Fixture_HasNoEventHandlersOrScriptUrls(string fixture, MetaOEmbedEndpoint endpoint)
    {
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(fixture), endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.Html, Does.Not.Match(@"\son[a-z]+\s*=").IgnoreCase);
            Assert.That(result.Html, Does.Not.Contain("javascript:").IgnoreCase);
            Assert.That(result.Html, Does.Not.Contain("data:").IgnoreCase);
            Assert.That(result.Html, Does.Not.Match("(href|data-[a-z-]+|src)=\"http:").IgnoreCase, "URL attributes must be https (xmlns is not a URL attribute)");
        });
    }

    // ---------------------------------------------------------------------------------------------------------
    // Mutations: start from a fixture, inject a payload
    // ---------------------------------------------------------------------------------------------------------

    [TestCase(" onerror=\"alert(1)\"")]
    [TestCase(" onclick=\"alert(1)\"")]
    [TestCase(" ONLOAD=\"alert(1)\"")]
    [TestCase(" onmouseover=alert(1)")]
    public void Mutation_EventHandlerAttributesAreRemoved(string payload)
    {
        var raw = InjectIntoRoot(TestFixtures.InstagramPost, payload);

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True);
            Assert.That(result.Html, Does.Not.Match(@"\son[a-z]+\s*=").IgnoreCase);
            Assert.That(result.Html, Does.Not.Contain("alert(1)"));
        });
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("JaVaScRiPt:alert(1)")]
    [TestCase("java\tscript:alert(1)")]
    [TestCase("data:text/html,<script>alert(1)</script>")]
    [TestCase("vbscript:msgbox(1)")]
    [TestCase("https://evil.example/")]
    [TestCase("https://www.instagram.com.evil.example/p/x/")]
    [TestCase("https://www.instagram.com@evil.example/p/x/")]
    [TestCase("https://evil.example/?u=https://www.instagram.com/")]
    [TestCase("http://www.instagram.com/p/x/")]
    [TestCase("//www.instagram.com/p/x/")]
    [TestCase("/p/x/")]
    [TestCase("https://www.instagram.com:8443/p/x/")]
    public void Mutation_HrefOutsideTheAllowlistIsRemoved(string href)
    {
        var raw = InjectChild(TestFixtures.InstagramPost, $"<a id=\"probe\" href=\"{href}\" target=\"_blank\">x</a>");

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);
        var anchors = Parse(result.Html).QuerySelectorAll("a").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True);
            Assert.That(anchors, Has.Count.EqualTo(2), "Meta's anchor plus the injected one");
            Assert.That(anchors.Count(a => a.HasAttribute("href")), Is.EqualTo(1), "only Meta's anchor keeps its href");
            Assert.That(result.Html, Does.Not.Contain("evil.example"));
            Assert.That(result.Html, Does.Not.Contain("alert(1)"));
            Assert.That(result.Html, Does.Not.Contain("script:").IgnoreCase);
            Assert.That(result.Html, Does.Not.Contain("id=\"probe\""), "id is not an allowed attribute for instagram");
        });
    }

    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/")]
    [TestCase("https://instagram.com/reel/abc/?x=1&amp;y=2")]
    [TestCase("https://help.instagram.com/")]
    public void Mutation_HrefOnAnAllowedHostIsKeptVerbatim(string href)
    {
        var raw = InjectChild(TestFixtures.InstagramPost, $"<a href=\"{href}\">probe-anchor</a>");

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);

        Assert.That(result.Html, Does.Contain($"href=\"{href}\">probe-anchor</a>"));
    }

    [Test]
    public void Mutation_PermalinkDataAttributeOnForeignHostIsRemoved()
    {
        var raw = TestFixtures.ReadHtml(TestFixtures.InstagramPost)
            .Replace("data-instgrm-permalink=\"https://www.instagram.com/", "data-instgrm-permalink=\"https://evil.example/", StringComparison.Ordinal);

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);
        var root = Parse(result.Html).QuerySelector("blockquote.instagram-media");

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True);
            Assert.That(root!.HasAttribute("data-instgrm-permalink"), Is.False);
            Assert.That(result.Html, Does.Not.Contain("evil.example"));
        });
    }

    [Test]
    public void Mutation_ForbiddenElementsAreRemovedAndListed()
    {
        var payload = "<iframe src=\"https://evil.example/\" srcdoc=\"&lt;script&gt;alert(1)&lt;/script&gt;\"></iframe>"
            + "<object data=\"https://evil.example/x.swf\"></object>"
            + "<embed src=\"https://evil.example/x.swf\">"
            + "<style>@import url(https://evil.example/x.css); a { behavior: url(x.htc) }</style>"
            + "<form action=\"https://evil.example/\"><input name=\"q\"></form>"
            + "<link rel=\"stylesheet\" href=\"https://evil.example/x.css\">"
            + "<meta http-equiv=\"refresh\" content=\"0;url=https://evil.example/\">"
            + "<base href=\"https://evil.example/\">"
            + "<script>alert(1)</script>"
            + "<img src=\"x\" onerror=\"alert(1)\">"
            + "<svg><script>alert(1)</script><foreignObject><body onload=\"alert(1)\"></body></foreignObject></svg>";
        var raw = InjectChild(TestFixtures.InstagramPost, payload);

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True);
            foreach (var tag in new[] { "iframe", "object", "embed", "style", "form", "input", "link", "meta", "base", "script", "img", "foreignObject" })
            {
                Assert.That(result.RemovedTags, Has.Member(tag), tag);
                Assert.That(result.Html, Does.Not.Contain("<" + tag).IgnoreCase, tag);
            }

            Assert.That(result.Html, Does.Not.Contain("evil.example"));
            Assert.That(result.Html, Does.Not.Contain("alert(1)"));
            Assert.That(result.Html, Does.Not.Contain("srcdoc"));
            Assert.That(result.Html, Does.Not.Contain("@import"));
        });
    }

    [Test]
    public void Mutation_SrcdocAndSrcOnAllowedElementsAreRemoved()
    {
        var raw = InjectChild(TestFixtures.InstagramPost, "<div srcdoc=\"&lt;script&gt;alert(1)&lt;/script&gt;\" src=\"https://www.instagram.com/x.js\">probe-div</div>");

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);

        Assert.Multiple(() =>
        {
            Assert.That(result.Html, Does.Contain("<div>probe-div</div>"));
            Assert.That(result.Html, Does.Not.Contain("srcdoc"));
            Assert.That(result.Html, Does.Not.Contain("src="));
        });
    }

    [TestCase("background:url(javascript:alert(1))")]
    [TestCase("background-image:url('https://evil.example/x.png')")]
    [TestCase("background-image:url(https://www.instagram.com/x.png)")]
    [TestCase("background:u\\72l(javascript:alert(1))")]
    [TestCase("color:expression(alert(1))")]
    [TestCase("width:expression(alert(1))")]
    [TestCase("behavior:url(x.htc)")]
    [TestCase("-moz-binding:url(https://evil.example/x.xml#xss)")]
    [TestCase("background-image:image-set('https://evil.example/x.png' 1x)")]
    [TestCase("position:fixed;top:0;left:0;width:100vw;height:100vh;z-index:99999")]
    [TestCase("opacity:0;pointer-events:none")]
    public void Mutation_DangerousCssIsRemovedFromStyle(string css)
    {
        var raw = InjectChild(TestFixtures.InstagramPost, $"<div style=\"{css}\">probe-div</div>");

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);
        var probe = Parse(result.Html).QuerySelectorAll("div").Single(d => d.TextContent == "probe-div");
        var style = probe.GetAttribute("style") ?? string.Empty;
        var declaredProperties = style
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(declaration => declaration.Split(':', 2)[0].Trim().ToLowerInvariant())
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True);
            Assert.That(style, Does.Not.Contain("url(").IgnoreCase);
            Assert.That(style, Does.Not.Contain("expression").IgnoreCase);
            Assert.That(style, Does.Not.Contain("image-set").IgnoreCase);
            Assert.That(declaredProperties, Has.No.Member("behavior"));
            Assert.That(declaredProperties, Has.No.Member("-moz-binding"));
            Assert.That(declaredProperties, Has.No.Member("position"));
            Assert.That(declaredProperties, Has.No.Member("top").And.No.Member("left"));
            Assert.That(declaredProperties, Has.No.Member("z-index"));
            Assert.That(declaredProperties, Has.No.Member("opacity"));
            Assert.That(declaredProperties, Has.No.Member("pointer-events"));
            Assert.That(result.Html, Does.Not.Contain("evil.example"));
            Assert.That(result.Html, Does.Not.Contain("alert(1)"));
        });
    }

    [Test]
    public void Mutation_UnlistedClassTokensAreDroppedButRootSurvives()
    {
        var raw = TestFixtures.ReadHtml(TestFixtures.InstagramPost)
            .Replace("class=\"instagram-media\"", "class=\"evil instagram-media fb-post\"", StringComparison.Ordinal);

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);
        var root = Parse(result.Html).QuerySelector("blockquote");

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True);
            Assert.That(root!.GetAttribute("class"), Is.EqualTo("instagram-media"));
        });
    }

    [Test]
    public void Mutation_RootClassReplaced_FailsClosed()
    {
        var raw = TestFixtures.ReadHtml(TestFixtures.InstagramPost)
            .Replace("class=\"instagram-media\"", "class=\"instagram-medium\"", StringComparison.Ordinal);

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.Html, Does.Not.Contain("instagram-medium"), "unknown classes are dropped");
            Assert.That(result.Html, Does.Contain("<blockquote"), "the markup itself is still returned for logging");
        });
    }

    [Test]
    public void Mutation_RootRemoved_FailsClosed()
    {
        var raw = "<div>" + TestFixtures.ReadHtml(TestFixtures.FacebookPost).Replace("fb-post", "fb-page", StringComparison.Ordinal) + "</div>";

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.FacebookPost);

        Assert.That(result.RootElementPresent, Is.False);
    }

    [Test]
    public void Mutation_InstagramMarkupThroughThreadsEndpoint_FailsClosed()
    {
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(TestFixtures.InstagramPost), MetaOEmbedEndpoints.Threads);

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.Html, Does.Not.Contain("instagram-media"), "the threads profile does not allow that class");
            Assert.That(result.Html, Does.Not.Contain("data-instgrm-permalink"), "the threads profile does not allow that attribute");
            Assert.That(result.Html, Does.Not.Contain("<script"));
        });
    }

    [Test]
    public void Mutation_ThreadsMarkupThroughFacebookEndpoint_FailsClosedAndKeepsOnlyDivs()
    {
        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(TestFixtures.ThreadsPost), MetaOEmbedEndpoints.FacebookPost);

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.Html, Does.Not.Contain("<blockquote"));
            Assert.That(result.Html, Does.Not.Contain("<a"));
            Assert.That(result.Html, Does.Not.Contain("<svg"));
            Assert.That(result.RemovedTags, Is.SupersetOf(new[] { "blockquote", "a", "svg", "path", "script" }));
        });
    }

    // ---------------------------------------------------------------------------------------------------------
    // Threads specifics
    // ---------------------------------------------------------------------------------------------------------

    [TestCase("https://www.threads.net/t/DWjTI0cgH5O", true)]
    [TestCase("https://threads.com/@threads/post/DWjTI0cgH5O", true)]
    [TestCase("https://www.threads.com.evil.example/t/x", false)]
    [TestCase("https://www.instagram.com/p/x/", false)]
    [TestCase("http://www.threads.com/t/x", false)]
    public void Threads_HrefHostAllowlist(string href, bool kept)
    {
        var raw = InjectChild(TestFixtures.ThreadsPost, $"<a href=\"{href}\">probe-anchor</a>");

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Threads);

        Assert.That(result.Html.Contains($"href=\"{href}\">probe-anchor</a>", StringComparison.Ordinal), Is.EqualTo(kept));
    }

    [TestCase("ig-tp-DWjTI0cgH5O", true)]
    [TestCase("ig-tp-abc_DEF-123", true)]
    [TestCase("fb-root", false)]
    [TestCase("location", false)]
    [TestCase("ig-tp-", false)]
    [TestCase("ig-tp-with space", false)]
    public void Threads_IdMustLookLikeAThreadsPostId(string id, bool kept)
    {
        var raw = TestFixtures.ReadHtml(TestFixtures.ThreadsPost).Replace("id=\"ig-tp-DWjTI0cgH5O\"", $"id=\"{id}\"", StringComparison.Ordinal);

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Threads);
        var root = Parse(result.Html).QuerySelector("blockquote.text-post-media");

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True);
            Assert.That(root!.HasAttribute("id"), Is.EqualTo(kept));
        });
    }

    // ---------------------------------------------------------------------------------------------------------
    // Facebook specifics
    // ---------------------------------------------------------------------------------------------------------

    [TestCase("https://www.facebook.com/zuck/posts/1", true)]
    [TestCase("https://m.facebook.com/reel/1", true)]
    [TestCase("https://facebook.com/reel/1", true)]
    [TestCase("http://www.facebook.com/zuck/posts/1", false)]
    [TestCase("https://evil.example/", false)]
    [TestCase("https://www.facebook.com.evil.example/", false)]
    [TestCase("javascript:alert(1)", false)]
    [TestCase("//www.facebook.com/zuck/posts/1", false)]
    public void Facebook_DataHrefHostAllowlist(string dataHref, bool kept)
    {
        var raw = TestFixtures.ReadHtml(TestFixtures.FacebookPost)
            .Replace("data-href=\"https://www.facebook.com/zuck/posts/10102577175875681\"", $"data-href=\"{dataHref}\"", StringComparison.Ordinal);

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.FacebookPost);
        var root = Parse(result.Html).QuerySelector("div.fb-post");

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.True, "the root is still there; only the attribute is dropped");
            Assert.That(root!.HasAttribute("data-href"), Is.EqualTo(kept));
            if (kept)
            {
                Assert.That(root.GetAttribute("data-href"), Is.EqualTo(dataHref));
            }
        });
    }

    [Test]
    public void Facebook_IdOtherThanFbRootIsRemovedAndUnknownClassesDropped()
    {
        var raw = TestFixtures.ReadHtml(TestFixtures.FacebookPost)
            .Replace("id=\"fb-root\"", "id=\"location\"", StringComparison.Ordinal)
            .Replace("class=\"fb-post\"", "class=\"fb-post fb-like evil\" id=\"fb-root\"", StringComparison.Ordinal);

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.FacebookPost);
        var document = Parse(result.Html);

        Assert.Multiple(() =>
        {
            Assert.That(document.QuerySelector("#location"), Is.Null);
            Assert.That(document.QuerySelector("div.fb-post")!.GetAttribute("class"), Is.EqualTo("fb-post"));
            Assert.That(document.QuerySelector("div.fb-post")!.GetAttribute("id"), Is.EqualTo("fb-root"), "fb-root is the one id value the facebook profile accepts, wherever it sits");
            Assert.That(result.Html, Does.Not.Contain("fb-like"));
            Assert.That(result.Html, Does.Not.Contain("evil"));
        });
    }

    // ---------------------------------------------------------------------------------------------------------
    // Profiles and contract edge cases
    // ---------------------------------------------------------------------------------------------------------

    [Test]
    public void UnknownProfile_UsesStrictestAllowlist_AndFailsClosedForNonFacebookMarkup()
    {
        var endpoint = MetaOEmbedEndpoints.Instagram with { SanitizerProfile = "tiktok" };

        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(TestFixtures.InstagramPost), endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.Html, Does.Not.Contain("<blockquote"));
            Assert.That(result.Html, Does.Not.Contain("<a"));
            Assert.That(result.Html, Does.Not.Contain("<script"));
            Assert.That(result.RemovedTags, Has.Member("blockquote").And.Member("script"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("FACEBOOK")]
    public void UnknownOrEmptyProfile_StillAcceptsFacebookShapedMarkup(string? profileName)
    {
        var endpoint = MetaOEmbedEndpoints.FacebookPost with { SanitizerProfile = profileName! };

        var result = sanitizer.Sanitize(TestFixtures.ReadHtml(TestFixtures.FacebookPost), endpoint);

        Assert.That(result.RootElementPresent, Is.True);
    }

    [TestCase("")]
    [TestCase(" \t\r\n ")]
    [TestCase(null)]
    public void EmptyOrWhitespaceInput_ReturnsEmptyAndFailsClosed(string? raw)
    {
        var result = sanitizer.Sanitize(raw!, MetaOEmbedEndpoints.Instagram);

        Assert.Multiple(() =>
        {
            Assert.That(result.Html, Is.Empty);
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.RemovedTags, Is.Empty);
        });
    }

    [TestCase("<script>alert(1)</script>")]
    [TestCase("just text, no elements")]
    [TestCase("<!-- a comment -->")]
    [TestCase("<span>not allowed</span>")]
    public void InputWithNoSurvivingRoot_FailsClosed(string raw)
    {
        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);

        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.Html, Does.Not.Contain("<script"));
            Assert.That(result.Html, Does.Not.Contain("<span"));
            Assert.That(result.Html, Does.Not.Contain("<!--"));
        });
    }

    [Test]
    public void InvalidRootSelector_FailsClosedWithoutThrowing()
    {
        var endpoint = MetaOEmbedEndpoints.Instagram with { ExpectedRootSelector = "blockquote.[[[" };

        SanitizedEmbedHtml result = null!;
        Assert.DoesNotThrow(() => result = sanitizer.Sanitize(TestFixtures.ReadHtml(TestFixtures.InstagramPost), endpoint));
        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.Html, Does.Contain("<blockquote"));
        });
    }

    [Test]
    public void NullEndpoint_Throws()
    {
        Assert.That(() => sanitizer.Sanitize("<div></div>", null!), Throws.ArgumentNullException);
    }

    [Test]
    public void OneMegabyteOfJunk_ReturnsWithoutThrowing()
    {
        var random = new Random(20260904);
        var alphabet = "<>/=\"' abcdefghijklmnopqrstuvwxyz&;:()!-_.\n";
        var junk = string.Create(1024 * 1024, random, (span, rng) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[rng.Next(alphabet.Length)];
            }
        });

        SanitizedEmbedHtml result = null!;
        Assert.DoesNotThrow(() => result = sanitizer.Sanitize(junk, MetaOEmbedEndpoints.Instagram));
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.Html, Does.Not.Contain("<script"));
        });
    }

    [Test]
    public void OneMegabyteOfRepeatedPayloads_ReturnsWithoutThrowing()
    {
        var unit = "<div onclick=\"alert(1)\" style=\"background:url(javascript:alert(1))\"><script>alert(1)</script><iframe srcdoc=\"x\"></iframe><a href=\"javascript:alert(1)\">x</a></div>";
        var raw = string.Concat(Enumerable.Repeat(unit, 1024 * 1024 / unit.Length + 1));

        SanitizedEmbedHtml result = null!;
        Assert.DoesNotThrow(() => result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram));
        Assert.Multiple(() =>
        {
            Assert.That(result.RootElementPresent, Is.False);
            Assert.That(result.Html, Does.Not.Contain("alert(1)"));
            Assert.That(result.Html, Does.Not.Contain("<script"));
            Assert.That(result.RemovedTags, Is.EquivalentTo(new[] { "script", "iframe" }));
        });
    }

    [Test]
    public void RemovedTags_AreDistinctAndInDocumentOrder()
    {
        var raw = "<blockquote class=\"instagram-media\"><script>1</script><iframe></iframe><script>2</script><object></object><iframe></iframe></blockquote>";

        var result = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);

        Assert.That(result.RemovedTags, Is.EqualTo(new[] { "script", "iframe", "object" }));
    }

    // ---------------------------------------------------------------------------------------------------------
    // Concurrency smoke test
    // ---------------------------------------------------------------------------------------------------------

    [Test]
    public async Task ConcurrentCalls_ProduceIdenticalResults()
    {
        var raw = TestFixtures.ReadHtml(TestFixtures.InstagramPost);
        var mutated = InjectChild(TestFixtures.InstagramPost, "<iframe></iframe><object></object>");
        var expectedClean = sanitizer.Sanitize(raw, MetaOEmbedEndpoints.Instagram);
        var expectedMutated = sanitizer.Sanitize(mutated, MetaOEmbedEndpoints.Instagram);

        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            var results = new List<(SanitizedEmbedHtml Actual, SanitizedEmbedHtml Expected)>();
            for (var n = 0; n < 25; n++)
            {
                var useMutated = (i + n) % 2 == 0;
                var actual = sanitizer.Sanitize(useMutated ? mutated : raw, MetaOEmbedEndpoints.Instagram);
                results.Add((actual, useMutated ? expectedMutated : expectedClean));
            }

            return results;
        }));

        var all = (await Task.WhenAll(tasks)).SelectMany(r => r).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(all, Has.Count.EqualTo(200));
            foreach (var (actual, expected) in all)
            {
                Assert.That(actual.Html, Is.EqualTo(expected.Html));
                Assert.That(actual.RootElementPresent, Is.True);
                Assert.That(actual.RemovedTags, Is.EqualTo(expected.RemovedTags), "RemovedTags must not leak between concurrent calls");
            }
        });
    }

    // ---------------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------------

    private static IDocument Parse(string html) => new HtmlParser().ParseDocument("<body>" + html);

    /// <summary>Adds raw attribute text to the fixture's root element.</summary>
    private static string InjectIntoRoot(string fixture, string attributeText)
    {
        var raw = TestFixtures.ReadHtml(fixture);
        var index = raw.IndexOf("<blockquote", StringComparison.Ordinal);
        Assume.That(index, Is.GreaterThanOrEqualTo(0));
        return raw.Insert(index + "<blockquote".Length, attributeText);
    }

    /// <summary>Appends markup as the last child of the fixture's root element.</summary>
    private static string InjectChild(string fixture, string markup)
    {
        var raw = TestFixtures.ReadHtml(fixture);
        var index = raw.LastIndexOf("</blockquote>", StringComparison.Ordinal);
        Assume.That(index, Is.GreaterThanOrEqualTo(0));
        return raw.Insert(index, markup);
    }
}
