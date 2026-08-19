using System.Text.Json;

namespace StoryChief.Xperience.Tests;

public sealed class StoryChiefFieldMapperTests
{
    [Test]
    public void MapConvertsConfiguredScalarNestedAndDateValues()
    {
        using var payload = JsonDocument.Parse("""
            {
              "title": "A mapped article",
              "published_at": "2026-08-19T08:30:00+02:00",
              "custom_fields": {
                "reading_time": 7,
                "sponsored": true
              },
              "tags": ["Kentico", "StoryChief"]
            }
            """);
        var mappings = new Dictionary<string, StoryChiefFieldMapping>
        {
            ["title"] = new("ArticleTitle"),
            ["published_at"] = new("ArticlePublishedAt", StoryChiefFieldValueKind.DateTime),
            ["custom_fields.reading_time"] = new("ArticleReadingTime", StoryChiefFieldValueKind.Integer),
            ["custom_fields.sponsored"] = new("ArticleSponsored", StoryChiefFieldValueKind.Boolean),
            ["tags"] = new("ArticleTagsJson", StoryChiefFieldValueKind.Json),
        };

        var result = StoryChiefFieldMapper.Map(payload.RootElement, mappings);

        Assert.Multiple(() =>
        {
            Assert.That(result["ArticleTitle"], Is.EqualTo("A mapped article"));
            Assert.That(result["ArticlePublishedAt"], Is.EqualTo(new DateTime(2026, 8, 19, 6, 30, 0, DateTimeKind.Utc)));
            Assert.That(result["ArticleReadingTime"], Is.EqualTo(7));
            Assert.That(result["ArticleSponsored"], Is.EqualTo(true));
            Assert.That(result["ArticleTagsJson"], Is.EqualTo("[\"Kentico\", \"StoryChief\"]"));
        });
    }

    [Test]
    public void MapSkipsMissingAndNullValues()
    {
        using var payload = JsonDocument.Parse("""
            {"title":"Present","excerpt":null}
            """);
        var mappings = new Dictionary<string, StoryChiefFieldMapping>
        {
            ["title"] = new("ArticleTitle"),
            ["excerpt"] = new("ArticleExcerpt"),
            ["seo_description"] = new("ArticleSeoDescription"),
        };

        var result = StoryChiefFieldMapper.Map(payload.RootElement, mappings);

        Assert.That(result, Is.EqualTo(new Dictionary<string, object>
        {
            ["ArticleTitle"] = "Present",
        }));
    }

    [Test]
    public void MapRejectsInvalidExplicitConversion()
    {
        using var payload = JsonDocument.Parse("""
            {"published_at":"not-a-date"}
            """);
        var mappings = new Dictionary<string, StoryChiefFieldMapping>
        {
            ["published_at"] = new("ArticlePublishedAt", StoryChiefFieldValueKind.DateTime),
        };

        var exception = Assert.Throws<JsonException>(() =>
            StoryChiefFieldMapper.Map(payload.RootElement, mappings))!;

        Assert.That(exception.Message, Does.Contain("published_at"));
    }
}
