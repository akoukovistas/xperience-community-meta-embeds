using Microsoft.AspNetCore.Http;

using NSubstitute;

using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class EmbedScriptRegistryTests
{
    private static readonly Uri Instagram = new("https://www.instagram.com/embed.js");
    private static readonly Uri Threads = new("https://www.threads.com/embed.js");
    private static readonly Uri FacebookV25 = new("https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v25.0");
    private static readonly Uri FacebookV26 = new("https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v26.0");

    private static IHttpContextAccessor Accessor(HttpContext? httpContext)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }

    [Test]
    public void TryClaim_ReturnsTrueOnceThenFalse()
    {
        var registry = new EmbedScriptRegistry(Accessor(new DefaultHttpContext()));

        Assert.That(registry.TryClaim(Instagram), Is.True);
        Assert.That(registry.TryClaim(Instagram), Is.False);
        Assert.That(registry.TryClaim(new Uri("https://www.instagram.com/embed.js")), Is.False, "equal URI, different instance");
    }

    [Test]
    public void TryClaim_TreatsDifferentFragmentsAsDifferentScripts()
    {
        // Uri.Equals ignores the fragment, but the Facebook SDK is configured through it.
        var registry = new EmbedScriptRegistry(Accessor(new DefaultHttpContext()));

        Assert.That(registry.TryClaim(FacebookV25), Is.True);
        Assert.That(registry.TryClaim(FacebookV26), Is.True);
        Assert.That(registry.TryClaim(FacebookV25), Is.False);
    }

    [Test]
    public void TryClaimMarker_ReturnsTrueOnceThenFalse_AndIsIndependentOfScripts()
    {
        var registry = new EmbedScriptRegistry(Accessor(new DefaultHttpContext()));

        Assert.That(registry.TryClaimMarker("fb-root"), Is.True);
        Assert.That(registry.TryClaimMarker("fb-root"), Is.False);
        Assert.That(registry.TryClaimMarker("other"), Is.True);
        Assert.That(registry.Claimed, Is.Empty, "markers are not scripts");
    }

    [Test]
    public void Claimed_ReturnsScriptsInClaimOrder()
    {
        var registry = new EmbedScriptRegistry(Accessor(new DefaultHttpContext()));

        registry.TryClaim(Threads);
        registry.TryClaim(Instagram);
        registry.TryClaim(Threads);
        registry.TryClaim(FacebookV25);

        Assert.That(registry.Claimed, Is.EqualTo(new[] { Threads, Instagram, FacebookV25 }).AsCollection);
    }

    [Test]
    public void TwoInstances_SharingOneHttpContext_ShareClaims()
    {
        var httpContext = new DefaultHttpContext();
        var first = new EmbedScriptRegistry(Accessor(httpContext));
        var second = new EmbedScriptRegistry(Accessor(httpContext));

        Assert.That(first.TryClaim(Instagram), Is.True);
        Assert.That(first.TryClaimMarker("fb-root"), Is.True);

        Assert.That(second.TryClaim(Instagram), Is.False);
        Assert.That(second.TryClaimMarker("fb-root"), Is.False);
        Assert.That(second.TryClaim(Threads), Is.True);
        Assert.That(first.Claimed, Is.EqualTo(new[] { Instagram, Threads }).AsCollection);
        Assert.That(second.Claimed, Is.EqualTo(first.Claimed).AsCollection);
    }

    [Test]
    public void TwoInstances_OnDifferentHttpContexts_AreIndependent()
    {
        var first = new EmbedScriptRegistry(Accessor(new DefaultHttpContext()));
        var second = new EmbedScriptRegistry(Accessor(new DefaultHttpContext()));

        Assert.That(first.TryClaim(Instagram), Is.True);
        Assert.That(second.TryClaim(Instagram), Is.True);
    }

    [Test]
    public void WithoutHttpContext_FallsBackToInstanceLocalState()
    {
        var first = new EmbedScriptRegistry(Accessor(null));
        var second = new EmbedScriptRegistry(Accessor(null));

        Assert.That(first.TryClaim(Instagram), Is.True);
        Assert.That(first.TryClaim(Instagram), Is.False);
        Assert.That(second.TryClaim(Instagram), Is.True, "no shared context, so the second instance has its own state");
        Assert.That(first.Claimed, Is.EqualTo(new[] { Instagram }).AsCollection);
    }

    [Test]
    public void TryClaim_RejectsNullAndRelativeUris()
    {
        var registry = new EmbedScriptRegistry(Accessor(new DefaultHttpContext()));

        Assert.That(() => registry.TryClaim(null!), Throws.ArgumentNullException);
        Assert.That(() => registry.TryClaim(new Uri("/embed.js", UriKind.Relative)), Throws.ArgumentException);
        Assert.That(() => registry.TryClaimMarker(" "), Throws.ArgumentException);
    }

    [Test]
    public void Constructor_RequiresAccessor()
    {
        Assert.That(() => new EmbedScriptRegistry(null!), Throws.ArgumentNullException);
    }
}
