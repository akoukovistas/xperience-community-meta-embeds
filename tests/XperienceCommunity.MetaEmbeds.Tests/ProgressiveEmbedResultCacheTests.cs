using NUnit.Framework;

using CMS.Core;
using CMS.Helpers;
using CMS.Tests;

using NSubstitute;

using XperienceCommunity.MetaEmbeds.Caching;
using XperienceCommunity.MetaEmbeds.Providers;

namespace XperienceCommunity.MetaEmbeds.Tests;

/// <summary>Contract with <see cref="IProgressiveCache"/>: what the delegate does to <see cref="CacheSettings"/>.</summary>
[TestFixture]
public class ProgressiveEmbedResultCacheTests : UnitTests
{
    private static readonly EmbedCacheKey Key = new("instagram", "https://instagram.com/p/fA9uwTtkSN/", false, "v25.0");

    private static readonly EmbedCachePolicy Policy = new(
        TimeSpan.FromHours(12), TimeSpan.FromHours(1), TimeSpan.FromMinutes(2),
        ["metaembeds|all", "metaembeds|endpoint|instagram"]);

    [Test]
    public async Task Success_SetsSuccessMinutesAndDependencyKeys()
    {
        var (cache, seen) = Build();

        var result = await cache.GetOrAddAsync(Key, _ => Task.FromResult(Success()), Policy, CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        var settings = seen.Single();
        Assert.Multiple(() =>
        {
            Assert.That(settings.CacheMinutes, Is.EqualTo(720d));
            Assert.That(settings.Cached, Is.True);
            Assert.That(settings.AllowProgressiveCaching, Is.True);
            Assert.That(settings.CacheDependency, Is.Not.Null);
            Assert.That(settings.CacheDependency.CacheKeys, Is.EquivalentTo(new[] { "metaembeds|all", "metaembeds|endpoint|instagram" }));
            Assert.That(settings.CacheItemName, Is.EqualTo(Key.ToCacheItemName()));
        });
    }

    [TestCase(EmbedFailureKind.NotFound, 60d)]
    [TestCase(EmbedFailureKind.RejectedByProvider, 60d)]
    [TestCase(EmbedFailureKind.UnsupportedInput, 60d)]
    [TestCase(EmbedFailureKind.Transient, 2d)]
    [TestCase(EmbedFailureKind.UnexpectedMarkup, 2d)]
    [TestCase(EmbedFailureKind.Internal, 2d)]
    public async Task Failure_SetsPolicyMinutesInsideTheDelegate(EmbedFailureKind kind, double expectedMinutes)
    {
        var (cache, seen) = Build();

        var result = await cache.GetOrAddAsync(Key, _ => Task.FromResult(EmbedResult.Failed(kind)), Policy, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(kind));
        var settings = seen.Single();
        Assert.That(settings.CacheMinutes, Is.EqualTo(expectedMinutes));
        Assert.That(settings.Cached, Is.True);
        Assert.That(settings.CacheDependency?.CacheKeys, Does.Contain("metaembeds|all"));
    }

    [TestCase(EmbedFailureKind.NotConfigured)]
    [TestCase(EmbedFailureKind.InvalidInput)]
    public async Task ZeroDuration_DisablesCachingForThatResult(EmbedFailureKind kind)
    {
        var (cache, seen) = Build();

        await cache.GetOrAddAsync(Key, _ => Task.FromResult(EmbedResult.Failed(kind)), Policy, CancellationToken.None);

        Assert.That(seen.Single().Cached, Is.False);
    }

    [Test]
    public async Task Factory_ReceivesANonCancellableToken()
    {
        var (cache, _) = Build();
        using var cts = new CancellationTokenSource();
        CancellationToken? seenToken = null;

        await cache.GetOrAddAsync(Key, ct =>
        {
            seenToken = ct;
            return Task.FromResult(Success());
        }, Policy, cts.Token);

        Assert.That(seenToken, Is.Not.Null);
        Assert.That(seenToken!.Value.CanBeCanceled, Is.False);
    }

    [Test]
    public void PreCancelledCaller_ThrowsBeforeLoading()
    {
        var (cache, seen) = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => cache.GetOrAddAsync(Key, _ => Task.FromResult(Success()), Policy, cts.Token));
        Assert.That(seen, Is.Empty);
    }

