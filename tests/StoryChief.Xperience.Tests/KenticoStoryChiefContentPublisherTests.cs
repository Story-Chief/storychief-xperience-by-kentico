using CMS.ContentEngine;

namespace StoryChief.Xperience.Tests;

public sealed class KenticoStoryChiefContentPublisherTests
{
    [Test]
    public async Task LockedPublishAcceptsAnAlreadyPublishedPage()
    {
        int statusChecks = 0;

        bool published = await KenticoStoryChiefContentPublisher.TryPublishOrConfirmPublished(
            () => Task.FromResult(false),
            () =>
            {
                statusChecks++;
                return Task.FromResult(VersionStatus.Published);
            },
            acceptAlreadyPublished: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(published, Is.True);
            Assert.That(statusChecks, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task FailedPublishStillFailsForNonPublishedPage()
    {
        bool published = await KenticoStoryChiefContentPublisher.TryPublishOrConfirmPublished(
            () => Task.FromResult(false),
            () => Task.FromResult(VersionStatus.Draft),
            acceptAlreadyPublished: true);

        Assert.That(published, Is.False);
    }

    [Test]
    public async Task SuccessfulPublishDoesNotCheckExistingState()
    {
        int statusChecks = 0;

        bool published = await KenticoStoryChiefContentPublisher.TryPublishOrConfirmPublished(
            () => Task.FromResult(true),
            () =>
            {
                statusChecks++;
                return Task.FromResult(VersionStatus.Draft);
            },
            acceptAlreadyPublished: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(published, Is.True);
            Assert.That(statusChecks, Is.Zero);
        }
    }

    [Test]
    public async Task RegularPublishDoesNotTreatExistingStateAsSuccess()
    {
        int statusChecks = 0;

        bool published = await KenticoStoryChiefContentPublisher.TryPublishOrConfirmPublished(
            () => Task.FromResult(false),
            () =>
            {
                statusChecks++;
                return Task.FromResult(VersionStatus.Published);
            },
            acceptAlreadyPublished: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(published, Is.False);
            Assert.That(statusChecks, Is.Zero);
        }
    }
}
