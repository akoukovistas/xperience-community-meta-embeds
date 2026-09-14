using NUnit.Framework;

using System.Diagnostics;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using XperienceCommunity.MetaEmbeds.Rendering;
using XperienceCommunity.MetaEmbeds.Widgets;

using ActionContext = Microsoft.AspNetCore.Mvc.ActionContext;

namespace XperienceCommunity.MetaEmbeds.Tests;

/// <summary>
/// Renders <c>_MetaEmbedWidget.cshtml</c> itself, through the real Razor view engine against the view compiled into
/// the package assembly. Everything upstream of the view is covered by <see cref="MetaEmbedWidgetTests"/>; this fixture
/// covers the last hop - the <c>@Html.Raw</c> calls, the edit-mode overlay, <c>fb-root</c> and the script loop.
/// </summary>
[TestFixture]
public class WidgetViewTests
{
    private const string Markup = "<blockquote class=\"instagram-media\">post</blockquote>";

    private ServiceProvider provider = null!;
    private IRazorViewEngine viewEngine = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        services.AddSingleton<DiagnosticSource>(new DiagnosticListener("XperienceCommunity.MetaEmbeds.Tests"));
        services.AddSingleton(new DiagnosticListener("XperienceCommunity.MetaEmbeds.Tests"));
        services.AddSingleton<ITempDataProvider, StubTempDataProvider>();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services
            .AddMvcCore()
            .AddRazorViewEngine()
            .AddApplicationPart(typeof(MetaEmbedWidget).Assembly);

        provider = services.BuildServiceProvider();
        viewEngine = provider.GetRequiredService<IRazorViewEngine>();
    }

    [OneTimeTearDown]
    public void TearDown() => provider?.Dispose();

    [Test]
    public void ViewPath_ResolvesToTheViewCompiledIntoThePackage()
    {
        var result = viewEngine.GetView(executingFilePath: null, MetaEmbedWidget.ViewPath, isMainPage: false);

        Assert.That(result.Success, Is.True, $"searched: {string.Join(", ", result.SearchedLocations ?? [])}");
    }

    [Test]
    public async Task Html_IsRenderedRawInsideTheWrapper()
    {
        var html = await RenderAsync(new MetaEmbedWidgetViewModel { Html = Markup, CssClass = "meta-embed meta-embed--instagram" });

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain(Markup), "@Html.Raw must not escape the sanitised markup");
            Assert.That(html, Does.Contain("class=\"meta-embed meta-embed--instagram\""));
            Assert.That(html, Does.Not.Contain("&lt;blockquote"));
        });
    }

    [Test]
    public async Task EditorMessage_IsHtmlEncodedAndReplacesTheEmbed()
    {
        var html = await RenderAsync(new MetaEmbedWidgetViewModel
        {
            IsEditMode = true,
            EditorMessage = "<script>alert(1)</script> paste a post URL",
            Html = Markup,
        });

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("meta-embed--message"));
            Assert.That(html, Does.Contain("role=\"status\""));
            Assert.That(html, Does.Not.Contain("<script>alert(1)</script>"), "the message is encoded, never raw");
            Assert.That(html, Does.Contain("&lt;script&gt;"));
            Assert.That(html, Does.Not.Contain(Markup), "a message replaces the embed");
        });
    }

    [Test]
    public async Task EditMode_AddsTheClickBlockingOverlayOverTheEmbed()
    {
        var edit = await RenderAsync(new MetaEmbedWidgetViewModel { Html = Markup, IsEditMode = true });
        var live = await RenderAsync(new MetaEmbedWidgetViewModel { Html = Markup, IsEditMode = false });

        Assert.Multiple(() =>
        {
            Assert.That(edit, Does.Contain("position: absolute; inset: 0;"), "overlay in edit mode");
            Assert.That(edit, Does.Contain("position: relative;"));
            Assert.That(live, Does.Not.Contain("position: absolute; inset: 0;"), "no overlay on the live site");
        });
    }

    [Test]
    public async Task EmitFacebookRoot_RendersExactlyOneRootBeforeTheEmbed()
    {
        var html = await RenderAsync(new MetaEmbedWidgetViewModel
        {
            Html = "<div class=\"fb-post\"></div>",
            EndpointKey = "facebook-post",
            EmitFacebookRoot = true,
        });
        var without = await RenderAsync(new MetaEmbedWidgetViewModel { Html = "<div class=\"fb-post\"></div>", EndpointKey = "facebook-post" });

        Assert.Multiple(() =>
        {
            Assert.That(html.Split("<div id=\"fb-root\"></div>").Length - 1, Is.EqualTo(1));
            Assert.That(html.IndexOf("fb-root", StringComparison.Ordinal), Is.LessThan(html.IndexOf("fb-post", StringComparison.Ordinal)));
            Assert.That(without, Does.Not.Contain("fb-root"));
        });
    }

    [Test]
    public async Task ScriptsToEmit_RenderOneTagEachAfterTheEmbed()
    {
        var scripts = new[] { new Uri("https://www.instagram.com/embed.js"), new Uri("https://connect.facebook.net/en_US/sdk.js") };

        var html = await RenderAsync(new MetaEmbedWidgetViewModel { Html = Markup, ScriptsToEmit = scripts });

        Assert.Multiple(() =>
        {
            foreach (var script in scripts)
            {
                Assert.That(html, Does.Contain(EmbedScriptTag.Render(script)), script.AbsoluteUri);
                Assert.That(html.IndexOf(script.AbsoluteUri, StringComparison.Ordinal), Is.GreaterThan(html.IndexOf(Markup, StringComparison.Ordinal)));
            }
        });
    }

    [Test]
    public async Task WrapperStyle_IsAppliedOnlyWhenPresent()
    {
        var centered = await RenderAsync(new MetaEmbedWidgetViewModel { Html = Markup, WrapperStyle = "margin: 0 auto;" });
        var natural = await RenderAsync(new MetaEmbedWidgetViewModel { Html = Markup });

        Assert.Multiple(() =>
        {
            Assert.That(centered, Does.Contain("style=\"margin: 0 auto;\""));
            Assert.That(natural, Does.Not.Contain("style="));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    public async Task NoHtmlAndNoMessage_RendersNothing(string? markup)
    {
        var html = await RenderAsync(new MetaEmbedWidgetViewModel { Html = markup! });

        Assert.That(html.Trim(), Is.Empty);
    }

    private async Task<string> RenderAsync(MetaEmbedWidgetViewModel model)
    {
        var result = viewEngine.GetView(executingFilePath: null, MetaEmbedWidget.ViewPath, isMainPage: false);
        Assert.That(result.Success, Is.True, MetaEmbedWidget.ViewPath);
        var view = result.View!;

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var viewData = new ViewDataDictionary<MetaEmbedWidgetViewModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };
        var tempData = new TempDataDictionary(httpContext, provider.GetRequiredService<ITempDataProvider>());

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, view, viewData, tempData, writer, new HtmlHelperOptions());
        await view.RenderAsync(viewContext);
        return writer.ToString();
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ApplicationName { get; set; } = typeof(MetaEmbedWidget).Assembly.GetName().Name!;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = Environments.Development;
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // Nothing to persist: the view never writes TempData.
        }
    }

    private sealed class NullLoggerProvider : ILoggerProvider
    {
        public static readonly NullLoggerProvider Instance = new();

        public ILogger CreateLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose()
        {
            // Nothing to dispose.
        }
    }
}
