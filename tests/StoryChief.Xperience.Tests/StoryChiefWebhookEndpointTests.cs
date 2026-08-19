using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace StoryChief.Xperience.Tests;

public sealed class StoryChiefWebhookEndpointTests
{
    private const string SigningKey = "endpoint-test-secret";

    [Test]
    public async Task PublishDispatchesToPublisherAndReturnsSignedResult()
    {
        const string unsignedBody =
            "{\"meta\":{\"event\":\"publish\",\"status\":\"publish\"},\"data\":{\"title\":\"A story\"}}";
        var publisher = new RecordingPublisher
        {
            Result = new StoryChiefPublishResult("42", "https://example.com/a-story", "published"),
        };

        var response = await InvokeAsync(SignRequest(unsignedBody), publisher);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(response.ContentType, Is.EqualTo("application/json; charset=utf-8"));
            Assert.That(publisher.PublishCalls, Is.EqualTo(1));
            Assert.That(publisher.LastStory.GetProperty("title").GetString(), Is.EqualTo("A story"));
            Assert.That(publisher.LastContext, Is.EqualTo(new StoryChiefPublishingContext("publish", "publish", false)));
            Assert.That(HasValidResponseSignature(response.Body), Is.True);
        }

        using var payload = JsonDocument.Parse(response.Body);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(payload.RootElement.GetProperty("id").GetString(), Is.EqualTo("42"));
            Assert.That(payload.RootElement.GetProperty("permalink").GetString(), Is.EqualTo("https://example.com/a-story"));
            Assert.That(payload.RootElement.GetProperty("status").GetString(), Is.EqualTo("published"));
        }
    }

    [Test]
    public async Task ConnectionTestReturnsSignedCapabilitiesWithoutPublishing()
    {
        const string unsignedBody = "{\"meta\":{\"event\":\"test\"},\"data\":{}}";
        var publisher = new RecordingPublisher();

        var response = await InvokeAsync(SignRequest(unsignedBody), publisher, options =>
        {
            options.Page.WebsiteChannelName = "AcmeWebsite";
            options.Page.ContentTypeName = "Acme.ArticlePage";
            options.Page.LanguageName = "en";
            options.Page.AuditUserName = "integration-user";
            options.Page.MapLanguage("en", "en-US");
            options.Page.MapField("title", "ArticleTitle");
            options.Page.CoverImage.ContentTypeName = "Acme.Image";
            options.Page.CoverImage.AssetFieldName = "ImageFile";
            options.Page.CoverImage.PageFieldName = "ArticleTeaser";
            options.Page.CoverImage.WorkspaceName = "Acme.Content";
        });

        using var payload = JsonDocument.Parse(response.Body);
        var metadata = payload.RootElement.GetProperty("meta");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(HasValidResponseSignature(response.Body), Is.True);
            Assert.That(publisher.TotalCalls, Is.Zero);
            Assert.That(metadata.GetProperty("features").EnumerateArray().Select(value => value.GetString()),
                Is.EquivalentTo(new[] { "publish_as_draft", "lock_updates", "multilingual" }));
            Assert.That(metadata.GetProperty("settings")[0].GetProperty("value").GetBoolean(), Is.True);
            Assert.That(metadata.GetProperty("settings")[1].GetProperty("value").GetBoolean(), Is.True);
        }
    }

    [Test]
    public async Task InvalidSignatureIsRejectedWithoutPublishing()
    {
        const string body =
            "{\"meta\":{\"event\":\"publish\",\"mac\":\"0000000000000000000000000000000000000000000000000000000000000000\"},\"data\":{\"title\":\"A story\"}}";
        var publisher = new RecordingPublisher();

        var response = await InvokeAsync(body, publisher);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(publisher.TotalCalls, Is.Zero);
        }
    }

    [Test]
    public async Task OversizedPayloadIsRejectedBeforeSignatureValidation()
    {
        var publisher = new RecordingPublisher();

        var response = await InvokeAsync("{}", publisher, options => options.MaxRequestBodyBytes = 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
            Assert.That(publisher.TotalCalls, Is.Zero);
        }
    }

    private static async Task<WebhookResponse> InvokeAsync(
        string body,
        RecordingPublisher publisher,
        Action<StoryChiefXperienceOptions>? configure = null)
    {
        var options = new StoryChiefXperienceOptions
        {
            SigningKey = SigningKey,
        };
        configure?.Invoke(options);

        await using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
        };
        byte[] requestBody = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(requestBody);
        context.Request.ContentLength = requestBody.Length;
        context.Response.Body = new MemoryStream();

        var result = await StoryChiefWebhookEndpoint.HandleAsync(
            context.Request,
            Options.Create(options),
            publisher,
            CancellationToken.None);

        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);

        return new WebhookResponse(
            context.Response.StatusCode,
            context.Response.ContentType,
            await reader.ReadToEndAsync());
    }

    private static string SignRequest(string unsignedBody)
    {
        string mac = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(SigningKey),
            Encoding.UTF8.GetBytes(unsignedBody))).ToLowerInvariant();
        const string metadataEnd = "},\"data\":";

        return unsignedBody.Replace(metadataEnd, $",\"mac\":\"{mac}\"}},\"data\":", StringComparison.Ordinal);
    }

    private static bool HasValidResponseSignature(string body)
    {
        int macStart = body.LastIndexOf(",\"mac\":\"", StringComparison.Ordinal);
        if (macStart < 0 || !body.EndsWith("\"}", StringComparison.Ordinal))
        {
            return false;
        }

        const int prefixLength = 8;
        int valueStart = macStart + prefixLength;
        string suppliedMac = body[valueStart..^2];
        string unsignedBody = $"{body[..macStart]}}}";
        string expectedMac = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(SigningKey),
            Encoding.UTF8.GetBytes(unsignedBody))).ToLowerInvariant();

        return suppliedMac.Equals(expectedMac, StringComparison.Ordinal);
    }

    private sealed record WebhookResponse(int StatusCode, string? ContentType, string Body);

    private sealed class RecordingPublisher : IStoryChiefContentPublisher
    {
        public StoryChiefPublishResult Result { get; init; } = new("1", null);

        public int PublishCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public int DeleteCalls { get; private set; }

        public int TotalCalls => PublishCalls + UpdateCalls + DeleteCalls;

        public JsonElement LastStory { get; private set; }

        public StoryChiefPublishingContext? LastContext { get; private set; }

        public Task<StoryChiefPublishResult> PublishAsync(
            JsonElement story,
            StoryChiefPublishingContext context,
            CancellationToken cancellationToken)
        {
            PublishCalls++;
            LastStory = story;
            LastContext = context;
            return Task.FromResult(Result);
        }

        public Task<StoryChiefPublishResult> UpdateAsync(
            JsonElement story,
            StoryChiefPublishingContext context,
            CancellationToken cancellationToken)
        {
            UpdateCalls++;
            LastStory = story;
            LastContext = context;
            return Task.FromResult(Result);
        }

        public Task<StoryChiefPublishResult> DeleteAsync(
            JsonElement story,
            StoryChiefPublishingContext context,
            CancellationToken cancellationToken)
        {
            DeleteCalls++;
            LastStory = story;
            LastContext = context;
            return Task.FromResult(Result);
        }
    }
}
