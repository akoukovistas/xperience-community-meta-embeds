# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

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
