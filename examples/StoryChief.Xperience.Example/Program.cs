using Kentico.Web.Mvc;

using StoryChief.Xperience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKentico(_ => { });

builder.Services.AddStoryChiefXperience(
    builder.Configuration.GetSection(StoryChiefXperienceOptions.SectionName));

var app = builder.Build();

app.InitKentico();
app.UseKentico();

app.MapStoryChiefWebhook();
app.MapGet("/", () => "StoryChief for Xperience by Kentico example");

await app.RunAsync();
