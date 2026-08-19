using System.Net;
using System.Text;
using System.Text.Json;

namespace StoryChief.Xperience.Tests;

public sealed class StoryChiefCoverImageTests
{
    [Test]
    public void ParserReadsStoryChiefFeaturedImageMetadata()
    {
        using var payload = JsonDocument.Parse("""
            {
              "featured_image": {
                "url": "https://images.storychief.test/covers/article.png",
                "name": "article.png",
                "alt": "Coffee being poured"
              }
            }
            """);

        var image = StoryChiefCoverImageParser.Parse(payload.RootElement);

        Assert.Multiple(() =>
        {
            Assert.That(image.State, Is.EqualTo(StoryChiefCoverImageState.Present));
            Assert.That(image.Url, Is.EqualTo(new Uri("https://images.storychief.test/covers/article.png")));
            Assert.That(image.Name, Is.EqualTo("article.png"));
            Assert.That(image.AltText, Is.EqualTo("Coffee being poured"));
        });
    }

    [TestCase("{}", "Unspecified")]
    [TestCase("{\"featured_image\":null}", "Removed")]
    [TestCase("{\"featured_image\":{}}", "Removed")]
    public void ParserDistinguishesMissingAndRemovedImages(string json, string expected)
    {
        using var payload = JsonDocument.Parse(json);

        var image = StoryChiefCoverImageParser.Parse(payload.RootElement);

        Assert.That(image.State.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void ParserRejectsNonHttpsImageUrls()
    {
        using var payload = JsonDocument.Parse("""
            {"featured_image":{"url":"http://images.storychief.test/article.png"}}
            """);

        var exception = Assert.Throws<JsonException>(() =>
            StoryChiefCoverImageParser.Parse(payload.RootElement));

        Assert.That(exception!.Message, Does.Contain("HTTPS"));
    }

    [Test]
    public async Task DownloaderFollowsValidatedRedirectAndReturnsBoundedImage()
    {
        byte[] content = Encoding.UTF8.GetBytes("test-image");
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/original")
            {
                return new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://cdn.storychief.test/final") },
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
                {
                    Headers = { ContentType = new("image/png") },
                },
            };
        });
        var validator = new RecordingUrlValidator();
        var downloader = new StoryChiefCoverImageDownloader(new HttpClient(handler), validator);
        var image = new StoryChiefCoverImageInput(
            StoryChiefCoverImageState.Present,
            new Uri("https://images.storychief.test/original"),
            "Article hero.png",
            "Alternative text");

        var result = await downloader.DownloadAsync(image, 1024, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Is.EqualTo(content));
            Assert.That(result.Name, Is.EqualTo("Article-hero.png"));
            Assert.That(result.Extension, Is.EqualTo(".png"));
            Assert.That(validator.ValidatedUris, Is.EqualTo(new[]
            {
                new Uri("https://images.storychief.test/original"),
                new Uri("https://cdn.storychief.test/final"),
            }));
        });
    }

    [Test]
    public void DownloaderRejectsResponsesThatExceedTheConfiguredLimit()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[5])
            {
                Headers = { ContentType = new("image/jpeg") },
            },
        });
        var downloader = new StoryChiefCoverImageDownloader(new HttpClient(handler), new RecordingUrlValidator());
        var image = new StoryChiefCoverImageInput(
            StoryChiefCoverImageState.Present,
            new Uri("https://images.storychief.test/large.jpg"));

        var exception = Assert.ThrowsAsync<JsonException>(() =>
            downloader.DownloadAsync(image, 4, CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("4-byte limit"));
    }

    [Test]
    public async Task DownloaderUsesTheResponseImageTypeForTheStoredExtension()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
            {
                Headers = { ContentType = new("image/png") },
            },
        });
        var downloader = new StoryChiefCoverImageDownloader(new HttpClient(handler), new RecordingUrlValidator());
        var image = new StoryChiefCoverImageInput(
            StoryChiefCoverImageState.Present,
            new Uri("https://images.storychief.test/cover"),
            "misleading.php");

        var result = await downloader.DownloadAsync(image, 1024, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Extension, Is.EqualTo(".png"));
            Assert.That(result.Name, Is.EqualTo("misleading.png"));
        });
    }

    [Test]
    public void ManagedAssetNameIsStableAndLanguageSpecific()
    {
        using var payload = JsonDocument.Parse("""
            {"storychief_id":"story-123"}
            """);

        string english = StoryChiefCoverImageManager.GetContentItemName(
            payload.RootElement,
            "AcmeWebsite",
            "en");
        string dutch = StoryChiefCoverImageManager.GetContentItemName(
            payload.RootElement,
            "AcmeWebsite",
            "nl-BE");

        Assert.Multiple(() =>
        {
            Assert.That(english, Is.EqualTo("StoryChiefCover_EA8CBFE573B1326B145FB3DC"));
            Assert.That(dutch, Is.Not.EqualTo(english));
        });
    }

    [TestCase("127.0.0.1")]
    [TestCase("10.2.3.4")]
    [TestCase("172.16.0.1")]
    [TestCase("192.168.1.1")]
    [TestCase("169.254.10.2")]
    [TestCase("::1")]
    [TestCase("fc00::1")]
    public void RemoteUrlValidatorRejectsNonPublicAddresses(string value) => Assert.That(
            StoryChiefRemoteImageUrlValidator.IsNonPublicAddress(IPAddress.Parse(value)),
            Is.True);

    private sealed class RecordingUrlValidator : IStoryChiefRemoteImageUrlValidator
    {
        public List<Uri> ValidatedUris { get; } = [];

        public Task ValidateAsync(Uri uri, CancellationToken cancellationToken)
        {
            ValidatedUris.Add(uri);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
    }
}
