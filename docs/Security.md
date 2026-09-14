# Security

How XperienceCommunity.MetaEmbeds treats the markup Meta returns, what it lets through, and what a site still has to
decide for itself. The rules below are implemented in `src/XperienceCommunity.MetaEmbeds/Rendering/` and pinned by
`tests/XperienceCommunity.MetaEmbeds.Tests/SanitizerTests.cs`.

## Threat model

- **Meta's oEmbed `html` is untrusted input.** It arrives over HTTPS from Meta's Graph endpoints, but the widget does
  not assume that pipeline is intact: a compromised endpoint, a changed response shape, or a tampered cache entry must
  not be able to run script in the editor's or visitor's browser. The markup is therefore reduced to an explicit
  allowlist before it is cached or rendered; everything not on the list is removed.
- **The editor's URL is validated but never fetched.** The URL pasted into the widget is matched against fixed host and
  path patterns (`IEmbedUrlMatcher`) and then sent to Meta as a query-string parameter. This library never issues a
  request to the URL an editor typed.
- **Only fixed Meta endpoints are contacted.** Outbound calls go to the four oEmbed endpoints defined in
  `MetaOEmbedEndpoints`, which live on two hosts: `graph.facebook.com` (Instagram, Facebook post, Facebook video) and
  `graph.threads.com` (Threads). No redirects are followed and no cookies are sent.
- **The SDK script tags Meta returns are never echoed.** The widget emits its own `<script>` tags for the SDK URLs it
  owns (`www.instagram.com/embed.js`, `www.threads.com/embed.js`, `connect.facebook.net/{locale}/sdk.js`); Meta's
  tags are removed by the allowlist, whatever URL they point at.

Out of scope: the behaviour of Meta's frontend SDKs once they run on the page. They are Meta's code, loaded from Meta's
hosts, and subject to Meta's Platform Terms and privacy policy. See the CSP section for containing them.

## Sanitiser design

`EmbedHtmlSanitizer` is the default `IEmbedHtmlSanitizer`. It is built on
[HtmlSanitizer](https://github.com/mganss/HtmlSanitizer) (`Ganss.Xss`, the same package Kentico.Xperience.WebApp
already depends on) with one allowlist **profile** per platform. The endpoint definition names the profile
(`MetaOEmbedEndpoint.SanitizerProfile`) and the CSS selector the sanitised markup must still satisfy
(`ExpectedRootSelector`). The implementation also uses AngleSharp/AngleSharp.Css APIs directly for DOM post-rules
(`ApplyPostRules`) and root-selector checks (`HasRoot`).

Per call:

1. Parse the fragment with AngleSharp (HTML5 parsing, no scripting).
2. Remove every element not in the profile's tag list, together with its content.
3. Remove every attribute not in the profile's attribute list. `data-*` attributes are only kept when listed
   individually.
4. Screen URL-valued attributes: they must be absolute `https` URLs, with no userinfo and no explicit port, whose host is
   one of the profile's domains or a subdomain of it. Anything else (`javascript:`, `data:`, `vbscript:`, `http:`,
   protocol-relative, relative, foreign host, look-alike host) removes the attribute; the element stays.
5. Filter `style` attributes: only the CSS properties in the shared property list survive, and any value containing
   `url(`, `image(`, `image-set(`, `src(`, `expression(`, `javascript:`, `vbscript:`, `-moz-binding`, `behavior`,
   `@import`, a backslash escape, `<`, `>` or a control character is dropped. Values are re-serialised by
   AngleSharp.Css, so the surviving CSS is normalised (for example `#FFF` becomes `rgba(255, 255, 255, 1)`).
6. Drop `class` tokens not in the profile's class list; an emptied `class` attribute is removed.
7. Post-process: `id` values must match the profile's pattern or the attribute is removed; every kept `<a>` that has a
   `target` gets `rel="noopener noreferrer"`, replacing any `rel` Meta sent (so `rel="opener"` cannot re-enable
   `window.opener`).
8. Serialise the body's children and check `ExpectedRootSelector` against the sanitised DOM. Comments are removed.

The result is `SanitizedEmbedHtml(Html, RootElementPresent, RemovedTags)`. `RemovedTags` lists the element names that
were removed (distinct, in document order) so the provider can log them; for a healthy response it is `["script"]`.

Sanitising is idempotent: `Sanitize(Sanitize(x).Html)` produces the same `Html`. The golden tests assert this for all
five captured shapes.

## Allowlists

Derived from live captures taken on 2026-09-04 (`tests/.../Fixtures/*.json`). Tags and attributes are matched
case-insensitively; classes are matched exactly.

### Instagram (`blockquote.instagram-media`, posts and reels)

| What        | Allowed                                                                                                                                                                                                                           |
|-------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Tags        | `blockquote` `div` `a` `svg` `g` `path`                                                                                                                                                                                           |
| Attributes  | `class` `style` `href` `target` `rel` `data-instgrm-permalink` `data-instgrm-version` `data-instgrm-captioned` `width` `height` `viewBox` `version` `xmlns` `xmlns:xlink` `stroke` `stroke-width` `fill` `fill-rule` `transform` `d` |
| Classes     | `instagram-media`                                                                                                                                                                                                                 |
| URL attrs   | `href`, `data-instgrm-permalink` → `https://` on `instagram.com` or a subdomain (`www.instagram.com`)                                                                                                                             |
| `id`        | not allowed                                                                                                                                                                                                                       |

