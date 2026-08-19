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
    options.Page.PageTemplateIdentifier = "Acme.Article";
    options.Page.LanguageName = "en";
    options.Page.MapLanguage("en", "en");
    options.Page.MapLanguage("nl", "nl-BE");
    options.Page.ParentWebPageItemId = 0;
    options.Page.AuditUserName = "Administrator";

    options.Page.MapField("title", "ArticleTitle");
    options.Page.MapField("content", "ArticleContent");
    options.Page.MapField("excerpt", "ArticleExcerpt");
    options.Page.MapField("seo_title", "ArticleSeoTitle");
    options.Page.MapField("seo_description", "ArticleSeoDescription");
    options.Page.MapField("published_at", "ArticlePublishedAt", StoryChiefFieldValueKind.DateTime);

    options.Page.MapTaxonomy("tags", "ArticleTags", "ArticleTaxonomy");
    options.Page.MapTaxonomy("category", "ArticlePrimaryCategory", "ArticleCategories");
    options.Page.MapTaxonomy("categories", "ArticleCategories", "ArticleCategories");

    options.Page.CoverImage.ContentTypeName = "Acme.Image";
    options.Page.CoverImage.AssetFieldName = "ImageFile";
    options.Page.CoverImage.PageFieldName = "ArticleCoverImage";
    options.Page.CoverImage.AltTextFieldName = "ImageAltText";
    options.Page.CoverImage.WorkspaceName = "Acme.Content";
});

var app = builder.Build();

// Register the normal Xperience and ASP.NET Core middleware first.
app.MapStoryChiefWebhook();
```

The default endpoint is `/storychief/webhook`. A custom path can be supplied to `MapStoryChiefWebhook`.

Configure the public HTTPS URL as the webhook endpoint in StoryChief. The signing key entered in the Xperience application's secret configuration must be the same key used by that StoryChief destination.

### Configuration-based registration

The integration can alternatively bind all options from an `IConfiguration` section:

```csharp
builder.Services.AddStoryChiefXperience(
    builder.Configuration.GetSection(StoryChiefXperienceOptions.SectionName));
```

```json
{
  "StoryChief": {
    "SigningKey": "",
    "MaxRequestBodyBytes": 10485760,
    "Page": {
      "WebsiteChannelName": "AcmeWebsite",
      "ContentTypeName": "Acme.ArticlePage",
      "PageTemplateIdentifier": "Acme.Article",
      "LanguageName": "en",
      "LanguageMappings": {
        "en": "en",
        "nl": "nl-BE"
      },
      "AuditUserName": "storychief-integration",
      "CoverImage": {
        "ContentTypeName": "Acme.Image",
        "AssetFieldName": "ImageFile",
        "PageFieldName": "ArticleCoverImage",
        "AltTextFieldName": "ImageAltText",
        "WorkspaceName": "Acme.Content",
        "MaxFileSizeBytes": 10485760
      },
      "TaxonomyMappings": {
        "tags": {
          "XperienceFieldName": "ArticleTags",
          "TaxonomyName": "ArticleTaxonomy",
          "CreateMissingTags": true,
          "TagMappings": {
            "2769118967": "GoGreen"
          }
        }
      },
      "FieldMappings": {
        "title": {
          "XperienceFieldName": "ArticleTitle",
          "ValueKind": "String"
        },
        "published_at": {
          "XperienceFieldName": "ArticlePublishedAt",
          "ValueKind": "DateTime"
        }
      }
    }
  }
}
```

Keep `SigningKey` empty in committed configuration and supply it through user secrets, environment variables, or the production secret store.

## Configure the target page

The integration creates website pages using Kentico's `IWebPageManager` API. Configure:

- `WebsiteChannelName` with the channel code name, not its display name.
- `ContentTypeName` with the full code name of an existing page content type.
- `PageTemplateIdentifier` with the identifier of the default page template when the content type is rendered through page templates. Leave it empty for content types rendered by a dedicated controller.
- `LanguageName` with an enabled Xperience content-language code name.
- `LanguageMappings` to opt into multilingual publishing by mapping every expected StoryChief language code to an enabled Xperience content-language code name.
- `ParentWebPageItemId` with the target folder or page ID. The default `0` creates pages at the channel root.
- `AuditUserName` with the Xperience user recorded in audit fields. It defaults to `Administrator`; a dedicated enabled integration user is recommended.
- `PermanentlyDelete` to control whether delete events bypass the recycle bin. It defaults to `false`.

The StoryChief `seo_slug` is automatically used as the page URL slug. The created Xperience page ID is returned to StoryChief as `external_id`, which is then used for updates and deletes.

## Publish translations

When `LanguageMappings` is empty, every story continues to use `LanguageName`. Once at least one mapping is configured, every incoming StoryChief language must have an explicit mapping; unmapped languages are rejected instead of being written into the wrong variant.

Publish the StoryChief source story before its translations. Translation webhooks include the source destination's `external_id`, which the integration uses to create an Xperience language variant on the same page. All variants therefore share one Xperience page ID while keeping their own content, workflow state, and public URL path.

On translation updates, `seo_slug` updates only that language variant's public URL. The shared content-tree path remains owned by the source story.

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

## Publish taxonomy tags and categories

[Xperience taxonomy](https://docs.kentico.com/documentation/developers-and-admins/configuration/taxonomies) mapping supports StoryChief's `tags`, `category`, and `categories` payloads. Prepare the content model with an existing taxonomy and a taxonomy field on the target page type, then map the StoryChief property to both code names:

```csharp
var tags = options.Page.MapTaxonomy("tags", "ArticleTags", "ArticleTaxonomy");
var primaryCategory = options.Page.MapTaxonomy(
    "category",
    "ArticlePrimaryCategory",
    "ArticleCategories");
