using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the StoryChief integration.
/// </summary>
public static class StoryChiefServiceCollectionExtensions
{
    /// <summary>
    /// Registers StoryChief webhook services.
    /// </summary>
    public static IServiceCollection AddStoryChiefXperience(
        this IServiceCollection services,
        Action<StoryChief.Xperience.StoryChiefXperienceOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);

        return AddStoryChiefPublisher(services);
    }

    /// <summary>
    /// Registers StoryChief webhook services using an application configuration section.
    /// </summary>
    public static IServiceCollection AddStoryChiefXperience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<StoryChief.Xperience.StoryChiefXperienceOptions>(configuration);

        return AddStoryChiefPublisher(services);
    }

    private static IServiceCollection AddStoryChiefPublisher(IServiceCollection services)
    {
        services.TryAddSingleton<StoryChief.Xperience.IStoryChiefRemoteImageUrlValidator,
            StoryChief.Xperience.StoryChiefRemoteImageUrlValidator>();
        services.AddHttpClient<StoryChief.Xperience.StoryChiefCoverImageDownloader>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("StoryChief.Xperience/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
            });
        services.TryAddScoped<StoryChief.Xperience.StoryChiefCoverImageManager>();
        services.TryAddScoped<StoryChief.Xperience.StoryChiefTaxonomyManager>();
        services.TryAddScoped<StoryChief.Xperience.IStoryChiefContentPublisher,
            StoryChief.Xperience.KenticoStoryChiefContentPublisher>();

        return services;
    }
}
