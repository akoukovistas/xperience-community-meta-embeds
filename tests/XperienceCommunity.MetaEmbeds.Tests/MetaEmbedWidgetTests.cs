using System.Globalization;

using CMS.Core;

using Kentico.Content.Web.Mvc;
using Kentico.PageBuilder.Web.Mvc;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Rendering;
using XperienceCommunity.MetaEmbeds.Resources;
using XperienceCommunity.MetaEmbeds.Widgets;

namespace XperienceCommunity.MetaEmbeds.Tests;

[TestFixture]
public class MetaEmbedWidgetTests
{
    private static readonly Uri InstagramSdk = new("https://www.instagram.com/embed.js");
    private static readonly Uri FacebookSdk = new("https://connect.facebook.net/en_US/sdk.js#xfbml=1&version=v25.0");

    private const string InstagramHtml = "<blockquote class=\"instagram-media\" data-instgrm-permalink=\"https://www.instagram.com/p/fA9uwTtkSN/\"></blockquote>";
    private const string FacebookHtml = "<div id=\"fb-root\"></div><div class=\"fb-post\" data-href=\"https://www.facebook.com/zuck/posts/1\" data-width=\"552\"></div>";

    // ---- helpers -----------------------------------------------------------------------------------------------

    private static EmbedItem Item(string endpointKey, string html, params Uri[] scripts) => new()
    {
        EndpointKey = endpointKey,
        ProviderName = "Test",
        Html = html,
        Type = "rich",
        SourceUrl = new Uri("https://www.instagram.com/p/fA9uwTtkSN/"),
        RequiredScripts = scripts,
    };

    private static IEmbedResolver ResolverReturning(EmbedResult result)
    {
        var resolver = Substitute.For<IEmbedResolver>();
        resolver.ResolveAsync(Arg.Any<EmbedRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(result));
        return resolver;
    }

