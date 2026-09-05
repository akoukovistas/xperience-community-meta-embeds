using NUnit.Framework;

using System.Text.RegularExpressions;

using XperienceCommunity.MetaEmbeds.Caching;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class CacheKeyTests
{
    private const string CacheFormUrl = "https://instagram.com/p/fA9uwTtkSN/";

    [Test]
    public void ToCacheItemName_HasDocumentedShape()
    {
        var key = new EmbedCacheKey("instagram", CacheFormUrl, Authenticated: false, GraphApiVersion: "v25.0");

        var name = key.ToCacheItemName();

        Assert.That(name, Does.Match(@"^metaembeds\|oembed\|instagram\|v25\.0\|anon\|[0-9a-f]{64}$"));
        Assert.That(name, Is.EqualTo("metaembeds|oembed|instagram|v25.0|anon|" + EmbedCacheKey.Sha256Hex(CacheFormUrl)));
    }

    [Test]
    public void ToCacheItemName_AuthenticatedSegment()
    {
        var anon = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0").ToCacheItemName();
        var auth = new EmbedCacheKey("instagram", CacheFormUrl, true, "v25.0").ToCacheItemName();

        Assert.That(auth, Does.Contain("|auth|"));
        Assert.That(anon, Does.Contain("|anon|"));
        Assert.That(auth, Is.Not.EqualTo(anon));
    }

    [Test]
    public void ToCacheItemName_LowerCasesEndpointAndVersionButHashesUrlVerbatim()
    {
        var mixed = new EmbedCacheKey("Instagram", CacheFormUrl, false, "V25.0").ToCacheItemName();
        var lower = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0").ToCacheItemName();
        var otherCaseUrl = new EmbedCacheKey("instagram", "https://instagram.com/p/fa9uwttksn/", false, "v25.0").ToCacheItemName();

        Assert.That(mixed, Is.EqualTo(lower));
        Assert.That(otherCaseUrl, Is.Not.EqualTo(lower), "shortcodes are case-sensitive");
    }

    [Test]
    public void ToCacheItemName_DiffersByEveryComponent()
    {
        var baseline = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0");
        var names = new[]
        {
            baseline,
            baseline with { EndpointKey = "threads" },
            baseline with { NormalizedUrl = "https://instagram.com/p/other/" },
            baseline with { Authenticated = true },
            baseline with { GraphApiVersion = "v26.0" },
        }.Select(k => k.ToCacheItemName()).ToList();

        Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count));
        Assert.That(names, Has.All.Not.Contain(" "));
    }

    [Test]
    public void ToCacheItemName_IsDeterministic()
    {
        var a = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0");
        var b = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0");

        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.ToCacheItemName(), Is.EqualTo(b.ToCacheItemName()));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Sha256Hex_KnownVector()
    {
        Assert.That(EmbedCacheKey.Sha256Hex("abc"), Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        Assert.That(EmbedCacheKey.Sha256Hex(string.Empty), Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
    }

    [Test]
    public void Sha256Hex_IsLowerCaseHexOf64Chars()
    {
        var hex = EmbedCacheKey.Sha256Hex(CacheFormUrl);

        Assert.That(hex, Has.Length.EqualTo(64));
        Assert.That(Regex.IsMatch(hex, "^[0-9a-f]+$"), Is.True);
    }

    [Test]
    public void Sha256Hex_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => EmbedCacheKey.Sha256Hex(null!));

    [Test]
    public void ToCacheItemName_HasNoSeparatorInsideTheHashedSegment()
    {
        var name = new EmbedCacheKey("facebook-post", "https://facebook.com/zuck/posts/1/", false, "v25.0").ToCacheItemName();

        Assert.That(name.Split('|'), Has.Length.EqualTo(6));
    }

    [Test]
    public void ToCacheItemName_WithVariant_AppendsItAsASeventhSegment()
    {
        var plain = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0").ToCacheItemName();
        var variant = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0", "HideCaption").ToCacheItemName();

        Assert.Multiple(() =>
        {
            Assert.That(variant, Does.StartWith(plain + "|"));
            Assert.That(variant.Split('|'), Has.Length.EqualTo(7));
            Assert.That(variant, Does.EndWith("|hidecaption"), "variant is lower-cased");
            Assert.That(variant, Is.Not.EqualTo(plain));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ToCacheItemName_BlankVariant_KeepsThePlainName(string? variant)
    {
        var plain = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0").ToCacheItemName();
        var withBlank = new EmbedCacheKey("instagram", CacheFormUrl, false, "v25.0", variant!).ToCacheItemName();

        Assert.That(withBlank, Is.EqualTo(plain));
    }
}
