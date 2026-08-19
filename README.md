# StoryChief for Xperience by Kentico

[![CI: Build and Test](https://github.com/Story-Chief/storychief-xperience-by-kentico/actions/workflows/ci.yml/badge.svg)](https://github.com/Story-Chief/storychief-xperience-by-kentico/actions/workflows/ci.yml)

An open-source Xperience by Kentico integration for receiving authenticated publishing webhooks from StoryChief.

> [!IMPORTANT]
> This package is under active development and is not published to NuGet yet.

## Requirements

| Xperience version | Package version |
| ----------------- | --------------- |
| >= 31.7.1         | 1.0.0 prerelease |

- ASP.NET Core 8.0
- Xperience by Kentico 31.7.1 or newer
- A StoryChief webhook destination and signing key

## Development installation

The package is not published to NuGet yet. Reference `src/StoryChief.Xperience/StoryChief.Xperience.csproj` from an Xperience application while developing locally.

Register the integration in `Program.cs`:

```csharp
builder.Services.AddStoryChiefXperience(options =>
{
    options.SigningKey = builder.Configuration["StoryChief:SigningKey"]
        ?? throw new InvalidOperationException("StoryChief:SigningKey is missing.");

    options.Page.WebsiteChannelName = "AcmeWebsite";
    options.Page.ContentTypeName = "Acme.ArticlePage";
    options.Page.LanguageName = "en";
    options.Page.MapField("title", "ArticleTitle");
    options.Page.MapField("content", "ArticleContent");
    options.Page.MapField("excerpt", "ArticleExcerpt");
    options.Page.MapField("published_at", "ArticlePublishedAt", StoryChiefFieldValueKind.DateTime);
});

// After app creation and the usual middleware registrations:
app.MapStoryChiefWebhook();
```

Store the key outside source control using user secrets, environment variables, or your production secret store.

The target must be an existing page content type whose fields match the configured value types. See the [Usage Guide](./docs/Usage-Guide.md) for parent-page, audit-user, deletion, nested-field, and custom-publisher configuration.

## Current scope

- `POST /storychief/webhook`
- SHA-256 HMAC validation compatible with StoryChief's PHP webhook contract
- Signed connection-test metadata
- Website page creation, draft updates, publishing, URL resolution, and deletion through Kentico's public APIs
- Configurable mapping from StoryChief fields to Xperience field code names
- Support for publishing as draft and status-only updates
- An `IStoryChiefContentPublisher` extension point for advanced media, taxonomy, author, or project-specific mapping
- Request-size limit and safe error responses

## Contributing

See [Contributing Setup](./docs/Contributing-Setup.md).

## License

Distributed under the MIT License. See [LICENSE.md](./LICENSE.md).

## Support

This is a StoryChief-maintained integration and is not yet listed as an official Kentico integration. Please use this repository's issue tracker during development.
