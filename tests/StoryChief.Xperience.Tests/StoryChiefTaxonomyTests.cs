using System.Text.Json;

namespace StoryChief.Xperience.Tests;

public sealed class StoryChiefTaxonomyTests
{
    [Test]
    public void ParserReadsWrappedStoryChiefTags()
    {
        using var payload = JsonDocument.Parse("""
            {
              "tags": {
                "data": [
                  {"storychief_id":2769118967,"name":"Go green","slug":"go-green"},
                  {"storychief_id":"42","name":"Kentico","slug":"kentico"}
                ]
              }
            }
            """);

        var terms = StoryChiefTaxonomyParser.Parse(payload.RootElement, "tags")
            ?? throw new AssertionException("Expected StoryChief tags to be parsed.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(terms, Has.Count.EqualTo(2));
            Assert.That(terms[0], Is.EqualTo(new StoryChiefTaxonomyTerm("2769118967", "Go green", "go-green")));
            Assert.That(terms[1], Is.EqualTo(new StoryChiefTaxonomyTerm("42", "Kentico", "kentico")));
        }
    }

    [Test]
    public void ParserReadsWrappedPrimaryCategory()
    {
        using var payload = JsonDocument.Parse("""
            {
              "category": {
                "data": {"storychief_id":2544218965,"name":"Plastic","slug":"plastic"}
              }
            }
            """);

        var terms = StoryChiefTaxonomyParser.Parse(payload.RootElement, "category")
            ?? throw new AssertionException("Expected the StoryChief category to be parsed.");

        Assert.That(terms, Is.EqualTo(new[]
        {
            new StoryChiefTaxonomyTerm("2544218965", "Plastic", "plastic"),
        }));
    }

    [Test]
    public void ParserDistinguishesMissingAndEmptyTaxonomyValues()
    {
        using var payload = JsonDocument.Parse("""
            {"categories":{"data":[]},"category":null}
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StoryChiefTaxonomyParser.Parse(payload.RootElement, "tags"), Is.Null);
            Assert.That(StoryChiefTaxonomyParser.Parse(payload.RootElement, "categories"), Is.Empty);
            Assert.That(StoryChiefTaxonomyParser.Parse(payload.RootElement, "category"), Is.Empty);
        }
    }

    [TestCase("{\"tags\":{\"data\":[\"invalid\"]}}", "non-object")]
    [TestCase("{\"tags\":{\"data\":[{\"storychief_id\":1}]}}", "without a name")]
    [TestCase("{\"tags\":true}", "object, array, or null")]
    public void ParserRejectsMalformedTaxonomyValues(string json, string expectedMessage)
    {
        using var payload = JsonDocument.Parse(json);

        var exception = Assert.Throws<JsonException>(new Action(
            () => StoryChiefTaxonomyParser.Parse(payload.RootElement, "tags")));

        Assert.That(exception.Message, Does.Contain(expectedMessage));
    }

    [Test]
    public void ManagedTagNamesAreStableAcrossCategoryPayloadShapes()
    {
        string primary = StoryChiefTaxonomyManager.GetManagedTagName("category", "ArticleCategories", "123");
        string collection = StoryChiefTaxonomyManager.GetManagedTagName("categories", "ArticleCategories", "123");
        string tag = StoryChiefTaxonomyManager.GetManagedTagName("tags", "ArticleCategories", "123");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(primary, Is.EqualTo(collection));
            Assert.That(tag, Is.Not.EqualTo(primary));
            Assert.That(primary, Does.StartWith("StoryChief_"));
        }
    }

    [Test]
    public void TaxonomyFieldsUseKenticosDocumentedTagReferenceType()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var references = StoryChiefTaxonomyManager.CreateTagReferences([first, second]);

        Assert.That(references.Select(reference => reference.Identifier), Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public void MapTaxonomyCreatesConfigurableMapping()
    {
        var options = new StoryChiefPageOptions();

        var mapping = options.MapTaxonomy("tags", "ArticleTags", "ArticleTaxonomy");
        mapping.CreateMissingTags = false;
        mapping.MapTag("2769118967", "GoGreen");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.TaxonomyMappings["tags"], Is.SameAs(mapping));
            Assert.That(mapping.XperienceFieldName, Is.EqualTo("ArticleTags"));
            Assert.That(mapping.TaxonomyName, Is.EqualTo("ArticleTaxonomy"));
            Assert.That(mapping.CreateMissingTags, Is.False);
            Assert.That(mapping.TagMappings["2769118967"], Is.EqualTo("GoGreen"));
            Assert.That(StoryChiefTaxonomyManager.IsConfigured(options.TaxonomyMappings), Is.True);
        }
    }
}
