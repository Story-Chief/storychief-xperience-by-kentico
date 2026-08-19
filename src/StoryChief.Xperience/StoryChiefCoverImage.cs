using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using CMS.ContentEngine;
using CMS.Membership;

namespace StoryChief.Xperience;

internal enum StoryChiefCoverImageState
{
    Unspecified,
    Removed,
    Present,
}

internal sealed record StoryChiefCoverImageInput(
    StoryChiefCoverImageState State,
    Uri? Url = null,
    string? Name = null,
    string? AltText = null);

internal sealed record StoryChiefDownloadedCoverImage(
    byte[] Data,
    string Name,
    string Extension,
    DateTime LastModified);

internal sealed record StoryChiefCoverImageMutation(
    IReadOnlyCollection<ContentItemReference> References,
    int? ContentItemIdToDelete = null,
    int? CreatedContentItemId = null);

internal static class StoryChiefCoverImageParser
{
    public static StoryChiefCoverImageInput Parse(JsonElement story)
    {
        if (!story.TryGetProperty("featured_image", out var featuredImage))
        {
            return new StoryChiefCoverImageInput(StoryChiefCoverImageState.Unspecified);
        }

        if (featuredImage.ValueKind is JsonValueKind.Null)
        {
            return new StoryChiefCoverImageInput(StoryChiefCoverImageState.Removed);
        }

        if (featuredImage.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The StoryChief featured_image must be an object or null.");
        }

        string? url = GetOptionalString(featuredImage, "url");
        if (string.IsNullOrWhiteSpace(url))
        {
            return new StoryChiefCoverImageInput(StoryChiefCoverImageState.Removed);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("The StoryChief featured_image.url must be an absolute HTTPS URL.");
        }

        return new StoryChiefCoverImageInput(
            StoryChiefCoverImageState.Present,
            uri,
            GetOptionalString(featuredImage, "name"),
            GetOptionalString(featuredImage, "alt"));
    }

    private static string? GetOptionalString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

internal interface IStoryChiefRemoteImageUrlValidator
{
    public Task ValidateAsync(Uri uri, CancellationToken cancellationToken);
}

internal sealed class StoryChiefRemoteImageUrlValidator : IStoryChiefRemoteImageUrlValidator
{
    public async Task ValidateAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("StoryChief cover images must use HTTPS.");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new HttpRequestException("The StoryChief cover-image host could not be resolved.", exception);
        }

        if (addresses.Length == 0 || addresses.Any(IsNonPublicAddress))
        {
            throw new JsonException("The StoryChief cover-image URL must resolve only to public network addresses.");
        }
    }

    internal static bool IsNonPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] is 0 or 10 or 127
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2)
                || (bytes[0] == 198 && bytes[1] is 18 or 19)
                || (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
                || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)
                || bytes[0] >= 224;
        }

        return address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.IPv6None)
            || address.Equals(IPAddress.IPv6Loopback)
            || address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6Multicast
            || IsUniqueLocalIpv6(bytes)
            || IsDocumentationIpv6(bytes);
    }

    private static bool IsUniqueLocalIpv6(byte[] bytes) => bytes.Length == 16 && (bytes[0] & 0xFE) == 0xFC;

    private static bool IsDocumentationIpv6(byte[] bytes) => bytes.Length == 16
        && bytes[0] == 0x20
        && bytes[1] == 0x01
        && bytes[2] == 0x0D
        && bytes[3] == 0xB8;
}

