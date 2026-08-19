# Usage guide

## Register the webhook

Add the services and endpoint to the Xperience application's `Program.cs`:

```csharp
builder.Services.AddStoryChiefXperience(options =>
{
    options.SigningKey = builder.Configuration["StoryChief:SigningKey"]
        ?? throw new InvalidOperationException("StoryChief:SigningKey is missing.");

    options.Page.WebsiteChannelName = "AcmeWebsite";
    options.Page.ContentTypeName = "Acme.ArticlePage";
    options.Page.LanguageName = "en";
    options.Page.ParentWebPageItemId = 0;
    options.Page.AuditUserName = "Administrator";

    options.Page.MapField("title", "ArticleTitle");
    options.Page.MapField("content", "ArticleContent");
    options.Page.MapField("excerpt", "ArticleExcerpt");
    options.Page.MapField("seo_title", "ArticleSeoTitle");
    options.Page.MapField("seo_description", "ArticleSeoDescription");
    options.Page.MapField("published_at", "ArticlePublishedAt", StoryChiefFieldValueKind.DateTime);
});

var app = builder.Build();

// Register the normal Xperience and ASP.NET Core middleware first.
app.MapStoryChiefWebhook();
```

The default endpoint is `/storychief/webhook`. A custom path can be supplied to `MapStoryChiefWebhook`.

Configure the public HTTPS URL as the webhook endpoint in StoryChief. The signing key entered in the Xperience application's secret configuration must be the same key used by that StoryChief destination.

## Configure the target page

The integration creates website pages using Kentico's `IWebPageManager` API. Configure:

- `WebsiteChannelName` with the channel code name, not its display name.
- `ContentTypeName` with the full code name of an existing page content type.
- `LanguageName` with an enabled Xperience content-language code name.
- `ParentWebPageItemId` with the target folder or page ID. The default `0` creates pages at the channel root.
- `AuditUserName` with the Xperience user recorded in audit fields. It defaults to `Administrator`; a dedicated enabled integration user is recommended.
- `PermanentlyDelete` to control whether delete events bypass the recycle bin. It defaults to `false`.

The StoryChief `seo_slug` is automatically used as the page URL slug. The created Xperience page ID is returned to StoryChief as `external_id`, which is then used for updates and deletes.

## Map StoryChief fields

Call `MapField` once for every value written to the page content type:

```csharp
options.Page.MapField("title", "ArticleTitle");
options.Page.MapField("content", "ArticleContent");
options.Page.MapField("custom_fields.reading_time", "ArticleReadingTime", StoryChiefFieldValueKind.Integer);
options.Page.MapField("published_at", "ArticlePublishedAt", StoryChiefFieldValueKind.DateTime);
options.Page.MapField("tags", "ArticleTagsJson", StoryChiefFieldValueKind.Json);
```

Nested property paths such as `custom_fields.reading_time` are supported. Missing and `null` values are skipped.

| Value kind | Behavior |
| ---------- | -------- |
| `Auto` | Preserves strings, booleans, and numbers; objects and arrays become JSON text. |
| `String` | Converts the value to text. |
| `Integer` | Requires a 32-bit JSON integer. |
| `Decimal` | Requires a JSON number that fits a .NET decimal. |
| `Boolean` | Requires a JSON boolean. |
| `DateTime` | Parses an ISO 8601 string and stores a UTC `DateTime`. |
| `Json` | Stores the raw JSON representation as text. |

The destination Xperience fields must accept the resulting .NET values. Field validation remains controlled by the configured Xperience content type.

## Advanced project-specific mapping

Direct mapping intentionally does not create Xperience assets, taxonomy tags, author content items, or other linked content. Projects that need those features can replace the default publisher with an `IStoryChiefContentPublisher` implementation:

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

Registrations added after `AddStoryChiefXperience` take precedence over the default page publisher.

## Publishing behavior

- `publish` creates a page draft and publishes it unless StoryChief requests draft status.
- `update` creates or updates the page draft, updates its URL slug, and publishes it when requested.
- `lock_updates` skips field and slug changes and only applies the requested status transition.
- `delete` moves the language variant to the recycle bin unless `PermanentlyDelete` is enabled.
- Published and draft responses return Xperience's absolute page URL to StoryChief.

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
