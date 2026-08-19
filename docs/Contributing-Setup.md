# Contributing setup

## Requirements

- .NET SDK 10.0.103 or a compatible SDK selected by `global.json`
- Docker, if you prefer an isolated SDK

## Build and test

```bash
dotnet restore
dotnet format StoryChief.Xperience.slnx --verify-no-changes
dotnet build StoryChief.Xperience.slnx --configuration Release --no-restore
dotnet test StoryChief.Xperience.slnx --configuration Release --no-build --no-restore
```

The test suite includes fixtures generated with PHP's default `json_encode` behavior. Keep these tests when changing webhook authentication or response serialization because StoryChief's signing contract depends on byte-compatible JSON.

The solution also builds `examples/StoryChief.Xperience.Example`. The example intentionally does not include an Xperience database or project-specific content type. See its README for the configuration required to run it.
