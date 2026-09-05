using System.Reflection;

using Kentico.Xperience.Admin.Base.FormAnnotations;

using Newtonsoft.Json;

using NUnit.Framework;

using XperienceCommunity.MetaEmbeds.Providers;
using XperienceCommunity.MetaEmbeds.Widgets;

namespace XperienceCommunity.MetaEmbeds.Tests;

/// <summary>
/// Page Builder stores widget configuration as JSON and deserialises it with Newtonsoft.Json onto the properties class,
/// so a configuration saved before a property existed must fall back to the C# initializer.
/// </summary>
[TestFixture]
public class WidgetPropertiesDefaultsTests
{
    [Test]
    public void Deserialize_WithoutSourceType_DefaultsToPost()
    {
        var properties = JsonConvert.DeserializeObject<MetaEmbedWidgetProperties>(
            "{\"url\":\"https://www.instagram.com/p/fA9uwTtkSN/\"}");

        Assert.That(properties, Is.Not.Null);
        Assert.That(properties!.Url, Is.EqualTo("https://www.instagram.com/p/fA9uwTtkSN/"));
        Assert.That(properties.SourceType, Is.EqualTo(EmbedSourceTypes.Post));
    }

    [Test]
    public void Deserialize_EmptyObject_HasSafeDefaults()
    {
        var properties = JsonConvert.DeserializeObject<MetaEmbedWidgetProperties>("{}");

        Assert.That(properties, Is.Not.Null);
        Assert.That(properties!.Url, Is.EqualTo(string.Empty));
        Assert.That(properties.SourceType, Is.EqualTo(EmbedSourceTypes.Post));
    }

    [Test]
    public void Deserialize_ExplicitNullSourceType_NormalizesToPost()
    {
        var properties = JsonConvert.DeserializeObject<MetaEmbedWidgetProperties>(
            "{\"url\":\"https://www.threads.com/t/DWjTI0cgH5O/\",\"sourceType\":null}");

        Assert.That(properties, Is.Not.Null);
        Assert.That(properties!.SourceType, Is.Null, "Newtonsoft overrides the initializer with an explicit null");
        Assert.That(EmbedSourceTypes.Normalize(properties.SourceType), Is.EqualTo(EmbedSourceTypes.Post));
    }

    [Test]
    public void Deserialize_UnknownSourceType_IsNormalizedButNotKnown()
    {
        var properties = JsonConvert.DeserializeObject<MetaEmbedWidgetProperties>(
            "{\"url\":\"https://www.instagram.com/p/fA9uwTtkSN/\",\"sourceType\":\" FEED \"}");

        Assert.That(properties, Is.Not.Null);
        Assert.That(EmbedSourceTypes.Normalize(properties!.SourceType), Is.EqualTo("feed"));
        Assert.That(EmbedSourceTypes.IsKnown(properties.SourceType), Is.False);
    }

    [Test]
    public void Url_IsRequiredAbsoluteUrlOfAtMost2048Characters()
    {
        var url = typeof(MetaEmbedWidgetProperties).GetProperty(nameof(MetaEmbedWidgetProperties.Url))!;

        var input = url.GetCustomAttribute<TextInputComponentAttribute>();
        var urlRule = url.GetCustomAttribute<UrlValidationRuleAttribute>();
        var maxLength = url.GetCustomAttribute<MaxLengthValidationRuleAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(input, Is.Not.Null);
            Assert.That(input!.Label, Does.StartWith("{$xperiencecommunity.metaembeds.properties.url."));
            Assert.That(url.GetCustomAttribute<RequiredValidationRuleAttribute>(), Is.Not.Null);
            Assert.That(urlRule, Is.Not.Null);
            Assert.That(urlRule!.AllowRelativeUrl, Is.False);
            Assert.That(maxLength, Is.Not.Null);
            Assert.That(maxLength!.MaxLength, Is.EqualTo(2048));
        });
    }

    [Test]
    public void SourceType_IsSingleOptionDropDownInsideCollapsedAdvancedCategory()
    {
        var type = typeof(MetaEmbedWidgetProperties);
        var sourceType = type.GetProperty(nameof(MetaEmbedWidgetProperties.SourceType))!;
        var url = type.GetProperty(nameof(MetaEmbedWidgetProperties.Url))!;

        var category = type.GetCustomAttributes<FormCategoryAttribute>().Single();
        var dropDown = sourceType.GetCustomAttribute<DropDownComponentAttribute>();
        var urlInput = url.GetCustomAttribute<TextInputComponentAttribute>()!;

        Assert.Multiple(() =>
        {
            Assert.That(category.Collapsible, Is.True);
            Assert.That(category.IsCollapsed, Is.True);
            Assert.That(category.Label, Is.EqualTo("{$xperiencecommunity.metaembeds.properties.category.advanced$}"));
            Assert.That(dropDown, Is.Not.Null);
            Assert.That(dropDown!.Options, Does.StartWith(EmbedSourceTypes.Post + ";"));
            Assert.That(dropDown.Options, Does.Not.Contain('\n'), "exactly one option");
            // Categories own the properties ordered after them: Url must precede the category, SourceType follow it.
            Assert.That(urlInput.Order, Is.LessThan(category.Order));
            Assert.That(dropDown.Order, Is.GreaterThan(category.Order));
        });
    }
}
