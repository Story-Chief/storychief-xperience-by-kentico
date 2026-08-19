# Usage guide

## Register the webhook

Add the services and endpoint to the Xperience application's `Program.cs`:

```csharp
builder.Services.AddStoryChiefXperience(options =>
{
    options.SigningKey = builder.Configuration["StoryChief:SigningKey"]
        ?? throw new InvalidOperationException("StoryChief:SigningKey is missing.");
});

var app = builder.Build();

// Register the normal Xperience and ASP.NET Core middleware first.
app.MapStoryChiefWebhook();
```

The default endpoint is `/storychief/webhook`. A custom path can be supplied to `MapStoryChiefWebhook`.

Configure the public HTTPS URL as the webhook endpoint in StoryChief. The signing key entered in the Xperience application's secret configuration must be the same key used by that StoryChief destination.

## Map StoryChief stories to an Xperience content type

Xperience projects define their own content types and field code names, so the initial package exposes a strongly bounded publishing adapter instead of assuming a schema. Implement `IStoryChiefContentPublisher`:

```csharp
public sealed class ArticlePublisher : IStoryChiefContentPublisher
{
    public Task<StoryChiefPublishResult> PublishAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken)
    {
        // Map title, content, excerpt, SEO fields, taxonomy, author, and media
        // to the project's generated Xperience content-type model.
        throw new NotImplementedException();
    }

    public Task<StoryChiefPublishResult> UpdateAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<StoryChiefPublishResult> DeleteAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}
```

Register the implementation after `AddStoryChiefXperience`:

```csharp
builder.Services.AddScoped<IStoryChiefContentPublisher, ArticlePublisher>();
```

Until a publisher is registered, connection tests succeed but publishing events return HTTP 501 with an explicit configuration error.

## StoryChief fields

The `story` JSON object can include:

- `external_id` and `storychief_id`
- `title`, `content`, and `excerpt`
- `seo_title`, `seo_description`, and `seo_slug`
- `featured_image`
- `author`, `tags`, `category`, and `categories`
- `language`, `canonical`, and `published_at`
- `custom_fields`

The publishing context includes the event, requested status, and `lock_updates` flag. Treat all fields as optional except those required by your own content model.
