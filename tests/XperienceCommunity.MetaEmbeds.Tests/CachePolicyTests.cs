using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Caching;
using XperienceCommunity.MetaEmbeds.Providers;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class CachePolicyTests
{
    [Test]
    public void For_CopiesDurationsAndBuildsDependencyKeys()
    {
        var options = new MetaEmbedsOptions
        {
            SuccessCacheDuration = TimeSpan.FromHours(6),
            NotFoundCacheDuration = TimeSpan.FromMinutes(30),
            TransientFailureCacheDuration = TimeSpan.FromSeconds(45),
        };

        var policy = EmbedCachePolicy.For(options, "instagram");

        Assert.Multiple(() =>
        {
            Assert.That(policy.Success, Is.EqualTo(TimeSpan.FromHours(6)));
            Assert.That(policy.NotFound, Is.EqualTo(TimeSpan.FromMinutes(30)));
            Assert.That(policy.Transient, Is.EqualTo(TimeSpan.FromSeconds(45)));
            Assert.That(policy.DependencyKeys, Is.EqualTo(new[] { "metaembeds|all", "metaembeds|endpoint|instagram" }));
        });
    }

    [Test]
    public void For_Defaults()
    {
        var policy = EmbedCachePolicy.For(new MetaEmbedsOptions(), "threads");

        Assert.Multiple(() =>
        {
            Assert.That(policy.Success, Is.EqualTo(TimeSpan.FromHours(12)));
            Assert.That(policy.NotFound, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(policy.Transient, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(policy.DependencyKeys, Is.EqualTo(new[] { MetaEmbedsConstants.CacheKeyAll, MetaEmbedsConstants.CacheKeyForEndpoint("threads") }));
        });
    }

    [Test]
    public void For_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => EmbedCachePolicy.For(null!, "instagram"));
        Assert.Throws<ArgumentNullException>(() => EmbedCachePolicy.For(new MetaEmbedsOptions(), null!));
    }

    [Test]
    public void DurationFor_Success_IsSuccessDuration()
    {
        var policy = new EmbedCachePolicy(TimeSpan.FromHours(3), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(1), ["metaembeds|all"]);

        Assert.That(policy.DurationFor(Success()), Is.EqualTo(TimeSpan.FromHours(3)));
    }

    [TestCase(EmbedFailureKind.NotFound, "NotFound")]
    [TestCase(EmbedFailureKind.RejectedByProvider, "NotFound")]
    [TestCase(EmbedFailureKind.UnsupportedInput, "NotFound")]
    [TestCase(EmbedFailureKind.NotConfigured, "Zero")]
    [TestCase(EmbedFailureKind.InvalidInput, "Zero")]
    [TestCase(EmbedFailureKind.Transient, "Transient")]
    [TestCase(EmbedFailureKind.UnexpectedMarkup, "Transient")]
    [TestCase(EmbedFailureKind.Internal, "Transient")]
    public void DurationFor_FailureKinds(EmbedFailureKind kind, string expectedBucket)
    {
        var policy = new EmbedCachePolicy(TimeSpan.FromHours(3), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(1), ["metaembeds|all"]);
        var expected = expectedBucket switch
        {
            "NotFound" => TimeSpan.FromMinutes(20),
            "Transient" => TimeSpan.FromMinutes(1),
            _ => TimeSpan.Zero,
        };

        Assert.That(policy.DurationFor(EmbedResult.Failed(kind)), Is.EqualTo(expected));
    }

    [Test]
    public void DurationFor_EveryKindIsCovered()
    {
        var policy = EmbedCachePolicy.For(new MetaEmbedsOptions(), "instagram");

        foreach (var kind in Enum.GetValues<EmbedFailureKind>())
        {
            Assert.DoesNotThrow(() => policy.DurationFor(EmbedResult.Failed(kind)), kind.ToString());
        }
    }

    [Test]
    public void DurationFor_Null_Throws()
    {
        var policy = EmbedCachePolicy.For(new MetaEmbedsOptions(), "instagram");

        Assert.Throws<ArgumentNullException>(() => policy.DurationFor(null!));
    }

    private static EmbedResult Success() => EmbedResult.Success(new EmbedItem
    {
        EndpointKey = "instagram",
        ProviderName = "Instagram",
        Html = "<blockquote class=\"instagram-media\"></blockquote>",
        Type = "rich",
        SourceUrl = new Uri("https://www.instagram.com/p/fA9uwTtkSN/"),
        RequiredScripts = [new Uri("https://www.instagram.com/embed.js")],
    });
}
