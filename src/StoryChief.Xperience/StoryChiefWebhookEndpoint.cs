using System.Diagnostics;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace StoryChief.Xperience;

internal static class StoryChiefWebhookEndpoint
{
    internal static async Task<IResult> HandleAsync(
        HttpRequest request,
        IOptions<StoryChiefXperienceOptions> optionsAccessor,
        IStoryChiefContentPublisher publisher,
        CancellationToken cancellationToken)
    {
        var options = optionsAccessor.Value;

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return Results.Problem(
                title: "StoryChief is not configured",
                detail: "Configure StoryChief:SigningKey before accepting webhook requests.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        byte[]? body = await ReadBodyAsync(request, options.MaxRequestBodyBytes, cancellationToken);
        if (body is null)
        {
            return Results.Problem(title: "Payload too large", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (!StoryChiefWebhookSignature.TryValidate(body, options.SigningKey, out var payload))
        {
            return Results.Problem(
                title: "Invalid StoryChief webhook signature",
                statusCode: StatusCodes.Status400BadRequest);
        }

        using var validatedPayload = payload!;
        var root = validatedPayload.RootElement;
        var metadata = root.GetProperty("meta");

        if (!metadata.TryGetProperty("event", out var eventElement)
            || eventElement.ValueKind != JsonValueKind.String)
        {
            return Results.Problem(title: "The StoryChief event is missing", statusCode: StatusCodes.Status400BadRequest);
        }

        string eventName = eventElement.GetString()!;
        if (eventName.Equals("test", StringComparison.Ordinal))
        {
            return SignedJson(CreateConnectionMetadata(options), options.SigningKey);
        }

        if (!root.TryGetProperty("data", out var story) || story.ValueKind != JsonValueKind.Object)
        {
            return Results.Problem(title: "The StoryChief story is missing", statusCode: StatusCodes.Status400BadRequest);
        }

        var context = new StoryChiefPublishingContext(
            eventName,
            GetOptionalString(metadata, "status"),
            GetOptionalBoolean(metadata, "lock_updates"));

        if (eventName is not ("publish" or "update" or "delete"))
        {
            return Results.Problem(
                title: "Unsupported StoryChief event",
                detail: $"The event '{eventName}' is not supported.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = eventName switch
            {
                "publish" => await publisher.PublishAsync(story.Clone(), context, cancellationToken),
                "update" => await publisher.UpdateAsync(story.Clone(), context, cancellationToken),
                "delete" => await publisher.DeleteAsync(story.Clone(), context, cancellationToken),
                _ => throw new UnreachableException(),
            };

            var response = new Dictionary<string, object?>
            {
                ["id"] = result.Id,
                ["permalink"] = result.Permalink,
            };

            if (!string.IsNullOrWhiteSpace(result.Status))
            {
                response["status"] = result.Status;
            }

            return SignedJson(response, options.SigningKey);
        }
        catch (StoryChiefPublisherNotConfiguredException exception)
        {
            return Results.Problem(
                title: "StoryChief content mapping is not configured",
                detail: exception.Message,
                statusCode: StatusCodes.Status501NotImplemented);
        }
        catch (JsonException exception)
        {
            return Results.Problem(
                title: "Invalid StoryChief payload",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static IResult SignedJson(object value, string signingKey) =>
        new StoryChiefSignedJsonResult(StoryChiefWebhookSignature.Sign(value, signingKey));

    private static Dictionary<string, object?> CreateConnectionMetadata(StoryChiefXperienceOptions options)
    {
        var pluginVersion = typeof(StoryChiefWebhookEndpoint).Assembly.GetName().Version ?? new Version(1, 0, 0);
        var cmsVersion = typeof(CMS.AssemblyDiscoverableAttribute).Assembly.GetName().Version;

        return new Dictionary<string, object?>
        {
            ["meta"] = new Dictionary<string, object?>
            {
                ["plugin_version"] = pluginVersion.ToString(3),
                ["versioning"] = new object[]
                {
                    new Dictionary<string, object?> { ["type"] = "dotnet_version", ["value"] = Environment.Version.ToString() },
                    new Dictionary<string, object?> { ["type"] = "cms_type", ["value"] = "xperience-by-kentico" },
                    new Dictionary<string, object?> { ["type"] = "cms_version", ["value"] = cmsVersion?.ToString(3) ?? "unknown" },
                },
                ["features"] = new[] { "publish_as_draft", "lock_updates" },
                ["settings"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["key"] = "page_mapping_configured",
                        ["title"] = "Xperience page mapping configured",
                        ["description"] = "A website channel, page content type, and field mapping are configured.",
                        ["value"] = IsPageMappingConfigured(options.Page),
                    },
                },
            },
        };
    }

    private static bool IsPageMappingConfigured(StoryChiefPageOptions options) =>
        !string.IsNullOrWhiteSpace(options.WebsiteChannelName)
        && !string.IsNullOrWhiteSpace(options.ContentTypeName)
        && !string.IsNullOrWhiteSpace(options.LanguageName)
        && !string.IsNullOrWhiteSpace(options.AuditUserName)
        && options.FieldMappings.Count > 0;

    private static string? GetOptionalString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool GetOptionalBoolean(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

    private static async Task<byte[]?> ReadBodyAsync(
        HttpRequest request,
        int maxRequestBodyBytes,
        CancellationToken cancellationToken)
    {
        if (maxRequestBodyBytes <= 0)
        {
            throw new InvalidOperationException("StoryChief MaxRequestBodyBytes must be greater than zero.");
        }

        if (request.ContentLength > maxRequestBodyBytes)
        {
            return null;
        }

        using var output = new MemoryStream();
        byte[] buffer = new byte[81920];
        int totalBytes = 0;

        while (true)
        {
            int bytesRead = await request.Body.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                return output.ToArray();
            }

            totalBytes += bytesRead;
            if (totalBytes > maxRequestBodyBytes)
            {
                return null;
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

}

internal sealed class StoryChiefSignedJsonResult(byte[] body) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        httpContext.Response.ContentLength = body.Length;
        await httpContext.Response.Body.WriteAsync(body, httpContext.RequestAborted);
    }
}
