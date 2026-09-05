using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Providers.OEmbed;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class UrlMatcherTests
{
    private readonly EmbedUrlMatcher matcher = new();

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\r\n")]
    public void TryParse_Blank_IsRejectedAsEmpty(string? input)
    {
        var ok = matcher.TryParse(input, out var normalized, out var reason);

        Assert.That(ok, Is.False);
        Assert.That(normalized, Is.Null);
        Assert.That(reason, Is.EqualTo("empty"));
    }

    [TestCase("not a url", "absolute")]
    [TestCase("/p/fA9uwTtkSN/", "absolute")]
    [TestCase("www.instagram.com/p/fA9uwTtkSN/", "absolute")]
    [TestCase("javascript:alert(1)", "scheme")]
    [TestCase("ftp://www.instagram.com/p/fA9uwTtkSN/", "scheme")]
    [TestCase("file:///C:/p/fA9uwTtkSN/", "scheme")]
    [TestCase("https://user:pw@www.instagram.com/p/fA9uwTtkSN/", "userinfo")]
    [TestCase("https://user@www.instagram.com/p/fA9uwTtkSN/", "userinfo")]
    [TestCase("https://www.instagram.com:8443/p/fA9uwTtkSN/", "port")]
    [TestCase("http://www.instagram.com:443/p/fA9uwTtkSN/", "port")]
    [TestCase("https://127.0.0.1/p/fA9uwTtkSN/", "host")]
    [TestCase("https://[::1]/p/fA9uwTtkSN/", "host")]
    [TestCase("https://www.instagr\u00e1m.com/p/fA9uwTtkSN/", "host")]
    public void TryParse_Rejects(string input, string expectedReasonFragment)
    {
        var ok = matcher.TryParse(input, out var normalized, out var reason);

        Assert.That(ok, Is.False, input);
        Assert.That(normalized, Is.Null);
        Assert.That(reason, Does.Contain(expectedReasonFragment).IgnoreCase);
    }

    [Test]
    public void TryParse_LongerThan2048_IsRejected()
    {
        var input = "https://www.instagram.com/p/" + new string('a', 2049 - "https://www.instagram.com/p/".Length);
        Assert.That(input, Has.Length.EqualTo(2049));

        Assert.That(matcher.TryParse(input, out _, out var reason), Is.False);
        Assert.That(reason, Does.Contain("2048"));
    }

    [Test]
    public void TryParse_Exactly2048_IsAccepted()
    {
        var input = "https://www.instagram.com/p/" + new string('a', 2048 - "https://www.instagram.com/p/".Length);
        Assert.That(input, Has.Length.EqualTo(2048));

        Assert.That(matcher.TryParse(input, out var normalized, out _), Is.True);
        Assert.That(normalized!.AbsoluteUri, Is.EqualTo(input));
    }

    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/", "https://www.instagram.com/p/fA9uwTtkSN/")]
    [TestCase("http://instagram.com/p/fA9uwTtkSN", "http://instagram.com/p/fA9uwTtkSN")]
    [TestCase("  https://www.instagram.com/p/fA9uwTtkSN/  ", "https://www.instagram.com/p/fA9uwTtkSN/")]
    [TestCase("HTTPS://WWW.Instagram.COM/p/AbC123/", "https://www.instagram.com/p/AbC123/")]
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/#comments", "https://www.instagram.com/p/fA9uwTtkSN/")]
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/?igsh=abc&hl=en", "https://www.instagram.com/p/fA9uwTtkSN/?igsh=abc&hl=en")]
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/?igsh=abc#x", "https://www.instagram.com/p/fA9uwTtkSN/?igsh=abc")]
    [TestCase("https://www.instagram.com:443/p/fA9uwTtkSN/", "https://www.instagram.com/p/fA9uwTtkSN/")]
    [TestCase("http://www.instagram.com:80/p/fA9uwTtkSN/", "http://www.instagram.com/p/fA9uwTtkSN/")]
    [TestCase("https://www.threads.com/@zuck/post/C1234567890", "https://www.threads.com/@zuck/post/C1234567890")]
    [TestCase("https://www.facebook.com/zuck/posts/10102577175875681", "https://www.facebook.com/zuck/posts/10102577175875681")]
    [TestCase("https://www.instagram.com", "https://www.instagram.com/")]
    public void TryParse_Normalises(string input, string expected)
    {
        var ok = matcher.TryParse(input, out var normalized, out var reason);

        Assert.That(ok, Is.True, reason);
        Assert.That(reason, Is.Null);
        Assert.That(normalized!.AbsoluteUri, Is.EqualTo(expected));
    }

    [Test]
    public void TryParse_StripsFragmentButKeepsPathCase()
    {
        Assert.That(matcher.TryParse("https://WWW.INSTAGRAM.COM/reel/DTxk5orCKEv/#frag", out var normalized, out _), Is.True);

        Assert.That(normalized!.Fragment, Is.Empty);
        Assert.That(normalized.Host, Is.EqualTo("www.instagram.com"));
        Assert.That(normalized.AbsolutePath, Is.EqualTo("/reel/DTxk5orCKEv/"));
    }

    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/", "https://instagram.com/p/fA9uwTtkSN/")]
    [TestCase("https://instagram.com/p/fA9uwTtkSN/", "https://instagram.com/p/fA9uwTtkSN/")]
    [TestCase("http://www.instagram.com/p/fA9uwTtkSN", "https://instagram.com/p/fA9uwTtkSN/")]
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN/?igsh=abc&hl=en", "https://instagram.com/p/fA9uwTtkSN/")]
    [TestCase("https://www.instagram.com/p/fA9uwTtkSN?utm_source=share", "https://instagram.com/p/fA9uwTtkSN/")]
    [TestCase("HTTP://WWW.INSTAGRAM.COM/p/fA9uwTtkSN/#x", "https://instagram.com/p/fA9uwTtkSN/")]
    [TestCase("https://www.instagram.com/p/AbC/", "https://instagram.com/p/AbC/")]
    [TestCase("https://www.facebook.com/zuck/posts/10102577175875681", "https://facebook.com/zuck/posts/10102577175875681/")]
    [TestCase("https://threads.net/@zuck/post/DUGwwelEh_K", "https://threads.net/@zuck/post/DUGwwelEh_K/")]
    [TestCase("https://www.threads.com/t/DWjTI0cgH5O/", "https://threads.com/t/DWjTI0cgH5O/")]
    public void ToCacheForm_CanonicalisesEquivalentSpellings(string input, string expected)
    {
        Assert.That(matcher.TryParse(input, out var normalized, out _), Is.True);

        Assert.That(matcher.ToCacheForm(normalized!), Is.EqualTo(expected));
    }

    [Test]
    public void ToCacheForm_QueryVariantsShareOneForm()
    {
        var forms = new[]
            {
                "https://www.instagram.com/p/fA9uwTtkSN/",
                "https://www.instagram.com/p/fA9uwTtkSN",
                "http://instagram.com/p/fA9uwTtkSN/?igsh=MzRlODBiNWFlZA==",
                "https://www.instagram.com/p/fA9uwTtkSN/?hl=en#comments",
            }
            .Select(input =>
            {
                Assert.That(matcher.TryParse(input, out var normalized, out _), Is.True, input);
                return matcher.ToCacheForm(normalized!);
            })
            .Distinct()
            .ToList();

        Assert.That(forms, Is.EqualTo(new[] { "https://instagram.com/p/fA9uwTtkSN/" }));
    }

    [Test]
    public void ToCacheForm_DifferentPostsStayApart()
    {
        Assert.That(matcher.TryParse("https://www.instagram.com/p/fA9uwTtkSN/", out var a, out _), Is.True);
        Assert.That(matcher.TryParse("https://www.instagram.com/p/fA9uwTtkSn/", out var b, out _), Is.True);

        Assert.That(matcher.ToCacheForm(a!), Is.Not.EqualTo(matcher.ToCacheForm(b!)));
    }

    [Test]
    public void ToCacheForm_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => matcher.ToCacheForm(null!));
}
