# Contributing setup

Thanks for helping. This page covers the tooling and workflow for **this** repository. It is a community project
maintained on a best-effort basis; issues and pull requests are welcome, response times are not guaranteed.

## Required software

### .NET SDK

- .NET SDK **10.0.100 or newer** to open the `.slnx` solution and build (`global.json` pins 8.0.100 with
  `rollForward: latestMajor`, so any newer SDK is picked up).
- The .NET **8.0 runtime**: the library and the tests target `net8.0`, because that is what Xperience by Kentico 31.x
  ships against.

<https://dotnet.microsoft.com/en-us/download>

No Node.js is needed: the package has no admin UI client components.

### Editor

VS Code, Visual Studio 2022 17.13+ or Rider (anything that opens `.slnx` and honours `.editorconfig`).

### Database (only for dogfooding)

The unit tests need no database and no running site. To try the widget for real you need an Xperience by Kentico
31.8.x site with a SQL Server 2019+ database; see the Xperience documentation for creating one.

## Build and test

```powershell
dotnet restore --locked-mode      # lock files are committed; restore must not change them
dotnet build --configuration Release --no-restore
dotnet test  --configuration Release --no-build --filter "TestCategory!=Live"
```

Tests are NUnit 3.14 (the version `Kentico.Xperience.Core.Tests` 31.8.3 is compiled against) with NSubstitute.

### Live contract tests

`LiveContractTests` (category `Live`) call Meta's four real oEmbed endpoints without a token and assert the response
shape (the six top-level fields, non-empty `html`, deprecated `author_*` / `thumbnail_*` fields absent, expected root
element present). They are the drift alarm for Meta changing markup or re-requiring tokens. They are skipped unless
you opt in:

```powershell
$env:METAEMBEDS_LIVE = "1"
dotnet test --filter "TestCategory=Live"
```

Do not add them to CI: they depend on the network, on Meta's availability and on the fixture posts still existing.

### Fixtures

`tests/XperienceCommunity.MetaEmbeds.Tests/Fixtures/*.json` are oEmbed responses captured on 2026-09-04 (tokenless,
`Accept-Language: en-US`). When you re-capture them, keep the capture date in `TestFixtures.cs` and in the README's
verification notes in sync, and re-run the sanitiser snapshot tests.

### Dependencies

Package versions are managed centrally in `Directory.Packages.props`. When you change a version run
`dotnet restore --force-evaluate` to regenerate the lock files and commit them. `Kentico.Xperience.Core.Tests` must
stay on exactly the same version as `Kentico.Xperience.WebApp`.

## Try the widget in a site

1. Reference the project from an Xperience 31.8.x web project:

   ```xml
   <ProjectReference Include="..\xperience-community-meta-embeds\src\XperienceCommunity.MetaEmbeds\XperienceCommunity.MetaEmbeds.csproj" />
   ```

   or `dotnet pack` the library and add the `.nupkg` from a local feed.

2. Start the site. Nothing else is required; the module registers the services and the widget shows up in Page Builder.

3. Manual checklist per release (there is no automated UI test):

   - each failure row in the [Usage guide](Usage-Guide.md#3-what-editors-see-when-something-is-wrong) shows its message
     in edit mode and renders nothing on the live site;
   - a Threads, an Instagram and a Facebook URL render on the live site and hydrate (the SDK turns the placeholder into
     the real card);
   - two embeds of the same platform on one page load the SDK once (`Inline`), and once via `<meta-embeds-scripts />`
     (`TagHelper`);
   - widget output caching on plus `TagHelper` mode; `CacheHelper.TouchKey("metaembeds|all")` re-renders;
   - the widget dialog shows **Post URL** on top, an expanded **Appearance** category (Layout, Hide caption, Theme) and
     a collapsed **Advanced** category containing **Source type** with the single option *Single post*, all labels
     localised (no raw `{$…$}` keys).

## Releasing to nuget.org

Releases are published by the `Release: Publish to NuGet` GitHub Actions workflow (`.github/workflows/release.yml`).
The .NET SDK does the packing and pushing; the standalone `nuget.exe` is not used anywhere.

One-time setup:

1. Sign in to <https://www.nuget.org/> and create an API key under **API Keys**: scope *Push new packages and package
   versions*, glob pattern `XperienceCommunity.MetaEmbeds*`, the shortest expiry you are comfortable renewing.
2. Add it to the GitHub repository as the secret **`NUGET_API_KEY`** (Settings → Secrets and variables → Actions).

Per release:

1. Set `<VersionPrefix>` in `Directory.Build.props` (and `<VersionSuffix>` for pre-releases, e.g. `preview.1`).
2. Move the **Unreleased** entries in `CHANGELOG.md` under a new `## [x.y.z] - yyyy-mm-dd` heading and update the
   version matrix in the README if the Xperience floor changed.
3. Commit, push, wait for CI to pass.
4. Create a GitHub release with tag **`vx.y.z`** (the workflow refuses a tag that does not match the version). Publishing
   the release restores in locked mode, builds, tests, packs, attaches the `.nupkg` and `.snupkg` to the release and
   pushes both to nuget.org. Indexing on nuget.org takes a few minutes; the README badge updates after that.

To rehearse without publishing, run the workflow manually from the Actions tab with **dry run** ticked; it builds and
packs and uploads the package as a workflow artifact. Publishing by hand works too:

```powershell
dotnet pack src/XperienceCommunity.MetaEmbeds/XperienceCommunity.MetaEmbeds.csproj -c Release -o artifacts
dotnet nuget push artifacts/XperienceCommunity.MetaEmbeds.<version>.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json
```

A pushed version can never be replaced on nuget.org, only unlisted, so fix mistakes with a new patch version.

## Development workflow

1. Create a branch with one of these prefixes: `feat/` for new functionality, `refactor/` for restructuring,
   `fix/` for bug fixes, `docs/` for documentation only.
2. Keep the contracts in `Providers/`, `Rendering/` and `Caching/` stable; they are the public extension surface.
   If a change is unavoidable, call it out in the pull request.
3. Run `dotnet format` and make sure `dotnet build` has no new warnings (nullable warnings are errors).
4. Add or update tests. Pure unit tests are preferred; use `Kentico.Xperience.Core.Tests` (`CMS.Tests.UnitTests`) only
   when a test genuinely needs Xperience infrastructure.
5. Update `CHANGELOG.md` under **Unreleased**.
6. Commit with a message following [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/#summary)
   where practical, and open a pull request. Describe the scope, include screenshots for anything editors see, and say
   whether a configuration change is needed.

## Repository layout

```text
src/XperienceCommunity.MetaEmbeds/         Razor class library (widget, view, resources, module, services)
  Components/Widgets/MetaEmbedWidget/      MetaEmbedWidget.cs, *Properties.cs, *ViewModel.cs, _MetaEmbedWidget.cshtml
  Providers/                               IEmbedProvider / IEmbedResolver seam, request/result types
  Providers/OEmbed/                        Meta oEmbed provider, endpoint table, URL matcher
  Rendering/                               sanitiser, script registry, <meta-embeds-scripts /> tag helper
  Caching/                                 IEmbedResultCache over IProgressiveCache, cache keys and policy
  Resources/                               MetaEmbedsResources.resx (+ registration)
tests/XperienceCommunity.MetaEmbeds.Tests/ NUnit tests and captured fixtures
docs/                                      Usage-Guide.md, Contributing-Setup.md, Security.md
```
