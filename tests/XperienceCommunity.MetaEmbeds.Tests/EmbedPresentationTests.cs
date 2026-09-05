using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Rendering;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class EmbedPresentationTests
{
    private const string ThreadsHtml =
        "<blockquote class=\"text-post-media\" data-text-post-permalink=\"https://www.threads.com/t/DWjTI0cgH5O\" id=\"ig-tp-DWjTI0cgH5O\" style=\"background:#FFF\" data-theme=\"light\"><a href=\"https://www.threads.com/t/DWjTI0cgH5O\">View on Threads</a></blockquote>";

    private const string FacebookPostHtml =
        "<div class=\"fb-post\" data-href=\"https://www.facebook.com/zuck/posts/10102577175875681\" data-width=\"552\"></div>";

    // ---- normalisation ------------------------------------------------------------------------------------------

    [TestCase(null, EmbedLayouts.Natural)]
    [TestCase("", EmbedLayouts.Natural)]
    [TestCase("natural", EmbedLayouts.Natural)]
    [TestCase("Centered", EmbedLayouts.Centered)]
    [TestCase("  FLUID ", EmbedLayouts.Fluid)]
    [TestCase("wide", EmbedLayouts.Natural)]
    public void Layouts_Normalize(string? input, string expected) =>
        Assert.That(EmbedLayouts.Normalize(input), Is.EqualTo(expected));

    [TestCase(null, EmbedThemes.Light)]
    [TestCase("", EmbedThemes.Light)]
    [TestCase("light", EmbedThemes.Light)]
    [TestCase("DARK", EmbedThemes.Dark)]
    [TestCase(" dark ", EmbedThemes.Dark)]
    [TestCase("sepia", EmbedThemes.Light)]
    public void Themes_Normalize(string? input, string expected) =>
        Assert.That(EmbedThemes.Normalize(input), Is.EqualTo(expected));

    [Test]
    public void From_NormalizesEveryMember()
    {
        var presentation = EmbedPresentation.From(" Fluid", "Dark", true);

        Assert.That(presentation, Is.EqualTo(new EmbedPresentation(EmbedLayouts.Fluid, EmbedThemes.Dark, true)));
    }

    // ---- CSS hooks ------------------------------------------------------------------------------------------------

    [Test]
    public void CssClasses_Default_HasWrapperEndpointAndLayoutClasses() =>
        Assert.That(EmbedPresentation.Default.CssClasses("instagram"), Is.EqualTo("meta-embed meta-embed--instagram meta-embed--layout-natural"));

    [Test]
    public void CssClasses_AllOptions_AddThemeAndCaptionModifiers()
    {
        var classes = new EmbedPresentation(EmbedLayouts.Centered, EmbedThemes.Dark, true).CssClasses("Threads");

        Assert.That(classes, Is.EqualTo("meta-embed meta-embed--threads meta-embed--layout-centered meta-embed--theme-dark meta-embed--no-caption"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void CssClasses_WithoutEndpoint_SkipsTheEndpointClass(string? endpointKey) =>
        Assert.That(EmbedPresentation.Default.CssClasses(endpointKey), Is.EqualTo("meta-embed meta-embed--layout-natural"));

    [Test]
    public void WrapperStyle_OnlyCenteredNeedsInlineStyle()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EmbedPresentation.Default.WrapperStyle, Is.Null);
            Assert.That(new EmbedPresentation(EmbedLayouts.Fluid, EmbedThemes.Light, false).WrapperStyle, Is.Null);
            Assert.That(new EmbedPresentation(EmbedLayouts.Centered, EmbedThemes.Light, false).WrapperStyle, Is.EqualTo("display:flex;justify-content:center;"));
        });
    }

    // ---- markup decorator -----------------------------------------------------------------------------------------

    [Test]
    public void Apply_DarkThemeOnThreads_RewritesDataTheme()
    {
        var result = EmbedMarkupDecorator.Apply(ThreadsHtml, "threads", new EmbedPresentation(EmbedLayouts.Natural, EmbedThemes.Dark, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain(" data-theme=\"dark\""));
            Assert.That(result, Does.Not.Contain("data-theme=\"light\""));
            Assert.That(result.Replace("data-theme=\"dark\"", "data-theme=\"light\""), Is.EqualTo(ThreadsHtml), "nothing else changes");
        });
    }

    [Test]
    public void Apply_DarkThemeOnThreads_IsIdempotent()
    {
        var presentation = new EmbedPresentation(EmbedLayouts.Natural, EmbedThemes.Dark, false);
        var once = EmbedMarkupDecorator.Apply(ThreadsHtml, "threads", presentation);

        Assert.That(EmbedMarkupDecorator.Apply(once, "threads", presentation), Is.EqualTo(once));
    }

    [TestCase("instagram")]
    [TestCase("facebook-post")]
    [TestCase("facebook-video")]
    public void Apply_DarkThemeOnOtherPlatforms_LeavesMarkupAlone(string endpointKey)
    {
        var html = "<div data-theme=\"light\" class=\"x\"></div>";

        Assert.That(EmbedMarkupDecorator.Apply(html, endpointKey, new EmbedPresentation(EmbedLayouts.Natural, EmbedThemes.Dark, false)), Is.EqualTo(html));
    }

    [Test]
    public void Apply_FluidOnFacebookPost_SetsDataWidthAuto()
    {
        var result = EmbedMarkupDecorator.Apply(FacebookPostHtml, "facebook-post", new EmbedPresentation(EmbedLayouts.Fluid, EmbedThemes.Light, false));

        Assert.That(result, Is.EqualTo("<div class=\"fb-post\" data-href=\"https://www.facebook.com/zuck/posts/10102577175875681\" data-width=\"auto\"></div>"));
    }

    [Test]
    public void Apply_FluidOnFacebookPostWithoutWidth_AddsDataWidthAuto()
    {
        var html = "<div class=\"fb-post\" data-href=\"https://www.facebook.com/zuck/posts/1\"></div>";

        var result = EmbedMarkupDecorator.Apply(html, "facebook-post", new EmbedPresentation(EmbedLayouts.Fluid, EmbedThemes.Light, false));

        Assert.That(result, Is.EqualTo("<div class=\"fb-post\" data-width=\"auto\" data-href=\"https://www.facebook.com/zuck/posts/1\"></div>"));
    }

    [TestCase(EmbedLayouts.Natural)]
    [TestCase(EmbedLayouts.Centered)]
    public void Apply_NonFluidLayouts_KeepFacebookWidth(string layout) =>
        Assert.That(EmbedMarkupDecorator.Apply(FacebookPostHtml, "facebook-post", new EmbedPresentation(layout, EmbedThemes.Light, false)), Is.EqualTo(FacebookPostHtml));

    [TestCase("instagram")]
    [TestCase("threads")]
    [TestCase("facebook-video")]
    public void Apply_FluidOnOtherPlatforms_LeavesMarkupAlone(string endpointKey)
    {
        var html = "<div class=\"fb-video\" data-href=\"x\" data-width=\"500\"></div>";

        Assert.That(EmbedMarkupDecorator.Apply(html, endpointKey, new EmbedPresentation(EmbedLayouts.Fluid, EmbedThemes.Light, false)), Is.EqualTo(html));
    }

    [Test]
    public void Apply_DefaultPresentation_ReturnsInputUnchanged()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EmbedMarkupDecorator.Apply(ThreadsHtml, "threads", EmbedPresentation.Default), Is.EqualTo(ThreadsHtml));
            Assert.That(EmbedMarkupDecorator.Apply(FacebookPostHtml, "facebook-post", EmbedPresentation.Default), Is.EqualTo(FacebookPostHtml));
        });
    }

    [Test]
    public void Apply_EmptyInputs_AreSafe()
    {
        var dark = new EmbedPresentation(EmbedLayouts.Fluid, EmbedThemes.Dark, true);

        Assert.Multiple(() =>
        {
            Assert.That(EmbedMarkupDecorator.Apply(string.Empty, "threads", dark), Is.Empty);
            Assert.That(EmbedMarkupDecorator.Apply(null!, "threads", dark), Is.Empty);
            Assert.That(EmbedMarkupDecorator.Apply(ThreadsHtml, null, dark), Is.EqualTo(ThreadsHtml));
            Assert.That(EmbedMarkupDecorator.Apply(ThreadsHtml, "", dark), Is.EqualTo(ThreadsHtml));
        });
    }

    [Test]
    public void Apply_NullPresentation_Throws() =>
        Assert.Throws<ArgumentNullException>(() => EmbedMarkupDecorator.Apply(ThreadsHtml, "threads", null!));
}
