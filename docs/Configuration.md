# Configuration reference

Every option has a working default, so the `XperienceCommunityMetaEmbeds` section is optional and the package works
with no configuration at all. This page is the reference; the [Usage guide](Usage-Guide.md) covers day-to-day use.

## appsettings.json

Every default spelled out:

```json
{
  "XperienceCommunityMetaEmbeds": {
    "Credentials": { "AppId": "", "ClientToken": "", "AccessToken": "" },
    "GraphApiVersion": "v25.0",
    "FacebookSdkLocale": "en_US",
    "ScriptMode": "Inline",
    "SuccessCacheDuration": "12:00:00",
    "NotFoundCacheDuration": "01:00:00",
    "TransientFailureCacheDuration": "00:02:00",
    "HttpTimeout": "00:00:05",
    "MaxResponseBytes": 65536
  }
}
```

| Key                             | Default    | Meaning                                                                                                                    |
|---------------------------------|------------|----------------------------------------------------------------------------------------------------------------------------|
| `Credentials.AppId`             | empty      | Meta app id. With `ClientToken`, sent as `access_token=APP_ID\|CLIENT_TOKEN`.                                               |
| `Credentials.ClientToken`       | empty      | Meta client token, see above.                                                                                              |
| `Credentials.AccessToken`       | empty      | An explicit access token, sent verbatim. Wins over `AppId` + `ClientToken`.                                                |
| `GraphApiVersion`               | `v25.0`    | Version segment for `graph.facebook.com` calls and the Facebook SDK URL. `v25.0` is what Meta's WordPress plugin pins.     |
| `FacebookSdkLocale`             | `en_US`    | Locale segment of the Facebook SDK URL; also sent as `Accept-Language` (`en-US`) on oEmbed requests.                       |
| `ScriptMode`                    | `Inline`   | `Inline`, `TagHelper` or `None`; see [Script loading](Usage-Guide.md#4-script-loading).                                     |
| `SuccessCacheDuration`          | 12 h       | Lifetime of a cached successful response.                                                                                  |
| `NotFoundCacheDuration`         | 1 h        | Lifetime of a cached "not found" / "rejected by Meta" failure. Protects the tokenless quota from a page with a broken URL. |
| `TransientFailureCacheDuration` | 2 min      | Lifetime of a cached network / 5xx / 429 / timeout / bad-JSON failure.                                                      |
| `HttpTimeout`                   | 5 s        | Timeout of a single oEmbed call.                                                                                           |
| `MaxResponseBytes`              | 65536      | Responses larger than this are rejected as transient failures.                                                             |

`GraphApiVersion` and `FacebookSdkLocale` are interpolated into outbound URLs, so they are constrained to the shapes
Meta uses (`v{major}.{minor}` and `xx_XX`). A value of any other shape is ignored in favour of the default and reported
once as a warning in the event log, so a typo cannot quietly move a call to a different host.

## Configuring in code

```csharp
// Program.cs
builder.Services.AddXperienceCommunityMetaEmbeds(builder.Configuration);                        // bind the section
builder.Services.AddXperienceCommunityMetaEmbeds(o => o.ScriptMode = EmbedScriptMode.TagHelper); // configure in code
```

Both calls are idempotent with the module that registers the package automatically. Values set in code win over the
`XperienceCommunityMetaEmbeds` section whichever order the calls are made in: the callback runs as a `PostConfigure`
action, which is after the module's configuration binder.

## Using a Meta access token

Nothing in this package needs a token. Meta opened the oEmbed endpoints to tokenless calls in June 2026 and states that
they "currently return the same technical data as the token-based version". A token buys rate-limit headroom
(Meta: "token-based access through App Review may offer higher rate limits"; the tokenless limit is documented as
"1,000 requests every hour" without saying per what) and future-proofs a site if Meta narrows tokenless access again.

1. In the [Meta App Dashboard](https://developers.facebook.com/apps/) create or open an app, add the **oEmbed Read**
   product and complete its App Review. Note the **App ID** (Settings → Basic) and the **Client token**
   (Settings → Advanced → Security).
2. Put the two values into configuration. The package sends them as the app access token `APP_ID|CLIENT_TOKEN`, exactly
   like Meta's WordPress plugin. If you already hold a user or page access token, set `AccessToken` instead; it is sent
   verbatim and wins over the pair.

```json
{
  "XperienceCommunityMetaEmbeds": {
    "Credentials": { "AppId": "1234567890", "ClientToken": "abc123…" }
  }
}
```

Because this is standard ASP.NET Core configuration, keep the secret out of `appsettings.json`: use
[user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) locally
(`dotnet user-secrets set "XperienceCommunityMetaEmbeds:Credentials:ClientToken" "…"`), and environment variables
(`XperienceCommunityMetaEmbeds__Credentials__ClientToken`) or a key vault in hosting. The same binding also works in
code:

```csharp
builder.Services.AddXperienceCommunityMetaEmbeds(o => o.Credentials.AccessToken = builder.Configuration["Meta:Token"]);
```

What changes when credentials are present: every oEmbed call carries `access_token`; the same endpoints are called;
tokenless and authenticated results are cached under different keys, so switching never serves a stale mix; the token is
redacted from every log message and editor-facing text; a rejected token (Meta error code 190) shows editors "Meta
rejected this URL as not embeddable" and logs a warning that names the credentials. The token is never stored in page
content or in the repository, which is why it is not a widget property and why there is no admin UI for it in this
version.

The authenticated route has been exercised only against fakes; no Meta app was available when the package was built.

## Cache mechanics

Two layers. The [Usage guide](Usage-Guide.md#5-widget-output-caching) covers how to switch them on and purge them; this
is what they actually do.

1. **oEmbed response cache** (`IEmbedResultCache`, default over `CMS.Helpers.IProgressiveCache`). Key:
   `metaembeds|oembed|{endpoint}|{graphVersion}|{anon|auth}|{sha256(normalisedUrl)}`, where the URL is normalised to a
   lower-case host with no `www.`, no query and no fragment, so every spelling of one post shares an entry. The request
   to Meta drops the query as well, so one cache entry always corresponds to exactly one request. Sanitised HTML is what
   gets cached; raw Meta markup never is. Progressive caching means concurrent first renders of one URL make a single
   HTTP call. Responses are cached in edit and preview mode too, so an editor refreshing does not cost a Meta call.
2. **Widget output cache.** The widget is registered with `AllowCache = true`; opt in per editable area with
   `allow-widget-output-cache`. Every render adds the dummy key `metaembeds|all` to the widget's cache dependencies.

**Multi-instance deployments.** `IProgressiveCache` is per instance, so each node fetches each URL once per 12 hours.
Rough budget against Meta's stated 1,000/hour: unique embedded URLs × nodes ÷ 12 h. A site with 500 distinct embeds on
3 nodes cold-starting at once spends about 1,500 calls in the first minutes, then about 125 per hour. If that is a
problem, replace `IEmbedResultCache` with a distributed implementation — see [Extending](Extending.md).

## Content Security Policy

A strict policy must allow:

- `script-src`: `https://www.instagram.com https://www.threads.com https://connect.facebook.net`
- `frame-src`: `https://www.instagram.com https://www.threads.com https://www.facebook.com` (the SDKs create the iframes)

For nonce-based policies, set `ScriptMode = None` and load the SDKs yourself with your nonce.
