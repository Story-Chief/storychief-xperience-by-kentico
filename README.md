# StoryChief for Xperience by Kentico

[![CI: Build and Test](https://github.com/Story-Chief/storychief-xperience-by-kentico/actions/workflows/ci.yml/badge.svg)](https://github.com/Story-Chief/storychief-xperience-by-kentico/actions/workflows/ci.yml)

An open-source Xperience by Kentico integration for receiving authenticated publishing webhooks from StoryChief.

> [!IMPORTANT]
> This package is under active development. Webhook authentication, connection checks, and the content-publisher extension point are implemented. The default Xperience content-type mapping is the next milestone.

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
});

// After app creation and the usual middleware registrations:
app.MapStoryChiefWebhook();
```

Store the key outside source control using user secrets, environment variables, or your production secret store.

Projects provide an `IStoryChiefContentPublisher` implementation to map StoryChief's generic story fields to their Xperience content type. See the [Usage Guide](./docs/Usage-Guide.md).

## Current scope

- `POST /storychief/webhook`
- SHA-256 HMAC validation compatible with StoryChief's PHP webhook contract
- Signed connection-test metadata
- `publish`, `update`, and `delete` dispatch to `IStoryChiefContentPublisher`
- Request-size limit and safe error responses

## Contributing

See [Contributing Setup](./docs/Contributing-Setup.md).

## License

Distributed under the MIT License. See [LICENSE.md](./LICENSE.md).

## Support

This is a StoryChief-maintained integration and is not yet listed as an official Kentico integration. Please use this repository's issue tracker during development.
