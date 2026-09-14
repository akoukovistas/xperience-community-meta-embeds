# Meta embeds for Xperience by Kentico

[![Community support](https://img.shields.io/badge/Community_support-grey?labelColor=orange)](#support)
[![CI: Build and Test](https://github.com/akoukovistas/xperience-community-meta-embeds/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/akoukovistas/xperience-community-meta-embeds/actions/workflows/ci.yml)
[![NuGet Package](https://img.shields.io/nuget/v/XperienceCommunity.MetaEmbeds.svg)](https://www.nuget.org/packages/XperienceCommunity.MetaEmbeds)

Paste a public Threads, Instagram or Facebook post URL into a Page Builder widget. Tokenless, cached, sanitised.
No Meta app, no App Review, no configuration.

This is a community package (`XperienceCommunity.*`), not a Kentico product. It is maintained on a best-effort basis;
see [Support](#support).

![The Meta embed properties dialog: Post URL on top, the Appearance category with Layout, Hide caption and Theme, and the collapsed Advanced category](https://raw.githubusercontent.com/akoukovistas/xperience-community-meta-embeds/main/images/widget-configuration.png)

## What it does

- One **Meta embed** Page Builder widget for public Threads posts, Instagram posts and reels, and Facebook posts and
  reels.
- Calls Meta's **tokenless** oEmbed endpoints server-side, so no Meta app, App Review or credentials are needed. A
  token is supported and buys rate-limit headroom, but nothing requires one.
- **Sanitises** Meta's markup against a per-platform allowlist before it is cached or rendered, and fails closed.
- **Caches** responses (12 h success / 1 h not-found / 2 min transient by default) and deduplicates Meta's SDK script
  tags per request.
- **Never throws and never renders an error.** Editors see a specific message in Page Builder; the live site renders
  nothing.

| Platform  | URL shapes accepted                                                    |
|-----------|------------------------------------------------------------------------|
| Threads   | `threads.com/@{user}/post/{code}`, `threads.com/t/{code}` (also `.net`) |
| Instagram | `instagram.com/p/{code}`, `instagram.com/reel/{code}`                   |
| Facebook  | `facebook.com/{user}/posts/{id}`, `facebook.com/reel/{id}`              |

`http://`, a missing `www.`, trailing slashes and query strings (`?igsh=…`, `?hl=en`) are all accepted; the query is
dropped before anything is sent to Meta. Instagram **profile** URLs are not accepted — Meta's endpoint rejects every one
of them, so the widget says so immediately instead of spending a request. The full list, including the shapes that are
deliberately excluded, is in the [Usage guide](docs/Usage-Guide.md#2-add-the-widget-to-a-page).

## Library version matrix

| Xperience by Kentico | Library |
|----------------------|---------|
| `>= 31.8.0`          | `1.0.0` |

The package targets `net8.0` and references `Kentico.Xperience.WebApp`. Monthly Xperience Refreshes are the support
expectation; the floor is the Refresh the package was built and verified against.

## Installation

```powershell
dotnet add package XperienceCommunity.MetaEmbeds
```

That is the whole setup. The package ships an Xperience module that registers its services during application start
with `TryAdd` semantics and binds `MetaEmbedsOptions` from the `XperienceCommunityMetaEmbeds` section if one exists —
no `Program.cs` change, no `appsettings.json` section, no Meta credentials.

## Quick start

1. Add **Meta embed** to a Page Builder editable area.
2. Open **Configure widget** and paste a post URL into **Post URL**.
3. Apply.

Optionally pick a **Layout**, tick **Hide caption** (Instagram) or switch **Theme** to dark (Threads). Style the result
through the `meta-embed--*` classes on the wrapper; the package ships no CSS of its own.

To configure anything in code instead of `appsettings.json`:

```csharp
// Program.cs — optional; values set here win over the configuration section.
builder.Services.AddXperienceCommunityMetaEmbeds(o => o.ScriptMode = EmbedScriptMode.TagHelper);
```

## Documentation

| Page                                             | What is in it                                                                            |
|--------------------------------------------------|------------------------------------------------------------------------------------------|
| [Usage guide](docs/Usage-Guide.md)               | Adding the widget, appearance options, CSS hooks, editor messages, script modes, purging |
| [Configuration](docs/Configuration.md)           | Every option and default, using a Meta access token, cache mechanics, CSP hosts           |
| [Security](docs/Security.md)                     | Threat model, sanitiser allowlists, privacy and Meta's terms                              |
| [Extending](docs/Extending.md)                   | The public seams and how to replace them                                                  |
| [Contributing](CONTRIBUTING.md)                  | How to propose a change, build and test                                                   |
| [Verification notes](docs/Verification-Notes.md) | Dated observations of Meta's live endpoints                                               |
| [Changelog](CHANGELOG.md)                        | What changed, per version                                                                 |

## Security

Meta's oEmbed `html` is treated as untrusted input: it is reduced to a per-platform allowlist of tags and attributes
with [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer) before it is cached or rendered, and if the platform's
expected root element is missing afterwards, nothing renders at all. Outbound traffic goes to four fixed Meta hosts,
with no cookies, no redirects followed, a 5 s timeout and a 64 KB body cap.

The allowlists and the threat model are in [docs/Security.md](docs/Security.md). To report a vulnerability, see
[SECURITY.md](SECURITY.md) — please do not open a public issue.

## Contributing

Issues and pull requests are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md); the full local setup, build and
manual verification checklist is in [docs/Contributing-Setup.md](docs/Contributing-Setup.md).

## License

Distributed under the MIT License. See [`LICENSE.md`](LICENSE.md).

## Support

This is a community project maintained by one person in their spare time. Support is **best-effort**: there is no SLA,
no bug-fix policy and no Kentico involvement, so it falls outside
[Kentico's support policies](https://github.com/Kentico/.github/blob/main/SUPPORT.md) entirely. Please report bugs
through [GitHub issues](https://github.com/akoukovistas/xperience-community-meta-embeds/issues), and security problems
through [SECURITY.md](SECURITY.md). Xperience by Kentico itself is supported by Kentico under its own terms.
