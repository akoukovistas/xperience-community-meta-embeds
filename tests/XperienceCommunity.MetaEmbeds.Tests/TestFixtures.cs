using System.Text.Json;

namespace XperienceCommunity.MetaEmbeds.Tests;

/// <summary>
/// Access to the oEmbed responses captured live on 2026-09-04 (tokenless, <c>Accept-Language: en-US</c>) in <c>Fixtures/</c>.
/// </summary>
public static class TestFixtures
{
    public const string InstagramPost = "instagram-post";
    public const string InstagramReel = "instagram-reel";
    public const string ThreadsPost = "threads-post";
    public const string FacebookPost = "facebook-post";
    public const string FacebookVideo = "facebook-video";
    public const string ErrorMediaNotFound = "error-media-not-found";
    public const string ErrorInvalidUrl = "error-invalid-url";
    public const string ErrorInvalidDimensions = "error-invalid-dimensions";

    /// <summary>The URL each success fixture was captured for.</summary>
    public static readonly IReadOnlyDictionary<string, string> SourceUrls = new Dictionary<string, string>
    {
        [InstagramPost] = "https://www.instagram.com/p/fA9uwTtkSN/",
        [InstagramReel] = "https://www.instagram.com/reel/DTxk5orCKEv/",
        [ThreadsPost] = "https://www.threads.com/@threads/post/DWjTI0cgH5O/",
        [FacebookPost] = "https://www.facebook.com/zuck/posts/10102577175875681",
        [FacebookVideo] = "https://www.facebook.com/reel/3305054673010377",
    };

    public static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json");

    /// <summary>Raw JSON body of a fixture.</summary>
    public static string ReadJson(string name) => File.ReadAllText(Path(name));

    /// <summary>The <c>html</c> field of a success fixture.</summary>
    public static string ReadHtml(string name)
    {
        using var document = JsonDocument.Parse(ReadJson(name));
        return document.RootElement.GetProperty("html").GetString()
            ?? throw new InvalidOperationException($"Fixture {name} has no html field.");
    }
}
