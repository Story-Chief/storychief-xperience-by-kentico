using System.Text.Json;

namespace StoryChief.Xperience.Tests;

public sealed class StoryChiefLanguageResolverTests
{
    [Test]
    public void UsesConfiguredLanguageWhenMappingsAreEmpty()
    {
        var options = new StoryChiefPageOptions { LanguageName = "en-US" };
        using var story = JsonDocument.Parse("""{"language":"nl"}""");

        string languageName = StoryChiefLanguageResolver.Resolve(story.RootElement, options);

        Assert.That(languageName, Is.EqualTo("en-US"));
    }

    [Test]
    public void ResolvesConfiguredLanguageMappingCaseInsensitively()
    {
        var options = new StoryChiefPageOptions { LanguageName = "en" };
        options.MapLanguage("nl", "nl-BE");
        using var story = JsonDocument.Parse("""{"language":"NL"}""");

        string languageName = StoryChiefLanguageResolver.Resolve(story.RootElement, options);

        Assert.That(languageName, Is.EqualTo("nl-BE"));
    }

    [Test]
    public void UsesConfiguredLanguageWhenPayloadLanguageIsMissing()
    {
        var options = new StoryChiefPageOptions { LanguageName = "en" };
        options.MapLanguage("nl", "nl-BE");
        using var story = JsonDocument.Parse("{}");

        string languageName = StoryChiefLanguageResolver.Resolve(story.RootElement, options);

        Assert.That(languageName, Is.EqualTo("en"));
    }

    [Test]
    public void RejectsUnmappedPayloadLanguageWhenMappingsAreConfigured()
    {
        var options = new StoryChiefPageOptions { LanguageName = "en" };
        options.MapLanguage("en", "en-US");
        using var story = JsonDocument.Parse("""{"language":"nl"}""");

        var exception = Assert.Throws<StoryChiefPublisherNotConfiguredException>(
            () => StoryChiefLanguageResolver.Resolve(story.RootElement, options));

        Assert.That(exception!.Message, Does.Contain("'nl'"));
    }

    [Test]
    public void RejectsNonStringPayloadLanguageWhenMappingsAreConfigured()
    {
        var options = new StoryChiefPageOptions { LanguageName = "en" };
        options.MapLanguage("en", "en-US");
        using var story = JsonDocument.Parse("""{"language":42}""");

        Assert.Throws<JsonException>(() => StoryChiefLanguageResolver.Resolve(story.RootElement, options));
    }
}
