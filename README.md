# StoryChief for Xperience by Kentico

[![CI: Build and Test](https://github.com/Story-Chief/storychief-xperience-by-kentico/actions/workflows/ci.yml/badge.svg)](https://github.com/Story-Chief/storychief-xperience-by-kentico/actions/workflows/ci.yml)

An open-source Xperience by Kentico integration for receiving authenticated publishing webhooks from StoryChief.

> [!IMPORTANT]
> This package is under active development and is not published to NuGet yet.

## Requirements

| Xperience version | Package version |
| --- | --- |
| >= 31.7.4 | 1.0.0 prerelease |

- ASP.NET Core 8.0
- Xperience by Kentico 31.7.4 or newer
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
    options.Page.PageTemplateIdentifier = "Acme.Article";
    options.Page.LanguageName = "en";
    options.Page.MapLanguage("en", "en");
    options.Page.MapLanguage("nl", "nl-BE");
    options.Page.MapField("title", "ArticleTitle");
    options.Page.MapField("content", "ArticleContent");
    options.Page.MapField("excerpt", "ArticleExcerpt");
    options.Page.MapField("published_at", "ArticlePublishedAt", StoryChiefFieldValueKind.DateTime);
});

// After app creation and the usual middleware registrations:
app.MapStoryChiefWebhook();
```

Store the key outside source control using user secrets, environment variables, or your production secret store.

The target must be an existing page content type whose fields match the configured value types. Page-template content types can set `PageTemplateIdentifier`; controller-rendered types leave it empty. See the [Usage Guide](./docs/Usage-Guide.md) for parent-page, audit-user, deletion, nested-field, and custom-publisher configuration.

For a complete, buildable host project, see the [example Xperience application](./examples/StoryChief.Xperience.Example/README.md).

## Current scope

- `POST /storychief/webhook`
- SHA-256 HMAC validation compatible with StoryChief's PHP webhook contract
- Signed connection-test metadata
- Website page creation, draft updates, publishing, URL resolution, and deletion through Kentico's public APIs
- Configurable mapping from StoryChief fields to Xperience field code names
- Support for publishing as draft and status-only updates
- Multilingual page variants using StoryChief translation relationships and configurable language mappings
- An `IStoryChiefContentPublisher` extension point for advanced media, taxonomy, author, or project-specific mapping
- Request-size limit and safe error responses
- A buildable Xperience host example and endpoint-level webhook tests

Future asset support will target [reusable content item assets in Content hub](https://docs.kentico.com/documentation/business-users/content-hub/content-item-assets). Kentico's sunset media-library APIs will not be used.

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) and the [contributor setup guide](./docs/Contributing-Setup.md).

## License

Distributed under the MIT License. See [LICENSE.md](./LICENSE.md).

## Support

This is a StoryChief-maintained integration and is not yet listed as an official Kentico integration. See [SUPPORT.md](./SUPPORT.md) for support boundaries and reporting channels.

Please report security vulnerabilities according to [SECURITY.md](./SECURITY.md).
