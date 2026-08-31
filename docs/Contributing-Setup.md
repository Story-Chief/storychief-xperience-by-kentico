# Contributing setup

## Requirements

- .NET SDK 10.0.103 or a compatible SDK selected by `global.json`
- Docker, if you prefer an isolated SDK

## Build and test

```bash
dotnet restore
dotnet format StoryChief.Xperience.slnx --exclude ./examples/** --verify-no-changes
dotnet build StoryChief.Xperience.slnx --configuration Release --no-restore
dotnet test StoryChief.Xperience.slnx --configuration Release --no-build --no-restore
```

The test suite includes fixtures generated with PHP's default `json_encode` behavior. Keep these tests when changing webhook authentication or response serialization because StoryChief's signing contract depends on byte-compatible JSON.

The solution also builds `examples/StoryChief.Xperience.Example`. The example intentionally does not include an Xperience database or project-specific content type. See its README for the configuration required to run it.

## Xperience compatibility

`KenticoXperienceVersion` in `Directory.Packages.props` is the oldest Xperience version supported by the NuGet package. Do not raise this baseline merely because a newer Xperience refresh is available, since doing so also raises the package's minimum dependency requirement.

Kentico packages are intentionally excluded from Dependabot version updates. When validating a newer Xperience release, update the dedicated compatibility job and the commands below without changing the minimum-version property.

CI validates the locked baseline and runs a separate compatibility build against the latest supported refresh. To reproduce the latest-version check, override the property consistently during restore, build, and test:

```bash
dotnet restore --force-evaluate -p:KenticoXperienceVersion=31.8.2
dotnet build StoryChief.Xperience.slnx --configuration Release --no-restore -p:KenticoXperienceVersion=31.8.2
dotnet test StoryChief.Xperience.slnx --configuration Release --no-build --no-restore -p:KenticoXperienceVersion=31.8.2
```

The forced restore updates lock files in the working tree. Do not commit those compatibility-only lock-file changes unless the minimum supported Xperience version is intentionally being raised.
