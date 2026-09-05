namespace XperienceCommunity.MetaEmbeds.Rendering;

/// <summary>Result of <see cref="IEmbedHtmlSanitizer.Sanitize"/>.</summary>
/// <param name="Html">The sanitised markup. Empty when nothing survived.</param>
/// <param name="RootElementPresent">True when the endpoint's <c>ExpectedRootSelector</c> still matches. False means fail closed.</param>
/// <param name="RemovedTags">Names of elements the sanitiser removed (for logs), e.g. <c>script</c>.</param>
public sealed record SanitizedEmbedHtml(string Html, bool RootElementPresent, IReadOnlyList<string> RemovedTags);