options.Page.MapTaxonomy("categories", "ArticleCategories", "ArticleCategories");
```

Each mapping handles the StoryChief wrapper shape (`tags.data`, `category.data`, or `categories.data`). A missing property leaves the Xperience field unchanged, while an empty array or `null` clears the configured taxonomy field. Updates replace the field's complete tag selection.

Terms are resolved in this order:

1. An explicit mapping from the StoryChief `storychief_id`, slug, or name to an Xperience tag code name.
2. A previously created integration-managed tag with the same stable StoryChief identifier.
3. A unique existing tag whose code name matches the StoryChief slug or name, or whose localized title matches the StoryChief name.
4. A new integration-managed tag when `CreateMissingTags` is enabled, which is the default.

Use explicit mappings when an established Xperience tag has a different code name:

```csharp
tags.MapTag("2769118967", "GoGreen");
tags.MapTag("sustainability", "SustainableContent");
```

Automatically created tags use deterministic code names, so subsequent updates and translated variants reuse them. Their titles are updated in the current Xperience language. The integration never creates or deletes taxonomy groups, never deletes tags when an article is removed, and never renames explicitly mapped or otherwise user-managed tags.

Set `CreateMissingTags` to `false` to require every incoming term to match an existing, previously integration-managed, or explicitly mapped tag. The setting prevents new tag creation; it does not disable stable reuse of tags the integration created earlier. Missing and ambiguous matches are reported as configuration errors instead of being silently ignored.

Do not also map the same StoryChief property and Xperience destination field through `MapField`; taxonomy mappings write Xperience `IEnumerable<TagReference>` values rather than JSON text.

## Publish cover images

Cover-image support uses Xperience [content item assets](https://docs.kentico.com/documentation/business-users/content-hub/content-item-assets), not obsolete media libraries. Prepare the content model with:

1. A reusable image content type with a `Content item asset` field.
2. An optional text field on that image type for alternative text.
3. A `Content items` field on the target page type that allows the reusable image type and accepts one item.

Configure the corresponding code names:

```csharp
options.Page.CoverImage.ContentTypeName = "Acme.Image";
options.Page.CoverImage.AssetFieldName = "ImageFile";
options.Page.CoverImage.PageFieldName = "ArticleCoverImage";
options.Page.CoverImage.AltTextFieldName = "ImageAltText";
options.Page.CoverImage.WorkspaceName = "Acme.Content";
options.Page.CoverImage.MaxFileSizeBytes = 10 * 1024 * 1024;
```

Leave all cover-image code names empty to disable the feature. `WorkspaceName` must identify a Content hub workspace in which the reusable image type is available. Setting only some required values is treated as a configuration error.

For each StoryChief story and language, the integration creates one deterministically named reusable image item. Publishing and updates reuse that item, replace its binary and alternative text, and link it from the language-specific page variant. A published update with an empty `featured_image` removes the page link and permanently deletes the integration-owned image item. Deleting the page variant also deletes its managed image item. Status-only updates made with `lock_updates` do not change the cover image.

Remote downloads must use HTTPS, resolve to public network addresses, return an `image/*` content type, and stay within `MaxFileSizeBytes`. Redirect targets receive the same validation.

## Advanced project-specific mapping

Direct mapping intentionally does not create author content items or arbitrary linked content. Projects that need those features can replace the default publisher with an `IStoryChiefContentPublisher` implementation:

```csharp
public sealed class ArticlePublisher : IStoryChiefContentPublisher
{
    public Task<StoryChiefPublishResult> PublishAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken)
    {
        // Map title, content, excerpt, SEO fields, author, and project-specific content
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
