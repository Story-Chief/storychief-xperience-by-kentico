# Example Xperience application

This minimal host demonstrates how an Xperience by Kentico application installs, configures, and maps the StoryChief webhook package.

Before running it:

1. Add an Xperience `CMSConnectionString` using user secrets or local configuration.
2. Set `StoryChief:SigningKey` with user secrets.
3. Replace the sample channel, content type, and field code names in `appsettings.json` and `Program.cs` with values from the target Xperience project.
4. Initialize or restore the Xperience database using the normal Xperience tooling.

```bash
dotnet user-secrets set "ConnectionStrings:CMSConnectionString" "<connection-string>"
dotnet user-secrets set "StoryChief:SigningKey" "<signing-key>"
dotnet run
```

The StoryChief endpoint is available at `/storychief/webhook`.
