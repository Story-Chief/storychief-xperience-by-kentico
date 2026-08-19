using System.Globalization;
using System.Text.Json;

using CMS.ContentEngine;
using CMS.DataEngine;
using CMS.Membership;
using CMS.Websites;

using Microsoft.Extensions.Options;

namespace StoryChief.Xperience;

internal sealed class KenticoStoryChiefContentPublisher(
    IWebPageManagerFactory webPageManagerFactory,
    IInfoByNameProvider<ChannelInfo> channelInfoProvider,
    IInfoProvider<WebsiteChannelInfo> websiteChannelInfoProvider,
    IUserInfoProvider userInfoProvider,
    IWebPageUrlRetriever webPageUrlRetriever,
    IOptions<StoryChiefXperienceOptions> optionsAccessor) : IStoryChiefContentPublisher
{
    public async Task<StoryChiefPublishResult> PublishAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken)
    {
        var options = GetValidatedOptions();
        var webPageManager = CreateWebPageManager(options);
        string displayName = GetDisplayName(story);
        var itemData = new ContentItemData(StoryChiefFieldMapper.Map(story, options.FieldMappings));
        var contentItemParameters = new ContentItemParameters(options.ContentTypeName, itemData);
        var createParameters = new CreateWebPageParameters(displayName, options.LanguageName, contentItemParameters)
        {
            ParentWebPageItemID = options.ParentWebPageItemId,
        };

        string? slug = GetOptionalString(story, "seo_slug");
        if (!string.IsNullOrWhiteSpace(slug))
        {
            createParameters.UrlSlug = slug;
        }

        int webPageItemId = await webPageManager.Create(createParameters, cancellationToken);
        bool publish = ShouldPublish(context);

        if (publish && !await webPageManager.TryPublish(webPageItemId, options.LanguageName, cancellationToken))
        {
            throw new InvalidOperationException($"Xperience could not publish page {webPageItemId}.");
        }

        string? permalink = await GetPermalink(webPageItemId, options.LanguageName, !publish, cancellationToken);

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
        int webPageItemId = GetExternalId(story);
        bool publish = ShouldPublish(context);

        if (!context.LockUpdates)
        {
            await webPageManager.TryCreateDraft(webPageItemId, options.LanguageName, cancellationToken);

            var itemData = new ContentItemData(StoryChiefFieldMapper.Map(story, options.FieldMappings));
            if (!await webPageManager.TryUpdateDraft(
                webPageItemId,
                options.LanguageName,
                new UpdateDraftData(itemData),
                cancellationToken))
            {
                throw new InvalidOperationException($"Xperience could not update page {webPageItemId}.");
            }

            string? slug = GetOptionalString(story, "seo_slug");
            if (!string.IsNullOrWhiteSpace(slug))
            {
                await webPageManager.UpdateTreePathSlug(webPageItemId, slug, cancellationToken);
            }
        }
        else if (!publish)
        {
            await webPageManager.TryCreateDraft(webPageItemId, options.LanguageName, cancellationToken);
        }

        if (publish && !await webPageManager.TryPublish(webPageItemId, options.LanguageName, cancellationToken))
        {
            throw new InvalidOperationException($"Xperience could not publish page {webPageItemId}.");
        }

        string? permalink = await GetPermalink(webPageItemId, options.LanguageName, !publish, cancellationToken);

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
        int webPageItemId = GetExternalId(story);

        await webPageManager.Delete(
            new DeleteWebPageParameters(webPageItemId, options.LanguageName)
            {
                Permanently = options.PermanentlyDelete,
            },
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
        var channel = channelInfoProvider.Get(options.WebsiteChannelName);
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

    private static int GetExternalId(JsonElement story)
    {
        if (!story.TryGetProperty("external_id", out var externalId))
        {
            throw new JsonException("The StoryChief external_id is missing.");
        }

        if (externalId.ValueKind == JsonValueKind.Number && externalId.TryGetInt32(out int numericId))
        {
            return numericId;
        }

        if (externalId.ValueKind == JsonValueKind.String
            && int.TryParse(externalId.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int stringId))
        {
            return stringId;
        }

        throw new JsonException("The StoryChief external_id must be a valid Xperience page identifier.");
    }

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
