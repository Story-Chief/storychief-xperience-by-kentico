# Release process

Releases are built from tags on `main` and published by `.github/workflows/release.yml`.

## One-time configuration

1. Create and verify the `StoryChief` organization on NuGet.org.
2. Create a GitHub environment named `release` and require maintainer approval.
3. Add an environment variable named `NUGET_USER` containing the NuGet.org username used for Trusted Publishing.
4. In NuGet.org, create a Trusted Publishing policy with:
   - Package owner: `StoryChief`
   - Provider: GitHub Actions
   - Repository owner: `Story-Chief`
   - Repository: `storychief-xperience-by-kentico`
   - Workflow file: `release.yml`
   - Environment: `release`
   - Package scope: `StoryChief.Xperience`
   - Permission: push new packages and package versions

Do not create or store a long-lived NuGet API key. The workflow exchanges GitHub's OIDC identity for a short-lived key during each approved release.

## Prepare a release

1. Update `VersionPrefix` and add or remove `VersionSuffix` as appropriate in `Directory.Build.props`.
2. Add the release notes and date to `CHANGELOG.md`.
3. Update the package version shown in `README.md`.
4. Open a pull request and confirm formatting, build, tests, packaging, and clean-package installation pass.
5. Merge the pull request into `main` and wait for the `main` workflow to pass.

## Publish

Create and push a tag that exactly matches the package version with a `v` prefix:

```bash
git tag -a v1.0.0 -m "StoryChief.Xperience 1.0.0"
git push origin v1.0.0
```

Approve the `release` environment deployment. The workflow validates the tag, rebuilds and tests the solution, verifies installation from the generated package, publishes the package and symbols to NuGet.org, and creates the corresponding GitHub release.

After publication, verify the NuGet package page, install the package into a clean Xperience project, and run a signed StoryChief connection test before promoting a release candidate to a stable version.
