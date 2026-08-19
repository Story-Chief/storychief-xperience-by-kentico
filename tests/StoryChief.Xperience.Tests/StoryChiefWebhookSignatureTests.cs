using System.Text;

namespace StoryChief.Xperience.Tests;

public sealed class StoryChiefWebhookSignatureTests
{
    private const string SigningKey = "test-secret";

    [Test]
    public void TryValidateAcceptsPayloadSignedByPhpJsonEncode()
    {
        const string body = """
            {"data":{"title":"Caf\u00e9 \/ launch","content":"<p>Hello \ud83d\ude00<\/p>"},"meta":{"event":"publish","status":"draft","mac":"dc13e9001234575c4dd5ede9fb123c0d31ceafaa79665949e171396cf21e2bb0"}}
            """;

        bool isValid = StoryChiefWebhookSignature.TryValidate(
            Encoding.UTF8.GetBytes(body),
            SigningKey,
            out var payload);

        using (payload)
        {
            Assert.That(isValid, Is.True);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!.RootElement.GetProperty("data").GetProperty("title").GetString(), Is.EqualTo("Café / launch"));
        }
    }

    [Test]
    public void TryValidateRejectsModifiedPayload()
    {
        const string body = """
            {"data":{"title":"Modified"},"meta":{"event":"publish","status":"draft","mac":"dc13e9001234575c4dd5ede9fb123c0d31ceafaa79665949e171396cf21e2bb0"}}
            """;

        bool isValid = StoryChiefWebhookSignature.TryValidate(
            Encoding.UTF8.GetBytes(body),
            SigningKey,
            out var payload);

        using (payload)
        {
            Assert.That(isValid, Is.False);
            Assert.That(payload, Is.Null);
        }
    }

    [Test]
    public void SignMatchesPhpJsonEncodeForUrlsAndUnicode()
    {
        var response = new Dictionary<string, object?>
        {
            ["id"] = "123",
            ["permalink"] = "https://example.com/café",
            ["status"] = "published",
        };

        string signedBody = Encoding.UTF8.GetString(StoryChiefWebhookSignature.Sign(response, SigningKey));

        Assert.That(signedBody, Is.EqualTo(
            "{\"id\":\"123\",\"permalink\":\"https:\\/\\/example.com\\/caf\\u00e9\",\"status\":\"published\",\"mac\":\"241f2687542796b038ad4601463525237b475183802627a4ca00443179ef68fd\"}"));
    }
}
