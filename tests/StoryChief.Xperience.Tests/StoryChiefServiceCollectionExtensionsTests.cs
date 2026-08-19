using CMS.ContentEngine;
using CMS.DataEngine;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace StoryChief.Xperience.Tests;

public sealed class StoryChiefServiceCollectionExtensionsTests
{
    [Test]
    public void KenticoPublisherUsesRegisteredChannelInfoProvider()
    {
        var constructor = typeof(KenticoStoryChiefContentPublisher).GetConstructors().Single();

        Assert.That(
            constructor.GetParameters().Select(parameter => parameter.ParameterType),
            Does.Contain(typeof(IInfoProvider<ChannelInfo>)));
    }

    [Test]
    public void ConfigurationRegistrationBindsPageAndFieldMappings()
    {
        var values = new Dictionary<string, string?>
        {
            ["SigningKey"] = "configured-secret",
            ["MaxRequestBodyBytes"] = "2048",
            ["Page:WebsiteChannelName"] = "AcmeWebsite",
            ["Page:ContentTypeName"] = "Acme.ArticlePage",
            ["Page:PageTemplateIdentifier"] = "Acme.Article",
            ["Page:LanguageName"] = "nl-BE",
            ["Page:LanguageMappings:en"] = "en-US",
            ["Page:LanguageMappings:nl"] = "nl-BE",
            ["Page:AuditUserName"] = "storychief-integration",
            ["Page:CoverImage:ContentTypeName"] = "Acme.Image",
            ["Page:CoverImage:AssetFieldName"] = "ImageFile",
            ["Page:CoverImage:PageFieldName"] = "ArticleTeaser",
            ["Page:CoverImage:AltTextFieldName"] = "ImageAltText",
            ["Page:CoverImage:WorkspaceName"] = "Content",
            ["Page:CoverImage:MaxFileSizeBytes"] = "4096",
            ["Page:TaxonomyMappings:tags:XperienceFieldName"] = "ArticleTags",
            ["Page:TaxonomyMappings:tags:TaxonomyName"] = "ArticleTaxonomy",
            ["Page:TaxonomyMappings:tags:CreateMissingTags"] = "false",
            ["Page:TaxonomyMappings:tags:TagMappings:2769118967"] = "GoGreen",
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.SigningKey, Is.EqualTo("configured-secret"));
            Assert.That(options.MaxRequestBodyBytes, Is.EqualTo(2048));
            Assert.That(options.Page.WebsiteChannelName, Is.EqualTo("AcmeWebsite"));
            Assert.That(options.Page.ContentTypeName, Is.EqualTo("Acme.ArticlePage"));
            Assert.That(options.Page.PageTemplateIdentifier, Is.EqualTo("Acme.Article"));
            Assert.That(options.Page.LanguageName, Is.EqualTo("nl-BE"));
            Assert.That(options.Page.LanguageMappings,
                Is.EquivalentTo(new Dictionary<string, string> { ["en"] = "en-US", ["nl"] = "nl-BE" }));
            Assert.That(options.Page.AuditUserName, Is.EqualTo("storychief-integration"));
            Assert.That(options.Page.CoverImage.ContentTypeName, Is.EqualTo("Acme.Image"));
            Assert.That(options.Page.CoverImage.AssetFieldName, Is.EqualTo("ImageFile"));
            Assert.That(options.Page.CoverImage.PageFieldName, Is.EqualTo("ArticleTeaser"));
            Assert.That(options.Page.CoverImage.AltTextFieldName, Is.EqualTo("ImageAltText"));
            Assert.That(options.Page.CoverImage.WorkspaceName, Is.EqualTo("Content"));
            Assert.That(options.Page.CoverImage.MaxFileSizeBytes, Is.EqualTo(4096));
            Assert.That(options.Page.TaxonomyMappings["tags"].XperienceFieldName, Is.EqualTo("ArticleTags"));
            Assert.That(options.Page.TaxonomyMappings["tags"].TaxonomyName, Is.EqualTo("ArticleTaxonomy"));
            Assert.That(options.Page.TaxonomyMappings["tags"].CreateMissingTags, Is.False);
            Assert.That(options.Page.TaxonomyMappings["tags"].TagMappings["2769118967"], Is.EqualTo("GoGreen"));
            Assert.That(options.Page.FieldMappings["title"],
                Is.EqualTo(new StoryChiefFieldMapping("ArticleTitle", StoryChiefFieldValueKind.String)));
            Assert.That(options.Page.FieldMappings["published_at"],
                Is.EqualTo(new StoryChiefFieldMapping("ArticlePublishedAt", StoryChiefFieldValueKind.DateTime)));
        }
    }
}