internal sealed class StoryChiefCoverImageDownloader(
    HttpClient httpClient,
    IStoryChiefRemoteImageUrlValidator urlValidator)
{
    private const int MaximumRedirects = 5;

    public async Task<StoryChiefDownloadedCoverImage> DownloadAsync(
        StoryChiefCoverImageInput image,
        int maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        if (image.State != StoryChiefCoverImageState.Present || image.Url is null)
        {
            throw new ArgumentException("A present StoryChief cover image is required.", nameof(image));
        }

        if (maxFileSizeBytes <= 0)
        {
            throw new StoryChiefPublisherNotConfiguredException(
                "StoryChief cover image MaxFileSizeBytes must be greater than zero.");
        }

        var currentUri = image.Url;
        for (int redirect = 0; redirect <= MaximumRedirects; redirect++)
        {
            await urlValidator.ValidateAsync(currentUri, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                if (redirect == MaximumRedirects || response.Headers.Location is null)
                {
                    throw new HttpRequestException("The StoryChief cover image returned too many redirects.");
                }

                currentUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);
                continue;
            }

            response.EnsureSuccessStatusCode();
            EnsureImageContentType(response.Content.Headers.ContentType);
            EnsureContentLength(response.Content.Headers.ContentLength, maxFileSizeBytes);

            byte[] data = await ReadWithinLimit(response.Content, maxFileSizeBytes, cancellationToken);
            string extension = GetExtension(image.Name, currentUri, response.Content.Headers.ContentType);
            string name = GetFileName(image.Name, currentUri, extension);
            var lastModified = response.Content.Headers.LastModified?.UtcDateTime ?? DateTime.UtcNow;

            return new StoryChiefDownloadedCoverImage(data, name, extension, lastModified);
        }

        throw new HttpRequestException("The StoryChief cover image could not be downloaded.");
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.Moved or
        HttpStatusCode.Redirect or
        HttpStatusCode.RedirectMethod or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private static void EnsureImageContentType(MediaTypeHeaderValue? contentType)
    {
        if (contentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new JsonException("The StoryChief featured_image.url did not return an image content type.");
        }
    }

    private static void EnsureContentLength(long? contentLength, int maxFileSizeBytes)
    {
        if (contentLength > maxFileSizeBytes)
        {
            throw new JsonException($"The StoryChief cover image exceeds the {maxFileSizeBytes}-byte limit.");
        }
    }

    private static async Task<byte[]> ReadWithinLimit(
        HttpContent content,
        int maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        byte[] buffer = new byte[81920];

        while (true)
        {
            int bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                return destination.ToArray();
            }

            if (destination.Length + bytesRead > maxFileSizeBytes)
            {
                throw new JsonException($"The StoryChief cover image exceeds the {maxFileSizeBytes}-byte limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static string GetExtension(string? suppliedName, Uri uri, MediaTypeHeaderValue? contentType)
    {
        string? contentTypeExtension = contentType?.MediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/avif" => ".avif",
            "image/svg+xml" => ".svg",
            _ => null,
        };
        if (contentTypeExtension is not null)
        {
            return contentTypeExtension;
        }

        string extension = Path.GetExtension(GetCandidateName(suppliedName, uri));
        if (!string.IsNullOrWhiteSpace(extension)
            && extension.Length > 1
            && extension.Length <= 10
            && extension[1..].All(char.IsLetterOrDigit))
        {
            return extension.ToLowerInvariant();
        }

        throw new JsonException("The StoryChief cover image has no supported file extension.");
    }

    private static string GetFileName(string? suppliedName, Uri uri, string extension)
    {
        string fileName = Path.GetFileName(GetCandidateName(suppliedName, uri));
        string stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "storychief-cover";
        }

        string safeStem = string.Concat(stem.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-')).Trim('-');

        return $"{(string.IsNullOrWhiteSpace(safeStem) ? "storychief-cover" : safeStem)}{extension}";
    }

    private static string GetCandidateName(string? suppliedName, Uri uri) =>
        !string.IsNullOrWhiteSpace(suppliedName) ? suppliedName : Uri.UnescapeDataString(uri.AbsolutePath);
}

internal sealed class StoryChiefCoverImageManager(
    IContentItemManagerFactory contentItemManagerFactory,
    IContentQueryExecutor contentQueryExecutor,
    IUserInfoProvider userInfoProvider,
    StoryChiefCoverImageDownloader downloader)
{
    public async Task<StoryChiefCoverImageMutation?> PrepareAsync(
        JsonElement story,
        string languageName,
        StoryChiefPageOptions pageOptions,
        bool publish,
        CancellationToken cancellationToken)
    {
        var options = pageOptions.CoverImage;
        if (!IsEnabled(options))
        {
            ValidateNotPartiallyConfigured(options);
            return null;
        }

        var image = StoryChiefCoverImageParser.Parse(story);
        if (image.State == StoryChiefCoverImageState.Unspecified)
        {
            return null;
        }

        string itemName = GetContentItemName(story, pageOptions.WebsiteChannelName, languageName);
        var existing = await FindAsync(itemName, languageName, options, cancellationToken);
        if (image.State == StoryChiefCoverImageState.Removed)
        {
            return new StoryChiefCoverImageMutation([], existing?.Id);
        }

        var downloaded = await downloader.DownloadAsync(image, options.MaxFileSizeBytes, cancellationToken);
        var manager = CreateManager(pageOptions.AuditUserName);
        var source = new ContentItemAssetStreamSource(_ =>
            Task.FromResult<Stream>(new MemoryStream(downloaded.Data, writable: false)));
        var asset = new ContentItemAssetMetadataWithSource(
            source,
            downloaded.Name,
            downloaded.Extension,
            downloaded.Data.LongLength,
            downloaded.LastModified);
        var fields = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [options.AssetFieldName] = asset,
        };
        if (!string.IsNullOrWhiteSpace(options.AltTextFieldName))
        {
            fields[options.AltTextFieldName] = image.AltText ?? string.Empty;
        }

        int contentItemId;
        Guid contentItemGuid;
        int? createdContentItemId = null;
        if (existing is null)
        {
            var parameters = new CreateContentItemParameters(
                options.ContentTypeName,
                itemName,
                GetDisplayName(story),
                languageName,
                options.WorkspaceName);
            contentItemId = await manager.Create(parameters, new ContentItemData(fields), cancellationToken);
            contentItemGuid = (await manager.GetContentItemMetadata(contentItemId, cancellationToken)).ContentItemGUID;
            createdContentItemId = contentItemId;
        }
        else
        {
            contentItemId = existing.Id;
            contentItemGuid = existing.Guid;
            await manager.TryCreateDraft(contentItemId, languageName, cancellationToken);
            if (!await manager.TryUpdateDraft(
                contentItemId,
                languageName,
                new ContentItemData(fields),
                cancellationToken))
            {
                throw new InvalidOperationException($"Xperience could not update cover image {contentItemId}.");
            }
        }

        if (publish && !await manager.TryPublish(contentItemId, languageName, cancellationToken))
        {
            throw new InvalidOperationException($"Xperience could not publish cover image {contentItemId}.");
        }

        return new StoryChiefCoverImageMutation(
            [new ContentItemReference { Identifier = contentItemGuid }],
            CreatedContentItemId: createdContentItemId);
    }

    public async Task PublishExistingAsync(
        JsonElement story,
        string languageName,
        StoryChiefPageOptions pageOptions,
        CancellationToken cancellationToken)
    {
        var options = pageOptions.CoverImage;
        if (!IsEnabled(options))
        {
            ValidateNotPartiallyConfigured(options);
            return;
        }

        string itemName = GetContentItemName(story, pageOptions.WebsiteChannelName, languageName);
        var existing = await FindAsync(itemName, languageName, options, cancellationToken);
        if (existing is not null && existing.Status != VersionStatus.Published)
        {
            var manager = CreateManager(pageOptions.AuditUserName);
            if (!await manager.TryPublish(existing.Id, languageName, cancellationToken))
            {
                throw new InvalidOperationException($"Xperience could not publish cover image {existing.Id}.");
            }
        }
    }

    public async Task DeleteExistingAsync(
        JsonElement story,
        string languageName,
        StoryChiefPageOptions pageOptions,
        bool permanently,
        CancellationToken cancellationToken)
    {
        var options = pageOptions.CoverImage;
        if (!IsEnabled(options))
        {
            ValidateNotPartiallyConfigured(options);
            return;
        }

        string itemName = GetContentItemName(story, pageOptions.WebsiteChannelName, languageName);
        var existing = await FindAsync(itemName, languageName, options, cancellationToken);
        if (existing is not null)
        {
            await DeleteAsync(
                existing.Id,
                languageName,
                pageOptions.AuditUserName,
                cancellationToken,
                permanently);
        }
    }

    public Task DeleteAsync(
        int contentItemId,
        string languageName,
        string auditUserName,
        CancellationToken cancellationToken,
        bool permanently = true) => CreateManager(auditUserName).Delete(
            new DeleteContentItemParameters(contentItemId, languageName) { Permanently = permanently },
            cancellationToken);

    internal static bool IsEnabled(StoryChiefCoverImageOptions options) =>
        !string.IsNullOrWhiteSpace(options.ContentTypeName)
        && !string.IsNullOrWhiteSpace(options.AssetFieldName)
        && !string.IsNullOrWhiteSpace(options.PageFieldName)
        && !string.IsNullOrWhiteSpace(options.WorkspaceName);

    internal static string GetContentItemName(
        JsonElement story,
        string websiteChannelName,
        string languageName)
    {
        string storyChiefId = GetRequiredIdentifier(story);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{websiteChannelName}\n{storyChiefId}\n{languageName}"));

        return $"StoryChiefCover_{Convert.ToHexString(hash)[..24]}";
    }

    private async Task<CoverImageItem?> FindAsync(
        string itemName,
        string languageName,
        StoryChiefCoverImageOptions options,
        CancellationToken cancellationToken)
    {
        var query = new ContentItemQueryBuilder()
            .ForContentType(options.ContentTypeName, parameters => parameters
                .TopN(1)
                .Where(where => where.WhereEquals("ContentItemName", itemName)))
            .InLanguage(languageName, useLanguageFallbacks: false);
        query.InWorkspaces(options.WorkspaceName);

        var items = await contentQueryExecutor.GetResult(
            query,
            item => new CoverImageItem(
                item.ContentItemID,
                item.ContentItemGUID,
                item.ContentItemCommonDataVersionStatus),
            new ContentQueryExecutionOptions { ForPreview = true },
            cancellationToken);

        return items.SingleOrDefault();
    }

    private IContentItemManager CreateManager(string auditUserName)
    {
        var user = userInfoProvider.Get(auditUserName) ?? throw new StoryChiefPublisherNotConfiguredException(
            $"The Xperience audit user '{auditUserName}' does not exist.");

        return contentItemManagerFactory.Create(user.UserID);
    }

    private static void ValidateNotPartiallyConfigured(StoryChiefCoverImageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ContentTypeName)
            || !string.IsNullOrWhiteSpace(options.AssetFieldName)
            || !string.IsNullOrWhiteSpace(options.PageFieldName)
            || !string.IsNullOrWhiteSpace(options.AltTextFieldName)
            || !string.IsNullOrWhiteSpace(options.WorkspaceName))
        {
            throw new StoryChiefPublisherNotConfiguredException(
                "StoryChief cover images require ContentTypeName, AssetFieldName, PageFieldName, and WorkspaceName.");
        }
    }

    private static string GetRequiredIdentifier(JsonElement story)
    {
        if (story.TryGetProperty("storychief_id", out var id)
            && id.ValueKind is JsonValueKind.String or JsonValueKind.Number)
        {
            return id.ToString();
        }

        throw new JsonException(
            "The StoryChief storychief_id is required when cover-image publishing is configured.");
    }

    private static string GetDisplayName(JsonElement story)
    {
        string title = story.TryGetProperty("title", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
        string displayName = string.IsNullOrWhiteSpace(title) ? "StoryChief cover image" : $"Cover: {title}";

        return displayName.Length <= 200 ? displayName : displayName[..200];
    }

    private sealed record CoverImageItem(int Id, Guid Guid, VersionStatus Status);
}
