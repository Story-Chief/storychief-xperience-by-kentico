using System.Globalization;
using System.Text.Json;

using CMS.ContentEngine;
using CMS.DataEngine;
using CMS.Membership;
using CMS.Websites;
using CMS.Websites.VisualBuilder.Internal;

using Microsoft.Extensions.Options;

namespace StoryChief.Xperience;

internal sealed class KenticoStoryChiefContentPublisher(
    IWebPageManagerFactory webPageManagerFactory,
    IInfoProvider<ChannelInfo> channelInfoProvider,
    IInfoProvider<WebsiteChannelInfo> websiteChannelInfoProvider,
    IInfoProvider<ContentLanguageInfo> contentLanguageInfoProvider,
    IUserInfoProvider userInfoProvider,
    IWebPageUrlRetriever webPageUrlRetriever,
    IVisualBuilderDataManager visualBuilderDataManager,
    StoryChiefCoverImageManager coverImageManager,
    IOptions<StoryChiefXperienceOptions> optionsAccessor) : IStoryChiefContentPublisher
{
    public async Task<StoryChiefPublishResult> PublishAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken)
    {
        var options = GetValidatedOptions();
        var webPageManager = CreateWebPageManager(options);
        string languageName = StoryChiefLanguageResolver.Resolve(story, options);
        string displayName = GetDisplayName(story);
        var fields = StoryChiefFieldMapper.Map(story, options.FieldMappings);
        string? slug = GetOptionalString(story, "seo_slug");
        bool publish = ShouldPublish(context);
        var coverImage = await coverImageManager.PrepareAsync(
            story,
            languageName,
            options,
            publish,
            cancellationToken);
        if (coverImage is not null)
        {
            fields[options.CoverImage.PageFieldName] = coverImage.References;
        }

        var itemData = new ContentItemData(fields);
        int webPageItemId;

        try
        {
            if (TryGetTranslationSourceId(story, out int sourceWebPageItemId))
            {
                var variantParameters = new CMS.Websites.CreateLanguageVariantParameters(
                    sourceWebPageItemId,
                    languageName,
                    displayName,
                    itemData);
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    variantParameters.UrlSlug = slug;
                }

                if (!await webPageManager.TryCreateLanguageVariant(variantParameters, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Xperience could not create the '{languageName}' language variant for page {sourceWebPageItemId}.");
                }

                webPageItemId = sourceWebPageItemId;
            }
            else
            {
                var contentItemParameters = new ContentItemParameters(options.ContentTypeName, itemData);
                var createParameters = new CreateWebPageParameters(displayName, languageName, contentItemParameters)
                {
                    ParentWebPageItemID = options.ParentWebPageItemId,
                };
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    createParameters.UrlSlug = slug;
                }

                webPageItemId = await webPageManager.Create(createParameters, cancellationToken);
            }
        }
        catch
        {
            if (coverImage?.CreatedContentItemId is int createdContentItemId)
            {
                await coverImageManager.DeleteAsync(
                    createdContentItemId,
                    languageName,
                    options.AuditUserName,
                    cancellationToken);
            }

            throw;
        }

        await AssignPageTemplate(
            webPageManager,
            webPageItemId,
            languageName,
            options,
            cancellationToken);
        if (publish && !await webPageManager.TryPublish(webPageItemId, languageName, cancellationToken))
        {
            throw new InvalidOperationException($"Xperience could not publish page {webPageItemId}.");
        }

        string? permalink = await GetPermalink(webPageItemId, languageName, !publish, cancellationToken);

        return new StoryChiefPublishResult(
            webPageItemId.ToString(CultureInfo.InvariantCulture),
            permalink,
            publish ? "published" : "draft");
    }

    public async Task<StoryChiefPublishResult> UpdateAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken)
    {
        var options = GetValidatedOptions();
        var webPageManager = CreateWebPageManager(options);
        string languageName = StoryChiefLanguageResolver.Resolve(story, options);
        int webPageItemId = GetExternalId(story);
        bool publish = ShouldPublish(context);
        StoryChiefCoverImageMutation? coverImage = null;

        if (!context.LockUpdates)
        {
            await webPageManager.TryCreateDraft(webPageItemId, languageName, cancellationToken);

            var fields = StoryChiefFieldMapper.Map(story, options.FieldMappings);
            coverImage = await coverImageManager.PrepareAsync(
                story,
                languageName,
                options,
                publish,
                cancellationToken);
            if (coverImage is not null)
            {
                fields[options.CoverImage.PageFieldName] = coverImage.References;
            }

            var itemData = new ContentItemData(fields);
            string? slug = GetOptionalString(story, "seo_slug");
            var updateData = string.IsNullOrWhiteSpace(slug)
                ? new UpdateDraftData(itemData)
                : new UpdateDraftData(itemData, slug);
            if (!await webPageManager.TryUpdateDraft(
                webPageItemId,
                languageName,
                updateData,
                cancellationToken))
            {
                throw new InvalidOperationException($"Xperience could not update page {webPageItemId}.");
            }

            if (!string.IsNullOrWhiteSpace(slug) && !IsTranslation(story))
            {
                await webPageManager.UpdateTreePathSlug(webPageItemId, slug, cancellationToken);
            }

        }
        else if (!publish)
        {
            await webPageManager.TryCreateDraft(webPageItemId, languageName, cancellationToken);
        }

        if (context.LockUpdates && publish)
        {
            await coverImageManager.PublishExistingAsync(
                story,
                languageName,
                options,
                cancellationToken);
        }

        if (publish && !await webPageManager.TryPublish(webPageItemId, languageName, cancellationToken))
        {
            throw new InvalidOperationException($"Xperience could not publish page {webPageItemId}.");
        }

        if (publish && coverImage?.ContentItemIdToDelete is int contentItemIdToDelete)
        {
            await coverImageManager.DeleteAsync(
                contentItemIdToDelete,
                languageName,
                options.AuditUserName,
                cancellationToken);
        }

        string? permalink = await GetPermalink(webPageItemId, languageName, !publish, cancellationToken);

        return new StoryChiefPublishResult(
            webPageItemId.ToString(CultureInfo.InvariantCulture),
            permalink,
            publish ? "published" : "draft");
    }

    public async Task<StoryChiefPublishResult> DeleteAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken)
    {
        var options = GetValidatedOptions();
        var webPageManager = CreateWebPageManager(options);
        string languageName = StoryChiefLanguageResolver.Resolve(story, options);
        int webPageItemId = GetExternalId(story);

        await webPageManager.Delete(
            new DeleteWebPageParameters(webPageItemId, languageName)
            {
                Permanently = options.PermanentlyDelete,
            },
            cancellationToken);

        await coverImageManager.DeleteExistingAsync(
            story,
            languageName,
            options,
            options.PermanentlyDelete,
            cancellationToken);

        return new StoryChiefPublishResult(
            webPageItemId.ToString(CultureInfo.InvariantCulture),
            null);
    }

    private StoryChiefPageOptions GetValidatedOptions()
    {
        var options = optionsAccessor.Value.Page;

        if (string.IsNullOrWhiteSpace(options.WebsiteChannelName))
        {
            throw CreateConfigurationException("StoryChief page WebsiteChannelName is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.ContentTypeName))
        {
            throw CreateConfigurationException("StoryChief page ContentTypeName is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.LanguageName))
        {
            throw CreateConfigurationException("StoryChief page LanguageName is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.AuditUserName))
        {
            throw CreateConfigurationException("StoryChief page AuditUserName is missing.");
        }

        if (options.FieldMappings.Count == 0)
        {
            throw CreateConfigurationException("At least one StoryChief page field mapping is required.");
        }

        return options;
    }

    private IWebPageManager CreateWebPageManager(StoryChiefPageOptions options)
    {
        var channel = channelInfoProvider
            .Get()
            .WhereEquals(nameof(ChannelInfo.ChannelName), options.WebsiteChannelName)
            .TopN(1)
            .FirstOrDefault();
        var websiteChannel = (channel is null
            ? null
            : websiteChannelInfoProvider
                .Get()
                .WhereEquals(nameof(WebsiteChannelInfo.WebsiteChannelChannelID), channel.ChannelID)
                .TopN(1)
                .FirstOrDefault()) ?? throw CreateConfigurationException(
                $"The Xperience website channel '{options.WebsiteChannelName}' does not exist.");

        var auditUser = userInfoProvider.Get(options.AuditUserName) ?? throw CreateConfigurationException(
                $"The Xperience audit user '{options.AuditUserName}' does not exist.");

        return webPageManagerFactory.Create(websiteChannel.WebsiteChannelID, auditUser.UserID);
    }

    private async Task<string?> GetPermalink(
        int webPageItemId,
        string languageName,
        bool forPreview,
        CancellationToken cancellationToken)
    {
        var url = await webPageUrlRetriever.Retrieve(
            webPageItemId,
            languageName,
            forPreview,
            cancellationToken);

        return string.IsNullOrWhiteSpace(url.AbsoluteUrl) ? null : url.AbsoluteUrl;
    }

    private async Task AssignPageTemplate(
        IWebPageManager webPageManager,
        int webPageItemId,
        string languageName,
        StoryChiefPageOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.PageTemplateIdentifier))
        {
            return;
        }

        var language = contentLanguageInfoProvider
            .Get()
            .WhereEquals(nameof(ContentLanguageInfo.ContentLanguageName), languageName)
            .TopN(1)
            .FirstOrDefault() ?? throw CreateConfigurationException(
                $"The Xperience content language '{languageName}' does not exist.");
        var metadata = await webPageManager.GetWebPageMetadata(webPageItemId, cancellationToken);
        string templateConfiguration = JsonSerializer.Serialize(new
        {
            identifier = options.PageTemplateIdentifier,
            properties = (object?)null,
            fieldIdentifiers = (object?)null,
        });

        await visualBuilderDataManager.SetVisualBuilderConfigurationData(
            metadata.ContentItemID,
            language.ContentLanguageID,
            new VisualBuilderData(null!, templateConfiguration),
            cancellationToken);
    }

    private static int GetExternalId(JsonElement story)
    {
        if (!story.TryGetProperty("external_id", out var externalId))
        {
            throw new JsonException("The StoryChief external_id is missing.");
        }

        return GetPageIdentifier(externalId, "external_id");
    }

    private static bool TryGetTranslationSourceId(JsonElement story, out int webPageItemId)
    {
        webPageItemId = 0;
        if (!story.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!source.TryGetProperty("external_id", out var externalId))
        {
            throw new JsonException(
                "The StoryChief source.external_id is missing. Publish the source story before its translation.");
        }

        webPageItemId = GetPageIdentifier(externalId, "source.external_id");
        return true;
    }

    private static int GetPageIdentifier(JsonElement externalId, string propertyPath)
    {
        if (externalId.ValueKind == JsonValueKind.Number && externalId.TryGetInt32(out int numericId))
        {
            return numericId;
        }

        if (externalId.ValueKind == JsonValueKind.String
            && int.TryParse(externalId.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int stringId))
        {
            return stringId;
        }

        throw new JsonException(
            $"The StoryChief {propertyPath} must be a valid Xperience page identifier.");
    }

    private static bool IsTranslation(JsonElement story) =>
        story.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object;

    private static string GetDisplayName(JsonElement story) =>
        GetOptionalString(story, "title")
        ?? GetOptionalString(story, "seo_title")
        ?? $"StoryChief {GetOptionalString(story, "storychief_id") ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}";

    private static string? GetOptionalString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool ShouldPublish(StoryChiefPublishingContext context) =>
        !string.Equals(context.Status, "draft", StringComparison.OrdinalIgnoreCase);

    private static StoryChiefPublisherNotConfiguredException CreateConfigurationException(string message) => new(message);
}
