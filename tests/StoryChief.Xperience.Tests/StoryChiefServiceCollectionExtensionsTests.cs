using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace StoryChief.Xperience.Tests;

public sealed class StoryChiefServiceCollectionExtensionsTests
{
    [Test]
    public void ConfigurationRegistrationBindsPageAndFieldMappings()
    {
        var values = new Dictionary<string, string?>
        {
            ["SigningKey"] = "configured-secret",
            ["MaxRequestBodyBytes"] = "2048",
            ["Page:WebsiteChannelName"] = "AcmeWebsite",
            ["Page:ContentTypeName"] = "Acme.ArticlePage",
            ["Page:LanguageName"] = "nl-BE",
            ["Page:AuditUserName"] = "storychief-integration",
            ["Page:FieldMappings:title:XperienceFieldName"] = "ArticleTitle",
            ["Page:FieldMappings:title:ValueKind"] = "String",
            ["Page:FieldMappings:published_at:XperienceFieldName"] = "ArticlePublishedAt",
            ["Page:FieldMappings:published_at:ValueKind"] = "DateTime",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();

        services.AddStoryChiefXperience(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<StoryChiefXperienceOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(options.SigningKey, Is.EqualTo("configured-secret"));
            Assert.That(options.MaxRequestBodyBytes, Is.EqualTo(2048));
            Assert.That(options.Page.WebsiteChannelName, Is.EqualTo("AcmeWebsite"));
            Assert.That(options.Page.ContentTypeName, Is.EqualTo("Acme.ArticlePage"));
            Assert.That(options.Page.LanguageName, Is.EqualTo("nl-BE"));
            Assert.That(options.Page.AuditUserName, Is.EqualTo("storychief-integration"));
            Assert.That(options.Page.FieldMappings["title"],
                Is.EqualTo(new StoryChiefFieldMapping("ArticleTitle", StoryChiefFieldValueKind.String)));
            Assert.That(options.Page.FieldMappings["published_at"],
                Is.EqualTo(new StoryChiefFieldMapping("ArticlePublishedAt", StoryChiefFieldValueKind.DateTime)));
        });
    }
}