    [Test]
    public async Task NullFactoryResult_BecomesInternalFailure()
    {
        var (cache, seen) = Build();

        var result = await cache.GetOrAddAsync(Key, _ => Task.FromResult<EmbedResult>(null!), Policy, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Internal));
        Assert.That(seen.Single().CacheMinutes, Is.EqualTo(2d));
    }

    [Test]
    public void FactoryException_Propagates()
    {
        var (cache, _) = Build();

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrAddAsync(Key, _ => throw new InvalidOperationException("boom"), Policy, CancellationToken.None));
    }

    [Test]
    public void NullArguments_Throw()
    {
        var (cache, _) = Build();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressiveEmbedResultCache(null!));
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetOrAddAsync(null!, _ => Task.FromResult(Success()), Policy, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetOrAddAsync(Key, null!, Policy, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetOrAddAsync(Key, _ => Task.FromResult(Success()), null!, CancellationToken.None));
        });
    }

    [Test]
    public async Task AllDurationsZero_StillCallsFactoryAndDoesNotCache()
    {
        var (cache, seen) = Build();
        var policy = new EmbedCachePolicy(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, ["metaembeds|all"]);

        var result = await cache.GetOrAddAsync(Key, _ => Task.FromResult(Success()), policy, CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(seen.Single().Cached, Is.False);
    }

    private static (ProgressiveEmbedResultCache Cache, List<CacheSettings> Seen) Build()
    {
        var seen = new List<CacheSettings>();
        var progressive = Substitute.For<IProgressiveCache>();
        progressive
            .LoadAsync(Arg.Any<Func<CacheSettings, Task<EmbedResult>>>(), Arg.Any<CacheSettings>())
            .Returns(call =>
            {
                var settings = call.Arg<CacheSettings>();
                seen.Add(settings);
                return call.Arg<Func<CacheSettings, Task<EmbedResult>>>()(settings);
            });
        return (new ProgressiveEmbedResultCache(progressive), seen);
    }

    internal static EmbedResult Success(string shortcode = "fA9uwTtkSN") => EmbedResult.Success(new EmbedItem
    {
        EndpointKey = "instagram",
        ProviderName = "Instagram",
        Html = "<blockquote class=\"instagram-media\"></blockquote>",
        Type = "rich",
        SourceUrl = new Uri($"https://www.instagram.com/p/{shortcode}/"),
        RequiredScripts = [new Uri("https://www.instagram.com/embed.js")],
    });
}

/// <summary>
/// Behaviour against the real <see cref="IProgressiveCache"/> from Xperience's test container. Proves the undocumented
/// part of the design: <see cref="CacheSettings.CacheMinutes"/> set inside the load delegate is what the cache honours,
/// so failures can live shorter than successes under one cache item name.
/// </summary>
[TestFixture]
public class ProgressiveEmbedResultCacheXperienceTests : UnitTests
{
    private ProgressiveEmbedResultCache cache = null!;

    [SetUp]
    public void SetUp() => cache = new ProgressiveEmbedResultCache(Service.Resolve<IProgressiveCache>());

