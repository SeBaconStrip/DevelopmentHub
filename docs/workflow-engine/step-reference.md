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

## `downloadAzureDevopsPipelineArtefactAsset`

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
- `assetName`
- `targetPath`
- `overwrite`
- `pat` optional override

Config fallback:

- `organization` falls back to `pullRequestProviders.azureDevOps.organization`
- `project` falls back to `pullRequestProviders.azureDevOps.project`
- `pat` falls back to `pullRequestProviders.azureDevOps.pat`

Note:

- the backend also accepts `downloadAzureDevopsPipelineArtifactAsset`

Example using pipeline run:

```json
{
  "type": "downloadAzureDevopsPipelineArtefactAsset",
  "name": "Download pipeline artifact",
  "organization": "my-org",
  "project": "MyProject",
  "pipelineId": "123",
  "runId": "{{runId}}",
  "assetName": "drop",
  "targetPath": "C:\\Temp\\drop-{{runId}}.zip",
  "overwrite": true
}
```

Example using build ID:

```json
{
  "type": "downloadAzureDevopsPipelineArtefactAsset",
  "name": "Download build artifact",
  "buildId": "{{buildId}}",
  "assetName": "drop",
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

## `runInstaller`

Runs an executable or installer.

Fields:

- `filePath`
- `arguments`
- `waitForExit`
- `successExitCodes`

Example:

```json
{
  "type": "runInstaller",
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

Example:

```json
{
  "type": "restartWindowsService",
  "name": "Restart service",
  "serviceName": "MyApiService",
  "waitForRunning": true,
  "timeoutSeconds": 60
}
```
