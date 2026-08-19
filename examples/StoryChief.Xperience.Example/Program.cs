using Kentico.Web.Mvc;

using StoryChief.Xperience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKentico(_ => { });

builder.Services.AddStoryChiefXperience(options =>
{
    var configuration = builder.Configuration.GetSection(StoryChiefXperienceOptions.SectionName);
    var pageConfiguration = configuration.GetSection(nameof(StoryChiefXperienceOptions.Page));

    options.SigningKey = configuration[nameof(StoryChiefXperienceOptions.SigningKey)] ?? string.Empty;
    options.Page.WebsiteChannelName = pageConfiguration[nameof(StoryChiefPageOptions.WebsiteChannelName)] ?? string.Empty;
    options.Page.ContentTypeName = pageConfiguration[nameof(StoryChiefPageOptions.ContentTypeName)] ?? string.Empty;
    options.Page.LanguageName = pageConfiguration[nameof(StoryChiefPageOptions.LanguageName)] ?? "en";
    options.Page.AuditUserName = pageConfiguration[nameof(StoryChiefPageOptions.AuditUserName)] ?? "Administrator";

    options.Page.MapField("title", "ArticleTitle");
    options.Page.MapField("content", "ArticleContent");
    options.Page.MapField("excerpt", "ArticleExcerpt");
    options.Page.MapField("seo_title", "ArticleSeoTitle");
    options.Page.MapField("seo_description", "ArticleSeoDescription");
    options.Page.MapField("published_at", "ArticlePublishedAt", StoryChiefFieldValueKind.DateTime);
});

var app = builder.Build();

app.InitKentico();
app.UseKentico();

app.MapStoryChiefWebhook();
app.MapGet("/", () => "StoryChief for Xperience by Kentico example");

await app.RunAsync();
