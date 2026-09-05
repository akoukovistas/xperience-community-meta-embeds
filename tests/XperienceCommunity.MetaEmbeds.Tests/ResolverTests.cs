using NUnit.Framework;

using Microsoft.Extensions.Logging;

using XperienceCommunity.MetaEmbeds.Providers;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class ResolverTests
{
    private const string Url = "https://www.instagram.com/p/fA9uwTtkSN/";

    [Test]
    public async Task Resolve_NullRequest_NotConfigured()
    {
        var (resolver, _, _) = Build(new FakeProvider());

        var result = await resolver.ResolveAsync(null!, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.NotConfigured));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Resolve_BlankInput_NotConfigured_ProviderNotCalled(string? input)
    {
        var provider = new FakeProvider();
        var (resolver, _, _) = Build(provider);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = input! }, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.NotConfigured));
        Assert.That(provider.Received, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("post")]
    [TestCase("POST")]
    [TestCase("  Post  ")]
    [TestCase("feed")]
    [TestCase("FEED")]
    [TestCase("story")]
    public async Task Resolve_SourceType_IsNormalisedToPostForThePostProvider(string? sourceType)
    {
        var provider = new FakeProvider();
        var (resolver, _, _) = Build(provider);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = sourceType!, Input = Url, Culture = "en" }, CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(provider.Received, Has.Count.EqualTo(1));
        Assert.That(provider.Received[0].SourceType, Is.EqualTo("post"));
        Assert.That(provider.Received[0].Input, Is.EqualTo(Url));
        Assert.That(provider.Received[0].Culture, Is.EqualTo("en"));
    }

    [Test]
    public async Task Resolve_UnknownSourceType_LogsWarningOncePerHourPerValue()
    {
        var provider = new FakeProvider();
        var (resolver, logger, clock) = Build(provider);

        await resolver.ResolveAsync(new EmbedRequest { SourceType = "feed", Input = Url }, CancellationToken.None);
        await resolver.ResolveAsync(new EmbedRequest { SourceType = "FEED", Input = Url }, CancellationToken.None);
        await resolver.ResolveAsync(new EmbedRequest { SourceType = "story", Input = Url }, CancellationToken.None);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.That(warnings, Has.Count.EqualTo(2));
        Assert.That(warnings.Select(w => w.Id.Name), Has.All.EqualTo("METAEMBEDS_UNKNOWN_SOURCE_TYPE"));
        Assert.That(warnings[0].Message, Does.Contain("'feed'"));
        Assert.That(warnings[1].Message, Does.Contain("'story'"));

        clock.Advance(TimeSpan.FromMinutes(59));
        await resolver.ResolveAsync(new EmbedRequest { SourceType = "feed", Input = Url }, CancellationToken.None);
        Assert.That(logger.Entries.Count(e => e.Level == LogLevel.Warning), Is.EqualTo(2));

        clock.Advance(TimeSpan.FromMinutes(2));
        await resolver.ResolveAsync(new EmbedRequest { SourceType = "feed", Input = Url }, CancellationToken.None);
        Assert.That(logger.Entries.Count(e => e.Level == LogLevel.Warning), Is.EqualTo(3));

        Assert.That(provider.Received, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task Resolve_KnownSourceType_LogsNoWarning()
    {
        var (resolver, logger, _) = Build(new FakeProvider());

        await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);
        await resolver.ResolveAsync(new EmbedRequest { SourceType = null!, Input = Url }, CancellationToken.None);

        Assert.That(logger.Entries.Where(e => e.Level >= LogLevel.Information), Is.Empty);
        Assert.That(logger.Entries.Count(e => e.Id.Name == "METAEMBEDS_SOURCE_TYPE_DEFAULTED"), Is.EqualTo(1));
    }

    [Test]
    public async Task Resolve_ProviderSupportingACustomSourceType_ReceivesItUnchanged()
    {
        var postProvider = new FakeProvider { Name = "post" };
        var feedProvider = new FakeProvider { Name = "feed", SupportsFunc = r => r.SourceType == "feed" };
        var (resolver, logger, _) = Build(postProvider, feedProvider);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = " Feed ", Input = "@handle" }, CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(feedProvider.Received.Single().SourceType, Is.EqualTo("feed"));
        Assert.That(postProvider.Received, Is.Empty);
        Assert.That(logger.Entries.Where(e => e.Level >= LogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task Resolve_NoProviders_UnsupportedInput()
    {
        var (resolver, _, _) = Build();

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.UnsupportedInput));
    }

    [Test]
    public async Task Resolve_NoProviderSupportsRequest_UnsupportedInput()
    {
        var provider = new FakeProvider { SupportsFunc = _ => false };
        var (resolver, _, _) = Build(provider);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.UnsupportedInput));
        Assert.That(provider.Received, Is.Empty);
    }

    [Test]
    public async Task Resolve_FirstSupportingProviderWins()
    {
        var first = new FakeProvider { Name = "first", SupportsFunc = _ => false };
        var second = new FakeProvider { Name = "second" };
        var third = new FakeProvider { Name = "third" };
        var (resolver, _, _) = Build(first, second, third);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(second.Received, Has.Count.EqualTo(1));
        Assert.That(third.Received, Is.Empty);
    }

    [Test]
    public async Task Resolve_ProviderThrows_Internal_LoggedOncePerTenMinutes()
    {
        var provider = new FakeProvider { ResolveFunc = (_, _) => throw new InvalidOperationException("kaboom") };
        var (resolver, logger, clock) = Build(provider);

        var first = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);
        var second = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Internal));
            Assert.That(first.Failure!.Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(first.Failure.ProviderMessage, Is.EqualTo("kaboom"));
            Assert.That(second.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Internal));
        });
        var errors = logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0].Id.Name, Is.EqualTo("METAEMBEDS_PROVIDER_FAILED"));
        Assert.That(errors[0].Exception, Is.InstanceOf<InvalidOperationException>());

        clock.Advance(TimeSpan.FromMinutes(11));
        await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);
        Assert.That(logger.Entries.Count(e => e.Level == LogLevel.Error), Is.EqualTo(2));
    }

    [Test]
    public async Task Resolve_DifferentFailures_LoggedSeparately()
    {
        var calls = 0;
        var provider = new FakeProvider { ResolveFunc = (_, _) => throw new InvalidOperationException($"failure {++calls}") };
        var (resolver, logger, _) = Build(provider);

        await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);
        await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.That(logger.Entries.Count(e => e.Level == LogLevel.Error), Is.EqualTo(2));
    }

    [Test]
    public async Task Resolve_SupportsThrows_Internal()
    {
        var provider = new FakeProvider { SupportsFunc = _ => throw new NotSupportedException("no") };
        var (resolver, _, _) = Build(provider);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Internal));
        Assert.That(result.Failure!.Exception, Is.InstanceOf<NotSupportedException>());
    }

    [Test]
    public async Task Resolve_ProviderReturnsNull_Internal()
    {
        var provider = new FakeProvider { ResolveFunc = (_, _) => Task.FromResult<EmbedResult>(null!) };
        var (resolver, logger, _) = Build(provider);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Internal));
        Assert.That(logger.Entries.Count(e => e.Level == LogLevel.Error), Is.EqualTo(1));
    }

    [Test]
    public async Task Resolve_CallerCancelled_TransientWithoutLogging()
    {
        var provider = new FakeProvider { ResolveFunc = (_, ct) => Task.FromCanceled<EmbedResult>(ct) };
        var (resolver, logger, _) = Build(provider);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, cts.Token);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(logger.Entries.Where(e => e.Level >= LogLevel.Information), Is.Empty);
    }

    [Test]
    public async Task Resolve_CancellationWithoutCallerCancel_TransientWithException()
    {
        var provider = new FakeProvider { ResolveFunc = (_, _) => throw new TaskCanceledException("timeout") };
        var (resolver, logger, _) = Build(provider);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.That(result.Failure?.Kind, Is.EqualTo(EmbedFailureKind.Transient));
        Assert.That(result.Failure!.Exception, Is.InstanceOf<TaskCanceledException>());
        Assert.That(logger.Entries.Where(e => e.Level >= LogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task Resolve_FailedProviderResult_PassesThroughUnchanged()
    {
        var failure = EmbedResult.Failed(EmbedFailureKind.NotFound, "gone");
        var provider = new FakeProvider { ResolveFunc = (_, _) => Task.FromResult(failure) };
        var (resolver, logger, _) = Build(provider);

        var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = "post", Input = Url }, CancellationToken.None);

        Assert.That(result, Is.SameAs(failure));
        Assert.That(logger.Entries.Where(e => e.Level >= LogLevel.Information), Is.Empty);
    }

    [Test]
    public void Resolve_NeverThrows_OverGarbage()
    {
        var exceptions = new Exception[]
        {
            new InvalidOperationException("a"),
            new NullReferenceException("b"),
            new ArgumentException("c"),
            new HttpRequestException("d"),
            new AggregateException(new TimeoutException("e")),
            new OutOfMemoryException("f"),
        };
        var i = 0;
        var provider = new FakeProvider
        {
            SupportsFunc = r => r.Input.Length % 3 != 0 ? true : throw exceptions[i++ % exceptions.Length],
            ResolveFunc = (_, _) => throw exceptions[i++ % exceptions.Length],
        };
        var (resolver, _, _) = Build(provider);

        var inputs = new[]
        {
            Url, "x", "  ", new string(' ', 10), new string('a', 100_000), "https://", "post", "￿﻿", "javascript:alert(1)",
        };
        var sourceTypes = new[] { null, "", "post", "feed", new string('z', 5_000), "\t", "POST " };

        Assert.DoesNotThrowAsync(async () =>
        {
            foreach (var input in inputs)
            {
                foreach (var sourceType in sourceTypes)
                {
                    var result = await resolver.ResolveAsync(new EmbedRequest { SourceType = sourceType!, Input = input }, CancellationToken.None);
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result.Succeeded || result.Failure is not null, Is.True);
                }
            }
        });
    }

    [Test]
    public void Ctor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new EmbedResolver(null!, new CoreTestLogger<EmbedResolver>()));
        Assert.Throws<ArgumentNullException>(() => new EmbedResolver([], null!));
    }

    private static (EmbedResolver Resolver, CoreTestLogger<EmbedResolver> Logger, ManualClock Clock) Build(params IEmbedProvider[] providers)
    {
        var logger = new CoreTestLogger<EmbedResolver>();
        var clock = new ManualClock();
        return (new EmbedResolver(providers, logger, clock), logger, clock);
    }

    private static EmbedItem Item(EmbedRequest request) => new()
    {
        EndpointKey = "instagram",
        ProviderName = "Instagram",
        Html = "<blockquote class=\"instagram-media\"></blockquote>",
        Type = "rich",
        SourceUrl = Uri.TryCreate(request.Input, UriKind.Absolute, out var uri) ? uri : new Uri(Url),
        RequiredScripts = [new Uri("https://www.instagram.com/embed.js")],
    };

    private sealed class FakeProvider : IEmbedProvider
    {
        public string Name { get; init; } = "fake";

        public Func<EmbedRequest, bool> SupportsFunc { get; init; } = r => r.SourceType == "post";

        public Func<EmbedRequest, CancellationToken, Task<EmbedResult>> ResolveFunc { get; init; } =
            (r, _) => Task.FromResult(EmbedResult.Success(Item(r)));

        public List<EmbedRequest> Received { get; } = [];

        public bool Supports(EmbedRequest request) => SupportsFunc(request);

        public Task<EmbedResult> ResolveAsync(EmbedRequest request, CancellationToken cancellationToken)
        {
            Received.Add(request);
            return ResolveFunc(request, cancellationToken);
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