    private static ILocalizationService Localization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(call => "LOC:" + call.ArgAt<string>(0));
        return localization;
    }

    private static IEmbedScriptRegistry SharedRegistry(HttpContext httpContext)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new EmbedScriptRegistry(accessor);
    }

    private static MetaEmbedWidget CreateWidget(
        IEmbedResolver resolver,
        IEmbedScriptRegistry scripts,
        bool edit,
        MetaEmbedsOptions? options = null,
        ILocalizationService? localization = null,
        HttpContext? httpContext = null)
    {
        var detector = Substitute.For<IWidgetRenderModeDetector>();
        detector.IsEditOrPreview(Arg.Any<HttpContext?>()).Returns(edit);

        var monitor = Substitute.For<IOptionsMonitor<MetaEmbedsOptions>>();
        monitor.CurrentValue.Returns(options ?? new MetaEmbedsOptions());

        var widget = new MetaEmbedWidget(
            resolver, scripts, detector, monitor, NullLogger<MetaEmbedWidget>.Instance, localization ?? Localization());

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        var viewContext = new ViewContext { HttpContext = httpContext ?? new DefaultHttpContext(), ViewData = viewData };
        widget.ViewComponentContext = new ViewComponentContext { ViewContext = viewContext };
        return widget;
    }

    private static ComponentViewModel<MetaEmbedWidgetProperties> Model(string url = "https://www.instagram.com/p/fA9uwTtkSN/", string? sourceType = "post") =>
        Model(new MetaEmbedWidgetProperties { Url = url, SourceType = sourceType! });

    private static ComponentViewModel<MetaEmbedWidgetProperties> Model(MetaEmbedWidgetProperties properties) =>
        ComponentViewModel<MetaEmbedWidgetProperties>.Create(new RoutedWebPage { LanguageName = "en" }, properties);

    private static MetaEmbedWidgetViewModel ViewModelOf(IViewComponentResult result)
    {
        Assert.That(result, Is.InstanceOf<ViewViewComponentResult>());
        var view = (ViewViewComponentResult)result;
        Assert.That(view.ViewName, Is.EqualTo(MetaEmbedWidget.ViewPath));
        Assert.That(view.ViewData?.Model, Is.InstanceOf<MetaEmbedWidgetViewModel>());
        return (MetaEmbedWidgetViewModel)view.ViewData!.Model!;
    }

    // ---- appearance --------------------------------------------------------------------------------------------

    [Test]
    public async Task Appearance_Defaults_ProduceNaturalClassesNoStyleAndNoParameters()
    {
        var resolver = ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk)));
        var widget = CreateWidget(resolver, SharedRegistry(new DefaultHttpContext()), edit: false);

        var model = ViewModelOf(await widget.InvokeAsync(Model()));

        Assert.Multiple(() =>
        {
            Assert.That(model.CssClass, Is.EqualTo("meta-embed meta-embed--instagram meta-embed--layout-natural"));
            Assert.That(model.WrapperStyle, Is.Null);
            Assert.That(model.Presentation, Is.EqualTo(EmbedPresentation.Default));
            Assert.That(model.Html, Is.EqualTo(InstagramHtml));
        });
        await resolver.Received(1).ResolveAsync(Arg.Is<EmbedRequest>(r => r.Parameters.Count == 0), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Appearance_HideCaption_SendsTheParameterAndAddsTheModifierClass()
    {
        var resolver = ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk)));
        var widget = CreateWidget(resolver, SharedRegistry(new DefaultHttpContext()), edit: false);

        var model = ViewModelOf(await widget.InvokeAsync(Model(new MetaEmbedWidgetProperties
        {
            Url = "https://www.instagram.com/p/fA9uwTtkSN/",
            HideCaption = true,
        })));

        Assert.That(model.CssClass, Does.Contain("meta-embed--no-caption"));
        await resolver.Received(1).ResolveAsync(
            Arg.Is<EmbedRequest>(r => r.Parameters[EmbedRequestParameters.HideCaption] == "true"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Appearance_Centered_AddsLayoutClassAndFlexStyle()
    {
        var widget = CreateWidget(
            ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk))),
            SharedRegistry(new DefaultHttpContext()),
            edit: false);

        var model = ViewModelOf(await widget.InvokeAsync(Model(new MetaEmbedWidgetProperties
        {
            Url = "https://www.instagram.com/p/fA9uwTtkSN/",
            Layout = "Centered",
        })));

        Assert.Multiple(() =>
        {
            Assert.That(model.CssClass, Does.Contain("meta-embed--layout-centered"));
            Assert.That(model.WrapperStyle, Is.EqualTo("display:flex;justify-content:center;"));
        });
    }

    [Test]
    public async Task Appearance_FluidFacebookPost_RewritesDataWidthAndStillStripsRoot()
    {
        var widget = CreateWidget(
            ResolverReturning(EmbedResult.Success(Item("facebook-post", FacebookHtml, FacebookSdk))),
            SharedRegistry(new DefaultHttpContext()),
            edit: false);

        var model = ViewModelOf(await widget.InvokeAsync(Model(new MetaEmbedWidgetProperties
        {
            Url = "https://www.facebook.com/zuck/posts/1",
            Layout = EmbedLayouts.Fluid,
        })));

        Assert.Multiple(() =>
        {
            Assert.That(model.Html, Does.Contain("data-width=\"auto\""));
            Assert.That(model.Html, Does.Not.Contain("data-width=\"552\""));
            Assert.That(model.Html, Does.Not.Contain("fb-root"), "root is still stripped and rendered once by the view");
            Assert.That(model.EmitFacebookRoot, Is.True);
            Assert.That(model.CssClass, Is.EqualTo("meta-embed meta-embed--facebook-post meta-embed--layout-fluid"));
            Assert.That(model.WrapperStyle, Is.Null);
        });
    }

    [Test]
    public async Task Appearance_DarkThreads_RewritesThemeAndAddsModifierClass()
    {
        const string threadsHtml = "<blockquote class=\"text-post-media\" data-theme=\"light\"></blockquote>";
        var widget = CreateWidget(
            ResolverReturning(EmbedResult.Success(Item("threads", threadsHtml, new Uri("https://www.threads.com/embed.js")))),
            SharedRegistry(new DefaultHttpContext()),
            edit: false);

        var model = ViewModelOf(await widget.InvokeAsync(Model(new MetaEmbedWidgetProperties
        {
            Url = "https://www.threads.com/t/DWjTI0cgH5O/",
            Theme = EmbedThemes.Dark,
        })));

        Assert.Multiple(() =>
        {
            Assert.That(model.Html, Is.EqualTo("<blockquote class=\"text-post-media\" data-theme=\"dark\"></blockquote>"));
            Assert.That(model.CssClass, Does.Contain("meta-embed--theme-dark"));
        });
    }

    [Test]
    public async Task Appearance_DarkThemeOnInstagram_LeavesMarkupAlone()
    {
        var widget = CreateWidget(
            ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk))),
            SharedRegistry(new DefaultHttpContext()),
            edit: false);

        var model = ViewModelOf(await widget.InvokeAsync(Model(new MetaEmbedWidgetProperties
        {
            Url = "https://www.instagram.com/p/fA9uwTtkSN/",
            Theme = EmbedThemes.Dark,
        })));

        Assert.Multiple(() =>
        {
            Assert.That(model.Html, Is.EqualTo(InstagramHtml));
            Assert.That(model.CssClass, Does.Contain("meta-embed--theme-dark"), "the CSS hook is still there for site styling");
        });
    }

    [Test]
    public async Task Appearance_UnknownValues_FallBackToDefaults()
    {
        var widget = CreateWidget(
            ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk))),
            SharedRegistry(new DefaultHttpContext()),
            edit: false);

        var model = ViewModelOf(await widget.InvokeAsync(Model(new MetaEmbedWidgetProperties
        {
            Url = "https://www.instagram.com/p/fA9uwTtkSN/",
            Layout = "sideways",
            Theme = null!,
        })));

        Assert.That(model.Presentation, Is.EqualTo(EmbedPresentation.Default));
    }

    // ---- failure paths -----------------------------------------------------------------------------------------

    [Test]
    public async Task Failure_OnLiveSite_ReturnsEmptyContent()
    {
        var widget = CreateWidget(ResolverReturning(EmbedResult.Failed(EmbedFailureKind.NotFound)), SharedRegistry(new DefaultHttpContext()), edit: false);

        var result = await widget.InvokeAsync(Model());

        Assert.That(result, Is.InstanceOf<ContentViewComponentResult>());
        Assert.That(((ContentViewComponentResult)result).Content, Is.EqualTo(string.Empty));
    }

    [TestCase(EmbedFailureKind.NotConfigured, "xperiencecommunity.metaembeds.failure.notconfigured")]
    [TestCase(EmbedFailureKind.InvalidInput, "xperiencecommunity.metaembeds.failure.invalidinput")]
    [TestCase(EmbedFailureKind.UnsupportedInput, "xperiencecommunity.metaembeds.failure.unsupportedinput")]
    [TestCase(EmbedFailureKind.NotFound, "xperiencecommunity.metaembeds.failure.notfound")]
    [TestCase(EmbedFailureKind.RejectedByProvider, "xperiencecommunity.metaembeds.failure.rejectedbyprovider")]
    [TestCase(EmbedFailureKind.Transient, "xperiencecommunity.metaembeds.failure.transient")]
    [TestCase(EmbedFailureKind.UnexpectedMarkup, "xperiencecommunity.metaembeds.failure.unexpectedmarkup")]
    [TestCase(EmbedFailureKind.Internal, "xperiencecommunity.metaembeds.failure.internal")]
    public async Task Failure_InEditMode_ReturnsViewWithLocalisedMessage(EmbedFailureKind kind, string expectedKey)
    {
        var localization = Localization();
        var widget = CreateWidget(ResolverReturning(EmbedResult.Failed(kind)), SharedRegistry(new DefaultHttpContext()), edit: true, localization: localization);

        var model = ViewModelOf(await widget.InvokeAsync(Model()));

        Assert.Multiple(() =>
        {
            Assert.That(model.EditorMessage, Is.EqualTo("LOC:" + expectedKey));
            Assert.That(model.IsEditMode, Is.True);
            Assert.That(model.Html, Is.Empty);
            Assert.That(model.ScriptsToEmit, Is.Empty);
        });
        localization.Received(1).GetString(expectedKey, Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Failure_WithExplicitEditorMessage_UsesItInsteadOfTheResource()
    {
        var failure = new EmbedFailure { Kind = EmbedFailureKind.RejectedByProvider, EditorMessage = "Custom message" };
        var localization = Localization();
        var widget = CreateWidget(ResolverReturning(EmbedResult.Failed(failure)), SharedRegistry(new DefaultHttpContext()), edit: true, localization: localization);

        var model = ViewModelOf(await widget.InvokeAsync(Model()));

        Assert.That(model.EditorMessage, Is.EqualTo("Custom message"));
        localization.DidNotReceiveWithAnyArgs().GetString(default!, default, default);
    }

    [Test]
    public async Task ResolverThrows_ReturnsEmptyContent_InEveryMode([Values(true, false)] bool edit)
    {
        var resolver = Substitute.For<IEmbedResolver>();
        resolver.ResolveAsync(Arg.Any<EmbedRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        var widget = CreateWidget(resolver, SharedRegistry(new DefaultHttpContext()), edit);

        var result = await widget.InvokeAsync(Model());

        Assert.That(result, Is.InstanceOf<ContentViewComponentResult>());
        Assert.That(((ContentViewComponentResult)result).Content, Is.Empty);
    }

    [Test]
    public async Task NullViewModel_IsResolvedAsEmptyInput()
    {
        var resolver = ResolverReturning(EmbedResult.Failed(EmbedFailureKind.NotConfigured));
        var widget = CreateWidget(resolver, SharedRegistry(new DefaultHttpContext()), edit: false);

        var result = await widget.InvokeAsync(null!);

        Assert.That(result, Is.InstanceOf<ContentViewComponentResult>());
        await resolver.Received(1).ResolveAsync(
            Arg.Is<EmbedRequest>(r => r.Input == string.Empty && r.SourceType == EmbedSourceTypes.Post),
            Arg.Any<CancellationToken>());
    }

    // ---- request shaping ---------------------------------------------------------------------------------------

    [Test]
    public async Task Resolver_ReceivesTrimmedUrlNormalisedSourceTypeAndPageLanguage()
    {
        var resolver = ResolverReturning(EmbedResult.Failed(EmbedFailureKind.NotFound));
        var widget = CreateWidget(resolver, SharedRegistry(new DefaultHttpContext()), edit: false);

        await widget.InvokeAsync(Model(url: "  https://www.threads.com/t/DWjTI0cgH5O/ ", sourceType: " POST "));

        await resolver.Received(1).ResolveAsync(
            Arg.Is<EmbedRequest>(r =>
                r.Input == "https://www.threads.com/t/DWjTI0cgH5O/"
                && r.SourceType == EmbedSourceTypes.Post
                && r.Culture == "en"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Resolver_ReceivesPostWhenStoredSourceTypeIsNull()
    {
        var resolver = ResolverReturning(EmbedResult.Failed(EmbedFailureKind.NotFound));
        var widget = CreateWidget(resolver, SharedRegistry(new DefaultHttpContext()), edit: false);

        await widget.InvokeAsync(Model(sourceType: null));

        await resolver.Received(1).ResolveAsync(
            Arg.Is<EmbedRequest>(r => r.SourceType == EmbedSourceTypes.Post),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CacheDependency_OnAllKey_IsSetForSuccessAndFailure()
    {
        var success = Model();
        var failure = Model();
        var registry = SharedRegistry(new DefaultHttpContext());

        await CreateWidget(ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk))), registry, edit: false).InvokeAsync(success);
        await CreateWidget(ResolverReturning(EmbedResult.Failed(EmbedFailureKind.NotFound)), registry, edit: false).InvokeAsync(failure);

        Assert.That(success.CacheDependencies.CacheKeys, Is.EqualTo(new[] { MetaEmbedsConstants.CacheKeyAll }).AsCollection);
        Assert.That(failure.CacheDependencies.CacheKeys, Is.EqualTo(new[] { MetaEmbedsConstants.CacheKeyAll }).AsCollection);
    }

    // ---- success paths -----------------------------------------------------------------------------------------

    [Test]
    public async Task Success_RendersHtmlAndEmitsEachScriptOnce_AcrossTwoWidgetsInOneRequest()
    {
        var httpContext = new DefaultHttpContext();
        var result = EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk));

        // Two widget instances, two registry instances, one HttpContext: state must be shared through HttpContext.Items.
        var first = CreateWidget(ResolverReturning(result), SharedRegistry(httpContext), edit: false, httpContext: httpContext);
        var second = CreateWidget(ResolverReturning(result), SharedRegistry(httpContext), edit: false, httpContext: httpContext);

        var firstModel = ViewModelOf(await first.InvokeAsync(Model()));
        var secondModel = ViewModelOf(await second.InvokeAsync(Model()));

        Assert.Multiple(() =>
        {
            Assert.That(firstModel.Html, Is.EqualTo(InstagramHtml));
            Assert.That(firstModel.EndpointKey, Is.EqualTo("instagram"));
            Assert.That(firstModel.EditorMessage, Is.Null);
            Assert.That(firstModel.IsEditMode, Is.False);
            Assert.That(firstModel.EmitFacebookRoot, Is.False);
            Assert.That(firstModel.ScriptsToEmit, Is.EqualTo(new[] { InstagramSdk }).AsCollection);

            Assert.That(secondModel.Html, Is.EqualTo(InstagramHtml));
            Assert.That(secondModel.ScriptsToEmit, Is.Empty, "the second widget must not emit the SDK again");
        });
    }

    [Test]
    public async Task Success_InEditMode_SetsIsEditModeAndNoMessage()
    {
        var widget = CreateWidget(ResolverReturning(EmbedResult.Success(Item("threads", "<blockquote class=\"text-post-media\"></blockquote>", new Uri("https://www.threads.com/embed.js")))), SharedRegistry(new DefaultHttpContext()), edit: true);

        var model = ViewModelOf(await widget.InvokeAsync(Model()));

        Assert.That(model.IsEditMode, Is.True);
        Assert.That(model.EditorMessage, Is.Null);
        Assert.That(model.EndpointKey, Is.EqualTo("threads"));
    }

    [Test]
    public async Task Success_TagHelperMode_ClaimsScriptsButEmitsNone()
    {
        var registry = SharedRegistry(new DefaultHttpContext());
        var options = new MetaEmbedsOptions { ScriptMode = EmbedScriptMode.TagHelper };
        var widget = CreateWidget(ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk))), registry, edit: false, options: options);

        var model = ViewModelOf(await widget.InvokeAsync(Model()));

        Assert.That(model.ScriptsToEmit, Is.Empty);
        Assert.That(registry.Claimed, Is.EqualTo(new[] { InstagramSdk }).AsCollection, "claimed so <meta-embeds-scripts /> can render it");
        Assert.That(registry.TryClaim(InstagramSdk), Is.False);
    }

    [Test]
    public async Task Success_NoneMode_NeitherClaimsNorEmits()
    {
        var registry = SharedRegistry(new DefaultHttpContext());
        var options = new MetaEmbedsOptions { ScriptMode = EmbedScriptMode.None };
        var widget = CreateWidget(ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk))), registry, edit: false, options: options);

        var model = ViewModelOf(await widget.InvokeAsync(Model()));

        Assert.That(model.ScriptsToEmit, Is.Empty);
        Assert.That(registry.Claimed, Is.Empty);
        Assert.That(model.Html, Is.EqualTo(InstagramHtml));
    }

    [Test]
    public async Task Success_Facebook_StripsMetaRootDivAndEmitsOurOwnOncePerRequest()
    {
        var httpContext = new DefaultHttpContext();
        var result = EmbedResult.Success(Item("facebook-post", FacebookHtml, FacebookSdk));
        var first = CreateWidget(ResolverReturning(result), SharedRegistry(httpContext), edit: false, httpContext: httpContext);
        var second = CreateWidget(ResolverReturning(result), SharedRegistry(httpContext), edit: false, httpContext: httpContext);

        var firstModel = ViewModelOf(await first.InvokeAsync(Model(url: "https://www.facebook.com/zuck/posts/1")));
        var secondModel = ViewModelOf(await second.InvokeAsync(Model(url: "https://www.facebook.com/zuck/posts/1")));

        Assert.Multiple(() =>
        {
            Assert.That(firstModel.Html, Does.Not.Contain("fb-root"));
            Assert.That(firstModel.Html, Does.Contain("class=\"fb-post\""));
            Assert.That(firstModel.EmitFacebookRoot, Is.True);
            Assert.That(firstModel.ScriptsToEmit, Is.EqualTo(new[] { FacebookSdk }).AsCollection);

            Assert.That(secondModel.Html, Does.Not.Contain("fb-root"));
            Assert.That(secondModel.EmitFacebookRoot, Is.False);
            Assert.That(secondModel.ScriptsToEmit, Is.Empty);
        });
    }

    [Test]
    public async Task Success_NonFacebook_NeverEmitsFacebookRoot()
    {
        var registry = SharedRegistry(new DefaultHttpContext());
        var widget = CreateWidget(ResolverReturning(EmbedResult.Success(Item("instagram", InstagramHtml, InstagramSdk))), registry, edit: false);

        var model = ViewModelOf(await widget.InvokeAsync(Model()));

        Assert.That(model.EmitFacebookRoot, Is.False);
        Assert.That(registry.TryClaimMarker(MetaEmbedWidget.FacebookRootMarker), Is.True, "the marker was never claimed");
    }

    // ---- resources ----------------------------------------------------------------------------------------------

    [Test]
    public void Resources_AreEmbeddedUnderTheExpectedManifestName()
    {
        var names = typeof(MetaEmbedWidget).Assembly.GetManifestResourceNames();

        Assert.That(names, Does.Contain("XperienceCommunity.MetaEmbeds.Resources.MetaEmbedsResources.resources"));
    }

    [Test]
    public void Resources_ContainAnEditorMessageForEveryFailureKind()
    {
        foreach (var kind in Enum.GetValues<EmbedFailureKind>())
        {
            var key = MetaEmbedsResources.FailureKey(kind);
            var text = MetaEmbedsResources.ResourceManager.GetString(key, CultureInfo.InvariantCulture);

            Assert.That(text, Is.Not.Null.And.Not.Empty, $"missing resource '{key}'");
        }
    }

    [TestCase("xperiencecommunity.metaembeds.widget.name", "Meta embed")]
    [TestCase("xperiencecommunity.metaembeds.properties.url.label", "Post URL")]
    [TestCase("xperiencecommunity.metaembeds.properties.sourceType.label", "Source type")]
    [TestCase("xperiencecommunity.metaembeds.properties.sourceType.options.post", "Single post")]
    [TestCase("xperiencecommunity.metaembeds.properties.category.advanced", "Advanced")]
    public void Resources_ContainTheBuilderStrings(string key, string expected)
    {
        Assert.That(MetaEmbedsResources.ResourceManager.GetString(key, CultureInfo.InvariantCulture), Is.EqualTo(expected));
    }

    [Test]
    public void Resources_ContainEveryKeyReferencedByTheWidgetAnnotations()
    {
        var referenced = new[]
        {
            "xperiencecommunity.metaembeds.widget.name",
            "xperiencecommunity.metaembeds.widget.description",
            "xperiencecommunity.metaembeds.properties.url.label",
            "xperiencecommunity.metaembeds.properties.url.explanationText",
            "xperiencecommunity.metaembeds.properties.sourceType.label",
            "xperiencecommunity.metaembeds.properties.sourceType.explanationText",
            "xperiencecommunity.metaembeds.properties.sourceType.options.post",
            "xperiencecommunity.metaembeds.properties.category.advanced",
        };

        foreach (var key in referenced)
        {
            Assert.That(MetaEmbedsResources.ResourceManager.GetString(key, CultureInfo.InvariantCulture), Is.Not.Null.And.Not.Empty, key);
        }
    }

    // ---- render mode detector ----------------------------------------------------------------------------------

    [Test]
    public void RenderModeDetector_PageBuilderEditMode_IsEdit()
    {
        var context = Substitute.For<IPageBuilderDataContext>();
        context.EditMode.Returns(true);
        var retriever = Substitute.For<IPageBuilderDataContextRetriever>();
        retriever.Retrieve().Returns(context);

        Assert.That(new PageBuilderRenderModeDetector(retriever).IsEditOrPreview(new DefaultHttpContext()), Is.True);
    }

    [Test]
    public void RenderModeDetector_PageBuilderOff_WithoutPreviewFeature_IsLive()
    {
        var context = Substitute.For<IPageBuilderDataContext>();
        context.EditMode.Returns(false);
        var retriever = Substitute.For<IPageBuilderDataContextRetriever>();
        retriever.Retrieve().Returns(context);

        Assert.That(new PageBuilderRenderModeDetector(retriever).IsEditOrPreview(new DefaultHttpContext()), Is.False);
        Assert.That(new PageBuilderRenderModeDetector(retriever).IsEditOrPreview(null), Is.False);
    }

    [Test]
    public void RenderModeDetector_RetrieverThrows_IsLive()
    {
        var retriever = Substitute.For<IPageBuilderDataContextRetriever>();
        retriever.Retrieve().Throws(new InvalidOperationException("no page builder context"));

        Assert.That(new PageBuilderRenderModeDetector(retriever).IsEditOrPreview(new DefaultHttpContext()), Is.False);
    }

    // ---- <meta-embeds-scripts /> --------------------------------------------------------------------------------

    private static (TagHelperContext Context, TagHelperOutput Output) TagHelperIo()
    {
        var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "meta-embeds-scripts",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        return (context, output);
    }

    private static MetaEmbedsScriptsTagHelper TagHelper(IEmbedScriptRegistry registry, EmbedScriptMode mode)
    {
        var monitor = Substitute.For<IOptionsMonitor<MetaEmbedsOptions>>();
        monitor.CurrentValue.Returns(new MetaEmbedsOptions { ScriptMode = mode });
        return new MetaEmbedsScriptsTagHelper(registry, monitor);
    }

    [Test]
    public void TagHelper_InTagHelperMode_RendersOneEncodedScriptTagPerClaimedUriInOrder()
    {
        var registry = SharedRegistry(new DefaultHttpContext());
        registry.TryClaim(FacebookSdk);
        registry.TryClaim(InstagramSdk);
        var (context, output) = TagHelperIo();

        TagHelper(registry, EmbedScriptMode.TagHelper).Process(context, output);

        var html = Render(output);
        Assert.Multiple(() =>
        {
            Assert.That(output.TagName, Is.Null, "the custom element itself is never rendered");
            Assert.That(html, Is.EqualTo(
                "<script async defer crossorigin=\"anonymous\" src=\"https://connect.facebook.net/en_US/sdk.js#xfbml=1&amp;version=v25.0\"></script>"
                + "<script async defer crossorigin=\"anonymous\" src=\"https://www.instagram.com/embed.js\"></script>"));
        });
    }

    [TestCase(EmbedScriptMode.Inline)]
    [TestCase(EmbedScriptMode.None)]
    public void TagHelper_InOtherModes_RendersNothing(EmbedScriptMode mode)
    {
        var registry = SharedRegistry(new DefaultHttpContext());
        registry.TryClaim(InstagramSdk);
        var (context, output) = TagHelperIo();

        TagHelper(registry, mode).Process(context, output);

        Assert.That(Render(output), Is.Empty, "the element and its content must both be suppressed");
    }

    private static string Render(TagHelperOutput output)
    {
        using var writer = new StringWriter();
        output.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }

    [Test]
    public void TagHelper_WithNothingClaimed_RendersEmptyOutput()
    {
        var (context, output) = TagHelperIo();

        TagHelper(SharedRegistry(new DefaultHttpContext()), EmbedScriptMode.TagHelper).Process(context, output);

        Assert.That(output.Content.GetContent(), Is.Empty);
    }
}