    [Test]
    public async Task Success_IsServedFromCacheOnSecondCall()
    {
        var key = UniqueKey();
        var calls = 0;

        var first = await cache.GetOrAddAsync(key, _ => { calls++; return Task.FromResult(ProgressiveEmbedResultCacheTests.Success()); }, DefaultPolicy(), CancellationToken.None);
        var second = await cache.GetOrAddAsync(key, _ => { calls++; return Task.FromResult(ProgressiveEmbedResultCacheTests.Success()); }, DefaultPolicy(), CancellationToken.None);

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public async Task ZeroDurationFailure_IsNotCached()
    {
        var key = UniqueKey();
        var calls = 0;

        await cache.GetOrAddAsync(key, _ => { calls++; return Task.FromResult(EmbedResult.Failed(EmbedFailureKind.NotConfigured)); }, DefaultPolicy(), CancellationToken.None);
        await cache.GetOrAddAsync(key, _ => { calls++; return Task.FromResult(EmbedResult.Failed(EmbedFailureKind.NotConfigured)); }, DefaultPolicy(), CancellationToken.None);

        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task ShortFailureTtlSetInsideDelegate_IsHonoured()
    {
        // Success would live 12 h; the transient failure must expire after ~300 ms instead.
        var key = UniqueKey();
        var policy = new EmbedCachePolicy(TimeSpan.FromHours(12), TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(300), DefaultPolicy().DependencyKeys);
        var calls = 0;
        Task<EmbedResult> Factory(CancellationToken _) { calls++; return Task.FromResult(EmbedResult.Failed(EmbedFailureKind.Transient, "blip")); }

        var first = await cache.GetOrAddAsync(key, Factory, policy, CancellationToken.None);
        var second = await cache.GetOrAddAsync(key, Factory, policy, CancellationToken.None);
        Assert.That(first.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(second, Is.SameAs(first), "the failure is negatively cached within its TTL");
        Assert.That(calls, Is.EqualTo(1));

        await Task.Delay(TimeSpan.FromMilliseconds(900));

        await cache.GetOrAddAsync(key, Factory, policy, CancellationToken.None);
        Assert.That(calls, Is.EqualTo(2), "after the short TTL the factory runs again");
    }

    [Test]
    public async Task SuccessAfterFailure_ReplacesTheEntryWithTheLongTtl()
    {
        var key = UniqueKey();
        var policy = new EmbedCachePolicy(TimeSpan.FromHours(12), TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(200), DefaultPolicy().DependencyKeys);

        await cache.GetOrAddAsync(key, _ => Task.FromResult(EmbedResult.Failed(EmbedFailureKind.Transient)), policy, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(600));
        var success = await cache.GetOrAddAsync(key, _ => Task.FromResult(ProgressiveEmbedResultCacheTests.Success()), policy, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(600));
        var again = await cache.GetOrAddAsync(key, _ => Task.FromResult(EmbedResult.Failed(EmbedFailureKind.Transient)), policy, CancellationToken.None);

        Assert.That(success.Succeeded, Is.True);
        Assert.That(again, Is.SameAs(success), "the success stays cached well beyond the transient TTL");
    }

    [Test]
    public async Task TouchingTheGlobalDummyKey_EvictsEverything()
    {
        var instagram = UniqueKey();
        var threads = UniqueKey() with { EndpointKey = "threads" };
        var calls = 0;
        Task<EmbedResult> Factory(CancellationToken _) { calls++; return Task.FromResult(ProgressiveEmbedResultCacheTests.Success()); }

        await cache.GetOrAddAsync(instagram, Factory, EmbedCachePolicy.For(new MetaEmbedsOptions(), "instagram"), CancellationToken.None);
        await cache.GetOrAddAsync(threads, Factory, EmbedCachePolicy.For(new MetaEmbedsOptions(), "threads"), CancellationToken.None);
        Assert.That(calls, Is.EqualTo(2));

        CacheHelper.TouchKey(MetaEmbedsConstants.CacheKeyAll);

        await cache.GetOrAddAsync(instagram, Factory, EmbedCachePolicy.For(new MetaEmbedsOptions(), "instagram"), CancellationToken.None);
        await cache.GetOrAddAsync(threads, Factory, EmbedCachePolicy.For(new MetaEmbedsOptions(), "threads"), CancellationToken.None);
        Assert.That(calls, Is.EqualTo(4));
    }

    [Test]
    public async Task TouchingAnEndpointDummyKey_EvictsOnlyThatEndpoint()
    {
        var instagram = UniqueKey();
        var threads = UniqueKey() with { EndpointKey = "threads" };
        var instagramCalls = 0;
        var threadsCalls = 0;

        await cache.GetOrAddAsync(instagram, _ => { instagramCalls++; return Task.FromResult(ProgressiveEmbedResultCacheTests.Success()); }, EmbedCachePolicy.For(new MetaEmbedsOptions(), "instagram"), CancellationToken.None);
        await cache.GetOrAddAsync(threads, _ => { threadsCalls++; return Task.FromResult(ProgressiveEmbedResultCacheTests.Success()); }, EmbedCachePolicy.For(new MetaEmbedsOptions(), "threads"), CancellationToken.None);

        CacheHelper.TouchKey(MetaEmbedsConstants.CacheKeyForEndpoint("instagram"));

        await cache.GetOrAddAsync(instagram, _ => { instagramCalls++; return Task.FromResult(ProgressiveEmbedResultCacheTests.Success()); }, EmbedCachePolicy.For(new MetaEmbedsOptions(), "instagram"), CancellationToken.None);
        await cache.GetOrAddAsync(threads, _ => { threadsCalls++; return Task.FromResult(ProgressiveEmbedResultCacheTests.Success()); }, EmbedCachePolicy.For(new MetaEmbedsOptions(), "threads"), CancellationToken.None);

        Assert.That(instagramCalls, Is.EqualTo(2));
        Assert.That(threadsCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentFirstCalls_RunTheFactoryOnce()
    {
        var key = UniqueKey();
        var calls = 0;
        async Task<EmbedResult> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(150);
            return ProgressiveEmbedResultCacheTests.Success();
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => cache.GetOrAddAsync(key, Factory, DefaultPolicy(), CancellationToken.None)));

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(results.Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task DifferentKeys_AreCachedIndependently()
    {
        var a = UniqueKey();
        var b = UniqueKey();
        var calls = 0;
        Task<EmbedResult> Factory(CancellationToken _) { calls++; return Task.FromResult(ProgressiveEmbedResultCacheTests.Success()); }

        await cache.GetOrAddAsync(a, Factory, DefaultPolicy(), CancellationToken.None);
        await cache.GetOrAddAsync(b, Factory, DefaultPolicy(), CancellationToken.None);
        await cache.GetOrAddAsync(a, Factory, DefaultPolicy(), CancellationToken.None);
        await cache.GetOrAddAsync(b, Factory, DefaultPolicy(), CancellationToken.None);

        Assert.That(calls, Is.EqualTo(2));
    }

    private static EmbedCachePolicy DefaultPolicy() => EmbedCachePolicy.For(new MetaEmbedsOptions(), "instagram");

    private static EmbedCacheKey UniqueKey() =>
        new("instagram", $"https://instagram.com/p/{Guid.NewGuid():N}/", false, "v25.0");
}
