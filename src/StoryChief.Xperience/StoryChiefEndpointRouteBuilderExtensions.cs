using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Maps StoryChief webhook endpoints.
/// </summary>
public static class StoryChiefEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the authenticated StoryChief webhook receiver.
    /// </summary>
    public static IEndpointConventionBuilder MapStoryChiefWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/storychief/webhook")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapPost(pattern, StoryChief.Xperience.StoryChiefWebhookEndpoint.HandleAsync)
            .AllowAnonymous();
    }
}
