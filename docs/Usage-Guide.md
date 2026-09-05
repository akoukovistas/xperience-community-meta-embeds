# Usage guide

This guide is for developers integrating the package and for the people who train editors. The [README](../README.md)
has the overview, the configuration reference and the caching details; this page walks through day-to-day use.

## 1. Install

```powershell
dotnet add package XperienceCommunity.MetaEmbeds
```

That is the whole setup. The package's Xperience module registers its services during application start (with
`TryAdd` semantics), so `Program.cs` needs no change and `appsettings.json` needs no section. The "Meta embed" widget
appears in the Page Builder widget list on the next start.

Call `AddXperienceCommunityMetaEmbeds` only when you want to change an option in code or bind the configuration
section explicitly:

```csharp
// Program.cs (optional)
builder.Services.AddXperienceCommunityMetaEmbeds(builder.Configuration);
// or
builder.Services.AddXperienceCommunityMetaEmbeds(o => o.ScriptMode = EmbedScriptMode.TagHelper);
```

Both calls are idempotent with the module's registrations. Your own registration of any package interface placed
before `AddKentico()` wins; a `Replace(...)` after it wins too.

## 2. Add the widget to a page

1. Open a page with a Page Builder editable area and pick **Meta embed** from the widget list.
2. Open **Configure widget** and paste the post URL into **Post URL**.
3. Apply. The embed renders inside the editor with a transparent overlay so it cannot swallow drag and click events.

Supported URL shapes (the host and path are checked server-side before anything is sent to Meta):

| Platform  | Accepted URL shapes                                                     | What renders                        |
|-----------|-------------------------------------------------------------------------|-------------------------------------|
| Threads   | `threads.com/@user/post/{code}`, `threads.com/t/{code}` (also `.net`)   | Threads post card                   |
| Instagram | `instagram.com/p/{code}`, `instagram.com/reel/{code}`                   | Instagram post / reel card          |
| Facebook  | `facebook.com/{user}/posts/{id}`                                         | Facebook post card                  |
| Facebook  | `facebook.com/reel/{id}`                                                 | Facebook video player               |

`http://`, missing `www.`, trailing slashes and query strings such as `?igsh=…` or `?hl=en` are all fine. Instagram
profile URLs (`instagram.com/{user}`) are accepted by the URL check for parity with Meta's WordPress plugin, but Meta's
tokenless endpoint rejected every profile URL tested on 2026-09-04, so editors see "Meta rejected this URL as not
embeddable" and the live site renders nothing.

The **Advanced** category (collapsed by default) holds **Source type**, which has a single option, *Single post*, in
this version. Leave it alone; it exists so a future release can add other source types without a migration.

## 3. What editors see when something is wrong

The live site never shows an error: a widget that cannot render produces no output at all. In Page Builder edit mode,
read-only mode and preview, the widget shows one of these messages instead of the embed:

| Situation                                        | Message                                                                                        |
|--------------------------------------------------|------------------------------------------------------------------------------------------------|
| Post URL is empty                                | This widget needs a post URL. Open Configure widget and paste one.                              |
| Not a valid absolute URL                         | That isn't a valid web address. Paste the full https:// link to the post.                       |
| Valid URL, but not a supported shape             | That URL isn't a supported Threads, Instagram or Facebook post link. Supported: …                |
| Meta: media not found / private / not embeddable | Meta can't embed this post. It may be private, deleted, or not embeddable.                      |
| Meta: URL rejected (incl. Instagram profiles)    | Meta rejected this URL as not embeddable.                                                       |
| Network error, timeout, 429, 5xx, bad JSON       | Couldn't reach Meta right now; the embed will appear once the service responds.                 |
| Meta returned markup the sanitiser removed       | Meta returned markup this widget doesn't recognise. Update the package or report it.             |
| Anything unexpected                              | Embed failed; see the event log.                                                                |

Meta's raw error text never reaches the page; it goes to the event log (source `XperienceCommunity.MetaEmbeds`).
Failures are cached too (1 hour for not-found/rejected, 2 minutes for transient errors), so fixing a URL and
re-applying shows the result immediately because the URL changed, while re-trying the same URL waits for the cache.

## 4. Script loading

Every embed needs Meta's client-side SDK for its platform. The package never echoes Meta's own `<script>` tag from the
oEmbed response; it emits its own tag, deduplicated per request, controlled by `ScriptMode`:

