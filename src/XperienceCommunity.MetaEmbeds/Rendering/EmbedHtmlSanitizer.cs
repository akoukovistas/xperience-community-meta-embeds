using AngleSharp;
using AngleSharp.Dom;

using Ganss.Xss;

using Microsoft.Extensions.ObjectPool;

using XperienceCommunity.MetaEmbeds.Providers.OEmbed;

namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>
/// Default <see cref="IEmbedHtmlSanitizer"/> built on HtmlSanitizer (Ganss.Xss) with one allowlist profile per
/// platform (<see cref="EmbedSanitizerProfile"/>). See <c>docs/Security.md</c> for the allowlists and the threat model.
/// </summary>
/// <remarks>
/// <para>
/// Each profile owns a small pool of <see cref="ProfileHtmlSanitizer"/> instances whose configuration is fixed at
/// construction. Pooling, rather than one shared instance, keeps the per-call <c>RemovedTags</c> bookkeeping off
/// any shared state, so <see cref="Sanitize"/> is safe to call concurrently from a singleton.
/// </para>
/// <para>
/// Never throws for any string input: a failure in parsing or post-processing yields an empty result with
/// <see cref="SanitizedEmbedHtml.RootElementPresent"/> false, which the provider treats as unexpected markup.
/// </para>
/// </remarks>
public sealed class EmbedHtmlSanitizer : IEmbedHtmlSanitizer
{
    private const int MaxPooledPerProfile = 16;
    private const string RelForNewWindow = "noopener noreferrer";

    private static readonly SanitizedEmbedHtml Empty = new(string.Empty, false, Array.Empty<string>());

    private readonly Dictionary<string, ObjectPool<ProfileHtmlSanitizer>> pools;

    /// <summary>Creates the sanitiser and pre-warms one configured instance per built-in profile.</summary>
    public EmbedHtmlSanitizer()
    {
        pools = new Dictionary<string, ObjectPool<ProfileHtmlSanitizer>>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in EmbedSanitizerProfile.All)
        {
            var pool = new DefaultObjectPool<ProfileHtmlSanitizer>(new PoolPolicy(profile), MaxPooledPerProfile);
            pool.Return(pool.Get());
            pools[profile.Name] = pool;
        }
    }

    /// <inheritdoc />
    public SanitizedEmbedHtml Sanitize(string rawHtml, MetaOEmbedEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return Empty;
        }

        var profile = EmbedSanitizerProfile.Resolve(endpoint.SanitizerProfile);
        var pool = pools[profile.Name];
        ProfileHtmlSanitizer? sanitizer = null;

        try
        {
            sanitizer = pool.Get();

            var document = sanitizer.SanitizeDom(rawHtml, string.Empty);
            var body = document.Body;
            if (body is null)
            {
                return Empty;
            }

            ApplyPostRules(body, profile);

            var html = body.ChildNodes.ToHtml(HtmlFormatter.Instance).Trim();
            var removedTags = sanitizer.RemovedTags.Count == 0
                ? Array.Empty<string>()
                : sanitizer.RemovedTags.ToArray();

            if (html.Length == 0)
            {
                return new SanitizedEmbedHtml(string.Empty, false, removedTags);
            }

            return new SanitizedEmbedHtml(html, HasRoot(body, endpoint.ExpectedRootSelector), removedTags);
        }
        catch (Exception)
        {
            // Deliberately broad: the seam has no logger and must never throw. The instance is not returned to the
            // pool because its state may be inconsistent; the pool simply creates a fresh one next time.
            sanitizer = null;
            return Empty;
        }
        finally
        {
            if (sanitizer is not null)
            {
                pool.Return(sanitizer);
            }
        }
    }

    /// <summary>
    /// Value-level rules HtmlSanitizer has no switch for: <c>id</c> values must match the profile's pattern, and every
    /// kept anchor that opens a new browsing context gets <c>rel="noopener noreferrer"</c> (replacing any existing
    /// <c>rel</c>, so a <c>rel="opener"</c> cannot re-enable <c>window.opener</c>).
    /// </summary>
    private static void ApplyPostRules(IElement body, EmbedSanitizerProfile profile)
    {
        foreach (var element in body.QuerySelectorAll("[id]").Where(e => !profile.IsAllowedId(e.GetAttribute("id"))))
        {
            element.RemoveAttribute("id");
        }

        foreach (var anchor in body.QuerySelectorAll("a[target]"))
        {
            anchor.SetAttribute("rel", RelForNewWindow);
        }
    }

    private static bool HasRoot(IElement body, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return false;
        }

        try
        {
            return body.QuerySelector(selector) is not null;
        }
        catch (DomException)
        {
            // An invalid selector can never match; fail closed.
            return false;
        }
    }

    private sealed class PoolPolicy(EmbedSanitizerProfile profile) : PooledObjectPolicy<ProfileHtmlSanitizer>
    {
        public override ProfileHtmlSanitizer Create() => new(profile);

        public override bool Return(ProfileHtmlSanitizer obj)
        {
            obj.Reset();
            return true;
        }
    }
}
