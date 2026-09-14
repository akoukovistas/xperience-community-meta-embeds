# Extending

Every seam is a public interface registered with `TryAdd`, so a registration placed before `AddKentico()` wins, and a
`Replace` after it wins too.

```csharp
// Stricter or different sanitiser
builder.Services.Replace(ServiceDescriptor.Singleton<IEmbedHtmlSanitizer, MyStricterSanitizer>());

// Distributed response cache instead of the per-instance IProgressiveCache
builder.Services.Replace(ServiceDescriptor.Singleton<IEmbedResultCache, MyRedisEmbedResultCache>());

// Another source type (this is how a future Feeds package plugs in)
builder.Services.AddSingleton<IEmbedProvider, MyFeedProvider>();

// Different or additional oEmbed endpoints
builder.Services.Replace(ServiceDescriptor.Singleton<IMetaOEmbedEndpointRegistry>(
    _ => new MetaOEmbedEndpointRegistry([.. MetaOEmbedEndpoints.Default, MyEndpoint])));
```

## The seams

| Interface                      | Responsibility                                                                     |
|--------------------------------|------------------------------------------------------------------------------------|
| `IEmbedResolver`               | The single entry point. Picks a provider for the request and never throws.          |
| `IEmbedProvider`               | Turns a request into markup. `MetaOEmbedProvider` is the one built in.              |
| `IEmbedUrlMatcher`             | Validates and normalises the URL an editor typed, and gives it its cache form.      |
| `IMetaOEmbedEndpointRegistry`  | The endpoint table: URL patterns, endpoint URL, SDK URL, expected root, profile.     |
| `IEmbedHtmlSanitizer`          | Reduces Meta's markup to an allowlist. See [Security](Security.md).                 |
| `IEmbedResultCache`            | Where resolved results live and for how long.                                       |
| `IEmbedScriptRegistry`         | Per-request deduplication of the SDK script tags.                                   |

`IEmbedResolver` is what the widget calls, and your own components can call it too — see
[Rendering an embed outside the widget](Usage-Guide.md#8-rendering-an-embed-outside-the-widget).

## A note on replacing the sanitiser

The sanitiser is the package's security boundary. If you replace it, read
[Substituting a stricter (or different) sanitiser](Security.md#substituting-a-stricter-or-different-sanitiser) first:
it lists what the built-in one guarantees, so you can tell whether yours still holds those guarantees.
