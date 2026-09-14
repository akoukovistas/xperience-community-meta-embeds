# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- Instagram profile URLs (`instagram.com/{user}`) are no longer matched. Meta's tokenless endpoint rejects every one of
  them, so they are now refused before the network call with a message that says to paste a post or reel link.
- The oEmbed request to Meta drops the URL's query string, matching the cache key. Two URLs differing only in their
  query (`?hl=de` / `?hl=en`) were previously two requests sharing one cache entry.
- Options configured in code now win over the `XperienceCommunityMetaEmbeds` section whichever order the registrations
  happen in, which is what the documentation already described.
- `GraphApiVersion` and `FacebookSdkLocale` are validated before being interpolated into a URL; a malformed value
  falls back to the package default and is logged once.
- Failures returned by a provider no longer carry the `Exception` object, which was being held in the cache for the
  lifetime of the entry. The exception still reaches the event log in full, and the failure names its type.
- The README is now an overview and an index. The configuration reference, access token guide, cache mechanics and CSP
  hosts moved to `docs/Configuration.md`, the seams to `docs/Extending.md`, privacy and Meta's terms into
  `docs/Security.md`, the dated live-API observations to `docs/Verification-Notes.md`, and the CSS hooks and starter
  stylesheet into `docs/Usage-Guide.md`.
- `AngleSharp` (1.7.1) and `AngleSharp.Css` (1.0.1) are declared dependencies of the package instead of arriving
  transitively through HtmlSanitizer; the sanitiser compiles against both. The test project declares `AngleSharp` for
  the same reason. Same versions as before, so no resolved dependency changes.

### Added

- Root `SECURITY.md`, `CONTRIBUTING.md` and `CODE_OF_CONDUCT.md`, so GitHub surfaces the security policy and the
  contributing link where people look for them.
- `.github/dependabot.yml` covering the `nuget` and `github-actions` ecosystems monthly.
- `WidgetViewTests`, which renders `_MetaEmbedWidget.cshtml` through the real Razor view engine - the last piece of
  the package that had no test coverage.

### Removed

- `MetaEmbedsConstants.UserAgent`, which duplicated the User-Agent the `HttpClient` actually sends, and
  `EmbedSourceTypes.IsKnown`, whose only caller was its own test. Both were public; neither has shipped.
- The unreferenced `images/logo.png`.

## [1.0.0] - 2026-09-06

First release. Built and verified against Xperience by Kentico 31.8.3.

### Added

- Initial implementation: one "Meta embed" Page Builder widget covering Threads posts, Instagram posts/reels and
  Facebook posts/reels through Meta's tokenless oEmbed endpoints.
- `MetaEmbedWidget` (identifier `XperienceCommunity.MetaEmbeds.Embed`, `AllowCache = true`) with a "Post URL" field
  (required, absolute URL, 2048 characters) and a "Source type" dropdown in a collapsed "Advanced" category. Localised
  editor messages for every failure kind; the live site renders nothing on failure and the widget never throws.
- Edit / read-only / preview detection with a click-blocking overlay over the embed in the Page Builder.
- Per-request SDK script deduplication (`IEmbedScriptRegistry` backed by `HttpContext.Items`) and the three
  `ScriptMode`s: `Inline` (default), `TagHelper` (`<meta-embeds-scripts />`), `None`. Facebook's `fb-root` div is
  stripped from Meta's markup and rendered once per request by the first Facebook embed.
- Widget output cache dependency on the dummy key `metaembeds|all`, so `CacheHelper.TouchKey("metaembeds|all")`
  purges cached widget output together with cached oEmbed responses.
- Zero-configuration module (`MetaEmbedsModule`) registering the services and binding `MetaEmbedsOptions` from the
  `XperienceCommunityMetaEmbeds` section; explicit `AddXperienceCommunityMetaEmbeds` overloads remain optional.
- English resources (`xperiencecommunity.metaembeds.*`) registered for both the `Builder` and `Server` localization
  targets.
- README, usage guide, contributing guide, GitHub Actions CI (Ubuntu + Windows), issue templates, package icon and logo.
- Appearance options in an expanded "Appearance" category of the widget dialog: **Layout** (natural / centered /
  fluid), **Hide caption** (Instagram, `hidecaption=true`, cached as its own variant) and **Theme** (Threads,
  `data-theme="dark"`). Stable CSS hooks on the wrapper (`meta-embed--{platform}`, `meta-embed--layout-{layout}`,
  `meta-embed--theme-dark`, `meta-embed--no-caption`) and a starter stylesheet in the README.
- `EmbedRequest.Parameters` (provider-specific request parameters) and an optional `Variant` segment on
  `EmbedCacheKey`, so parameters that change Meta's response are cached separately.
- README section on using a Meta access token: where to get one, how to keep it out of source, what changes when it is set.

[Unreleased]: https://github.com/akoukovistas/xperience-community-meta-embeds/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/akoukovistas/xperience-community-meta-embeds/releases/tag/v1.0.0
