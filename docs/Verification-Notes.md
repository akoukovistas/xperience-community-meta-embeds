# Verification notes

Maintainer-facing evidence about Meta's live endpoints, not user documentation. Meta changes these endpoints, so treat
everything here as a dated observation rather than a contract. The only part that is enforced automatically is the
opt-in live contract test suite (`METAEMBEDS_LIVE=1`), which re-checks the field list and the root elements; it does
not run in CI.

## 2026-09-04 — first release

Checked against the live API from a residential connection, with `curl`, no token:

- All four endpoints return HTTP 200 and exactly six top-level fields: `html`, `provider_name`, `provider_url`, `type`,
  `version`, `width`. Twenty back-to-back tokenless Instagram calls all returned 200.
- Deprecated fields `author_name`, `author_url`, `thumbnail_url`, `thumbnail_width`, `thumbnail_height` are absent
  (removed by Meta on 2025-11-03), as are `author_id`, `media_id` and `title`. The package reads only `html`, `width`,
  `type` and `provider_name`, and ignores unknown members.
- `graph.instagram.com/instagram_oembed` needs a token (400, code 190); the legacy `api.instagram.com/oembed` returns an
  HTML page. Neither is used.
- Instagram profile URLs (`instagram.com/{user}`) return 400 / subcode 2207047 — every one tested. This is why the URL
  matcher rejects them outright instead of spending a request on them.
- `/reels/{code}` (plural) is rejected by Meta. `/p/`, `/reel/`, `instagr.am`, `http://` and `?igsh=` variants are all
  accepted.
- Facebook responses embed an SDK tag whose locale follows `Accept-Language` and whose version (`v26.0`) is higher than
  the requested `v25.0`. The package therefore never echoes Meta's own tag.
- The captured responses are the test fixtures in `tests/XperienceCommunity.MetaEmbeds.Tests/Fixtures/`.

## Re-checking

```bash
# From the repository root. Needs network access and hits Meta's live endpoints.
METAEMBEDS_LIVE=1 dotnet test --filter "TestCategory=Live"
```

If a live test starts failing, record the new observation as a dated section above rather than editing the old one —
the point of this file is to show when a behaviour changed.