| `ScriptMode`  | Behaviour                                                                                                   | Use when                                                   |
|---------------|-------------------------------------------------------------------------------------------------------------|------------------------------------------------------------|
| `Inline`      | Default. The first widget on the page that needs an SDK emits `<script async defer crossorigin="anonymous" src="…">` right after its markup. | Zero-config sites without widget output caching.           |
| `TagHelper`   | Widgets only record which SDKs they need; the layout renders them once with `<meta-embeds-scripts />`.      | Widget output caching is on, or you want scripts before `</body>`. |
| `None`        | Nothing is emitted. The site loads the SDKs itself.                                                         | You already load the Meta SDKs, or a CSP nonce is required. |

For `TagHelper` mode, add the tag helper to `_ViewImports.cshtml` and place the element after the last widget zone,
normally right before `</body>`:

```cshtml
@* _ViewImports.cshtml *@
@addTagHelper *, XperienceCommunity.MetaEmbeds
```

```cshtml
@* _Layout.cshtml *@
    ...
    <meta-embeds-scripts />
</body>
```

The element renders nothing unless `ScriptMode` is `TagHelper`, so it is safe to leave in the layout.

SDK URLs emitted (hosts to allow in a `script-src` CSP): `https://www.instagram.com/embed.js`,
`https://www.threads.com/embed.js`, `https://connect.facebook.net/{FacebookSdkLocale}/sdk.js#xfbml=1&version={GraphApiVersion}`.
The SDKs then create iframes from `www.instagram.com`, `www.threads.com` and `www.facebook.com` (`frame-src`).

The Facebook SDK also expects a `<div id="fb-root"></div>` on the page. Meta's oEmbed markup includes one; the widget
strips it from the sanitised HTML and renders exactly one per request itself, emitted by the first Facebook embed on the
page, in every script mode. The SDK creates the element itself if it is missing, so a cached widget that happens not to
carry it is harmless.

## 5. Widget output caching

The widget is registered with `AllowCache = true`. Enable output caching per editable area as usual:

```cshtml
<editable-area area-identifier="main" allow-widget-output-cache="true" widget-output-cache-expires-after="@TimeSpan.FromMinutes(10)" />
```

Two things to know:

- Every widget render adds the dummy cache key `metaembeds|all` to its output cache dependencies, so
  `CacheHelper.TouchKey("metaembeds|all")` evicts cached widget output as well as the cached oEmbed responses.
- Script deduplication is per request, but cached output is per widget, so with `ScriptMode = Inline` two cached
  widgets on one page can both carry the SDK tag. Switch to `TagHelper` mode when output caching is on.

## 6. Purging

Cached oEmbed responses live 12 hours (success), 1 hour (not found / rejected) or 2 minutes (transient failure) by
default. To drop everything at once, for example after Meta changes its markup or a post becomes public:

```csharp
CMS.Helpers.CacheHelper.TouchKey("metaembeds|all");
```

Per platform: `CacheHelper.TouchKey("metaembeds|endpoint|instagram")` (also `threads`, `facebook-post`,
`facebook-video`).

## 7. Event log

Everything is logged through `ILogger<T>` and surfaces in the Xperience event log. Custom categories default to
`Warning`, which shows transient failures, sanitiser rejections and unexpected exceptions. To also see the
`Information`-level "not found" / "rejected" entries:

```json
{
  "Logging": {
    "XperienceEventLog": {
      "LogLevel": {
        "XperienceCommunity.MetaEmbeds": "Information"
      }
    }
  }
}
```

## 8. Rendering an embed outside the widget

`IEmbedResolver` is a public service. Any view component or controller can resolve a URL the same way the widget does:

```csharp
public sealed class MyComponent(IEmbedResolver resolver) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string url)
    {
        var result = await resolver.ResolveAsync(
            new EmbedRequest { SourceType = EmbedSourceTypes.Post, Input = url }, HttpContext.RequestAborted);

        return result.Succeeded
            ? View(result.Items[0])            // Items[0].Html is sanitised; Items[0].RequiredScripts lists the SDK URIs
            : Content(string.Empty);
    }
}
```

Remember to load the scripts in `RequiredScripts` yourself (or claim them through `IEmbedScriptRegistry` so the
`<meta-embeds-scripts />` tag helper renders them).