`data-instgrm-captioned` is a boolean attribute in Meta's markup; it survives as `data-instgrm-captioned=""`, which
the DOM treats identically.

### Threads (`blockquote.text-post-media`)

| What        | Allowed                                                                                                                                                             |
|-------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Tags        | `blockquote` `a` `div` `svg` `path`                                                                                                                                 |
| Attributes  | `class` `id` `style` `href` `target` `rel` `data-text-post-permalink` `data-text-post-version` `data-theme` `width` `height` `viewBox` `fill` `d` `xmlns` `aria-label` `role` |
| Classes     | `text-post-media`                                                                                                                                                   |
| URL attrs   | `href`, `data-text-post-permalink` → `https://` on `threads.com` or `threads.net` (or subdomains)                                                                   |
| `id`        | must match `^ig-tp-[A-Za-z0-9_-]{1,64}$` (Meta's `ig-tp-{post code}`)                                                                                               |

`aria-label` and `role` appear on the Threads logo SVG in the live markup (`role="img" aria-label="Threads"`). They are
inert accessibility attributes and were added to the plan's list so the placeholder stays labelled for screen readers.

### Facebook (`div.fb-post`, `div.fb-video`) — the strictest profile

| What        | Allowed                                                              |
|-------------|----------------------------------------------------------------------|
| Tags        | `div`                                                                |
| Attributes  | `id` `class` `data-href` `data-width`                                |
| Classes     | `fb-post` `fb-video`                                                 |
| URL attrs   | `data-href` → `https://` on `facebook.com` or a subdomain            |
| `id`        | must be exactly `fb-root`                                            |

`<div id="fb-root"></div>` is kept because the Facebook SDK needs it; the widget de-duplicates it per page.

### Unknown profile names

If an endpoint names a profile the sanitiser does not know (or none), the **Facebook** allowlist is applied because it
is the strictest, and the endpoint's `ExpectedRootSelector` is still checked. Since only `div` elements survive, any
markup that is not Facebook-shaped loses its root and the provider fails closed. To support a new platform, register
your own `IEmbedHtmlSanitizer` (see below) rather than reusing a built-in profile name.

### CSS properties (all profiles)

Layout and paint properties Meta's placeholder uses, with their longhands: `align-items`, `align-self`, `background`
(and its longhands), `border` (all sides, widths, styles, colours, radii), `box-shadow`, `box-sizing`, `color`,
`display`, `flex` (and `flex-basis/direction/flow/grow/shrink/wrap`), `font` (and its longhands), `gap`/`row-gap`/
`column-gap`, `height`, `justify-content`, `letter-spacing`, `line-height`, `margin` (all sides), `max-/min-height`,
`max-/min-width`, `overflow` (`-x`, `-y`, `-wrap`), `padding` (all sides), `text-align`, `text-decoration` (and its
longhands), `text-transform`, `transform`, `vertical-align`, `white-space`, `width`, `word-break`.

Deliberately absent, so the markup cannot cover or hide parts of the host page: `position`, `z-index`, `top`/`right`/
`bottom`/`left`/`inset`, `opacity`, `visibility`, `pointer-events`, `clip`/`clip-path`, `filter`, `mask*`, `content`,
`cursor`, `animation*`, `transition*`, `float`, `all`. `transform` is allowed because Meta's placeholder depends on it;
its reach is limited to the widget's own box.

## Always removed, in every profile

- Elements: `script`, `iframe`, `object`, `embed`, `style`, `link`, `meta`, `base`, `form`, `input`, `img`, `noscript`,
  `template`, `svg > foreignObject`, and anything else not on the profile's tag list. Removed elements take their
  content with them.
- Attributes: every `on*` handler, `src`, `srcdoc`, `srcset`, `ping`, `formaction`, `action`, `xlink:href`, and
  anything else not on the profile's attribute list.
- URLs: `javascript:`, `data:`, `vbscript:`, `http:`, protocol-relative (`//host/...`), relative paths, URLs with
  userinfo (`https://www.instagram.com@evil.example/`), explicit ports, and any host outside the profile's domains.
- CSS: `url()` in any property, `expression()`, `behavior`, `-moz-binding`, `@import`, `image()`/`image-set()`/`src()`,
  backslash escapes, and every property not on the list.
- HTML comments.

## Fail-closed behaviour

- If `ExpectedRootSelector` does not match the sanitised DOM, `RootElementPresent` is `false`. The provider maps this to
  `EmbedFailureKind.UnexpectedMarkup`: nothing renders on the live site, editors see "Meta returned markup this widget
  doesn't recognise", and the event is logged. The sanitised HTML is still returned so the log can show what arrived.
- Empty or whitespace input, a parsing or post-processing failure, or an invalid selector all yield
  `SanitizedEmbedHtml("", false, [])`. `Sanitize` never throws for any string input; a `null` endpoint is a
  programming error and throws `ArgumentNullException`.
- Wrong profile for the markup (for example Instagram markup through the Threads endpoint) fails closed, because the
  other profile's class and attribute names are not on the list and the root check fails.

## Sanitising runs before caching

`IEmbedProvider` sanitises the oEmbed response before it hands the result to `IEmbedResultCache`, so the cache only
ever holds clean HTML. A stale or tampered cache entry cannot re-introduce script, and upgrading the package's
allowlist takes effect for new entries as the cache turns over (purge with `CacheHelper.TouchKey("metaembeds|all")` to
force it).

## `rel="noopener noreferrer"`

Meta's anchors open the post in a new tab (`target="_blank"`). Every kept anchor with a `target` attribute gets
`rel="noopener noreferrer"`, overwriting any `rel` in the source. This severs `window.opener` so the destination cannot
navigate the host page, and withholds the referrer.

## Content Security Policy

The sanitiser removes Meta's script tags; the widget then loads the SDKs from hosts it controls. A site with a strict
CSP must allow, at minimum:

| Directive    | Hosts                                                                                      | Why                                                                    |
|--------------|--------------------------------------------------------------------------------------------|------------------------------------------------------------------------|
| `script-src` | `https://www.instagram.com` `https://www.threads.com` `https://connect.facebook.net`         | The SDK URLs the widget emits (`embed.js`, `embed.js`, `sdk.js`).      |
| `frame-src`  | `https://www.instagram.com` `https://www.threads.com` `https://www.facebook.com` `https://web.facebook.com` | The SDKs replace the placeholder with an iframe from these hosts.       |
| `style-src`  | `'unsafe-inline'` (or a nonce/hash strategy of your own)                                   | Meta's placeholder is styled with inline `style` attributes.            |

The SDKs may also pull further assets (images, fonts, scripts) from Meta CDNs such as `static.cdninstagram.com` or
`static.xx.fbcdn.net`; which ones depends on Meta and changes over time, so run your CSP in report-only mode with each
platform embedded once and add what the reports show. This list was compiled from the SDK URLs in
`MetaOEmbedEndpoints` and the behaviour of the Meta embeds in a browser; it is not something the library can enforce.

## Substituting a stricter (or different) sanitiser

`IEmbedHtmlSanitizer` is a single-method seam registered with `TryAddSingleton`, so a registration made before
`AddXperienceCommunityMetaEmbeds()` wins:

```csharp
// Program.cs
builder.Services.AddSingleton<IEmbedHtmlSanitizer, MyStricterSanitizer>();
builder.Services.AddXperienceCommunityMetaEmbeds();
```

```csharp
public sealed class MyStricterSanitizer : IEmbedHtmlSanitizer
{
    private readonly EmbedHtmlSanitizer inner = new();

    public SanitizedEmbedHtml Sanitize(string rawHtml, MetaOEmbedEndpoint endpoint)
    {
        var result = inner.Sanitize(rawHtml, endpoint);
        // Example: refuse inline styles entirely.
        if (result.Html.Contains("style=", StringComparison.OrdinalIgnoreCase))
        {
            return new SanitizedEmbedHtml(string.Empty, false, result.RemovedTags);
        }

        return result;
    }
}
```

Contract for a replacement: never throw for any string input, return `RootElementPresent = false` whenever you are not
sure the markup is what the endpoint expects, and remember that whatever you return is cached and rendered as raw HTML.
`Sanitize` must be safe to call concurrently; the default implementation pools its HtmlSanitizer instances per profile
so no per-call state is shared.

## Privacy and Meta terms

This package calls Meta's oEmbed API endpoints. When a Threads, Instagram or Facebook URL is embedded:

- The web server makes a server-side request to `graph.threads.com/oembed` (Threads),
  `graph.facebook.com/{version}/instagram_oembed` (Instagram), `graph.facebook.com/{version}/oembed_post` (Facebook
  posts) or `graph.facebook.com/{version}/oembed_video` (Facebook videos) to fetch the embed HTML. The only data sent is
  the post URL the editor entered, with its query string removed (and, if configured, your access token).
- The rendered page loads `www.threads.com/embed.js`, `www.instagram.com/embed.js` or
  `connect.facebook.net/{locale}/sdk.js` in the visitor's browser to turn the placeholder into the embed.
- No visitor or user data is collected or stored by this package. The cache holds sanitised embed markup keyed by a
  hash of the post URL.
- Frontend embed rendering is subject to [Meta's Privacy Policy](https://www.facebook.com/privacy/policy/).
- Use of Meta's endpoints and SDKs is subject to the [Meta Platform Terms](https://developers.facebook.com/terms/dfc_platform_terms/),
  [Developer Policies](https://developers.facebook.com/devpolicy/) and
  [Meta's Community Standards](https://transparency.meta.com/policies/community-standards/), together with all other
  applicable terms and policies. Consent management for the third-party scripts is the site's responsibility.

## Reporting a problem

This is a community package. If you find markup that passes the sanitiser and should not, open a private security
advisory on the GitHub repository rather than a public issue, and include the oEmbed URL and the offending fragment.
