# Contributing

Thanks for taking the time. This is a community project maintained in spare time, so the most useful contributions are
small, focused and come with a test.

## Before you open a pull request

- **Open an issue first** for anything beyond a typo or an obvious bug fix, so we can agree on the approach before you
  spend time on it.
- **Keep the public seams stable.** `IEmbedResolver`, `IEmbedProvider`, `IEmbedUrlMatcher`, `IEmbedHtmlSanitizer`,
  `IEmbedResultCache` and `IEmbedScriptRegistry` are the extension points consumers build on.
- **Add or update tests.** The sanitiser and URL matcher in particular are pinned by golden tests; a behaviour change
  should show up as a test diff.
- **Update the docs and `CHANGELOG.md`** under `## [Unreleased]` when behaviour changes.

## Building and testing

Full prerequisites, local build steps and how to run the opt-in live contract tests are in
[docs/Contributing-Setup.md](docs/Contributing-Setup.md). The short version:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --filter "TestCategory!=Live"
```

CI runs the same three commands on Ubuntu and Windows, with `dotnet restore --locked-mode`. If you add or change a
package reference, commit the regenerated `packages.lock.json` files (`dotnet restore --force-evaluate`) or CI will
fail.

## Reporting bugs and security issues

Bugs go in [GitHub issues](https://github.com/akoukovistas/xperience-community-meta-embeds/issues). Security problems
do **not** - see [SECURITY.md](SECURITY.md).

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE.md), and to abide by the
[Code of Conduct](CODE_OF_CONDUCT.md).
