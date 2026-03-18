# Step Reference

## `downloadFile`

Downloads a file from a direct URL.

Fields:

- `url`
- `targetPath`
- `overwrite`

Example:

```json
{
  "type": "downloadFile",
  "name": "Download package",
  "url": "https://example.com/package.zip",
  "targetPath": "C:\\Temp\\package.zip",
  "overwrite": true
}
```

## `downloadGithubReleaseAsset`

Downloads a GitHub release asset, including private repository assets when a PAT is available.

Fields:

- `owner`
- `repository`
- `releaseTag`
- `assetName`
- `targetPath`
- `overwrite`
- `pat` optional override

Token resolution:

1. `step.pat`
2. `pullRequestProviders.github.pat`

Required GitHub permission:

- fine-grained PAT with `Contents: Read`

Example:

```json
{
  "type": "downloadGithubReleaseAsset",
  "name": "Download extension zip",
  "owner": "example-owner",
  "repository": "example-repository",
  "releaseTag": "v{{version}}",
  "assetName": "package-{{version}}.zip",
  "targetPath": "C:\\Downloads\\package-{{version}}.zip",
  "overwrite": true
}
```

## `downloadAzureDevopsPipelineArtifactAsset`

Downloads an Azure DevOps pipeline artifact or build artifact.

Supported lookup modes:

- `pipelineId` + `runId`
- `buildId`

Fields:

- `organization` optional override
- `project` optional override
- `pipelineId` optional when `buildId` is used
- `runId` optional when `buildId` is used
- `buildId` optional when `pipelineId` + `runId` are used
- `artifactName`
- `targetPath`
- `overwrite`
- `pat` optional override

Config fallback:

- `organization` falls back to `pullRequestProviders.azureDevOps.organization`
- `project` falls back to `pullRequestProviders.azureDevOps.project`
- `pat` falls back to `pullRequestProviders.azureDevOps.pat`

Note:

- the backend also accepts `downloadAzureDevopsPipelineArtifactAsset`
- this step downloads the artifact payload as a whole (for example a ZIP/package), not a single file inside the artifact

Example using pipeline run:

```json
{
  "type": "downloadAzureDevopsPipelineArtifactAsset",
  "name": "Download pipeline artifact",
  "organization": "my-org",
  "project": "MyProject",
  "pipelineId": "123",
  "runId": "{{runId}}",
  "artifactName": "drop",
  "targetPath": "C:\\Temp\\drop-{{runId}}.zip",
  "overwrite": true
}
```

Example using build ID:

```json
{
  "type": "downloadAzureDevopsPipelineArtifactAsset",
  "name": "Download build artifact",
  "buildId": "{{buildId}}",
  "artifactName": "drop",
  "targetPath": "C:\\Temp\\drop-{{buildId}}.zip",
  "overwrite": true
}
```

## `extractArchive`

Extracts a ZIP archive to a target directory.

Fields:

- `archivePath`
- `destinationPath`
- `cleanDestination`

Example:

```json
{
  "type": "extractArchive",
  "name": "Extract package",
  "archivePath": "C:\\Temp\\package.zip",
  "destinationPath": "C:\\Apps\\Package",
  "cleanDestination": true
}
```

## `runExecutable`

Runs any executable or installer. The legacy discriminator `runInstaller` is also accepted.

Fields:

- `filePath`
- `arguments`
- `waitForExit`
- `successExitCodes`
- `runElevated`

Example:

```json
{
  "type": "runExecutable",
  "name": "Run setup",
  "filePath": "C:\\Apps\\Package\\setup.exe",
  "arguments": ["/silent"],
  "waitForExit": true,
  "successExitCodes": [0, 3010]
}
```

## `patchJson`

Creates a backup file and applies JSON operations to a target JSON file.

Fields:

- `filePath`
- `operations`

Supported operations:

- `set`
- `remove`
- `append`

Path syntax:

- `$.Property`
- `$.Nested.Property`

Example:

```json
{
  "type": "patchJson",
  "name": "Patch appsettings",
  "filePath": "C:\\Apps\\Package\\appsettings.json",
  "operations": [
    {
      "op": "set",
      "path": "$.FeatureFlags.Enabled",
      "value": true
    },
    {
      "op": "set",
      "path": "$.ConnectionStrings.Main",
      "value": "Server=.;Database=App;Trusted_Connection=True;"
    }
  ]
}
```

## `restartWindowsService`

Restarts a Windows service using PowerShell.

Fields:

- `serviceName`
- `waitForRunning`
- `timeoutSeconds`
- `runElevated` optional

If `runElevated` is `true`, the backend starts a separate elevated PowerShell process for this step only.

Behavior:

- Windows shows a UAC prompt
- only this single step runs elevated
- the main DevelopmentHub process stays non-admin
- if the user cancels the UAC prompt, the step fails

Example:

```json
{
  "type": "restartWindowsService",
  "name": "Restart service",
  "serviceName": "MyApiService",
  "waitForRunning": true,
  "timeoutSeconds": 60,
  "runElevated": true
}
```
