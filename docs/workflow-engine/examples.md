# Examples

## Download From A Public URL

```json
{
  "id": "download-tool",
  "name": "Download Tool",
  "description": "Downloads a zip file from a direct URL.",
  "requiresConfirmation": false,
  "inputs": [
    {
      "name": "version",
      "label": "Version",
      "type": "text",
      "defaultValue": "1.0.0"
    }
  ],
  "steps": [
    {
      "type": "downloadFile",
      "name": "Download zip",
      "url": "https://example.com/tool-{{version}}.zip",
      "targetPath": "C:\\Temp\\tool-{{version}}.zip",
      "overwrite": true
    }
  ]
}
```

## Download A Private GitHub Release Asset

```json
{
  "id": "download-private-gh-release",
  "name": "Download Private GitHub Release Asset",
  "description": "Downloads a private GitHub release asset using the configured PAT.",
  "requiresConfirmation": false,
  "inputs": [
    {
      "name": "version",
      "label": "Version",
      "type": "text",
      "defaultValue": "0.7.36"
    }
  ],
  "steps": [
    {
      "type": "downloadGithubReleaseAsset",
      "name": "Download release zip",
      "owner": "example-owner",
      "repository": "example-repository",
      "releaseTag": "v{{version}}",
      "assetName": "package-{{version}}.zip",
      "targetPath": "C:\\Downloads\\package-{{version}}.zip",
      "overwrite": true
    }
  ]
}
```

## Download An Azure DevOps Pipeline Artifact

```json
{
  "id": "download-ado-artifact",
  "name": "Download Azure DevOps Artifact",
  "description": "Downloads a pipeline artifact from Azure DevOps.",
  "requiresConfirmation": false,
  "inputs": [
    {
      "name": "runId",
      "label": "Run ID",
      "type": "text",
      "defaultValue": ""
    }
  ],
  "steps": [
    {
      "type": "downloadAzureDevopsPipelineArtefactAsset",
      "name": "Download artifact",
      "organization": "my-org",
      "project": "MyProject",
      "pipelineId": "123",
      "runId": "{{runId}}",
      "assetName": "drop",
      "targetPath": "C:\\Temp\\drop-{{runId}}.zip",
      "overwrite": true
    }
  ]
}
```

## Extract A Local Zip

```json
{
  "id": "extract-local-package",
  "name": "Extract Local Package",
  "description": "Extracts a locally downloaded zip package.",
  "requiresConfirmation": false,
  "inputs": [
    {
      "name": "version",
      "label": "Version",
      "type": "text",
      "defaultValue": "0.5.29"
    }
  ],
  "steps": [
    {
      "type": "extractArchive",
      "name": "Extract local zip",
      "archivePath": "C:\\Downloads\\package-{{version}}.zip",
      "destinationPath": "C:\\Downloads\\package-{{version}}",
      "cleanDestination": true
    }
  ]
}
```

## Patch JSON And Restart A Service

```json
{
  "id": "configure-and-restart-service",
  "name": "Configure And Restart Service",
  "description": "Patches appsettings.json and restarts the Windows service.",
  "requiresConfirmation": true,
  "inputs": [
    {
      "name": "connectionString",
      "label": "Connection String",
      "type": "text",
      "defaultValue": "Server=.;Database=App;Trusted_Connection=True;"
    }
  ],
  "steps": [
    {
      "type": "patchJson",
      "name": "Patch appsettings",
      "filePath": "C:\\Apps\\MyService\\appsettings.json",
      "operations": [
        {
          "op": "set",
          "path": "$.ConnectionStrings.Main",
          "value": "{{connectionString}}"
        }
      ]
    },
    {
      "type": "restartWindowsService",
      "name": "Restart service",
      "serviceName": "MyService",
      "waitForRunning": true,
      "timeoutSeconds": 60
    }
  ]
}
```

## Download, Extract And Run Installer

```json
{
  "id": "install-package",
  "name": "Install Package",
  "description": "Downloads a package, extracts it and runs the installer.",
  "requiresConfirmation": true,
  "inputs": [
    {
      "name": "version",
      "label": "Version",
      "type": "text",
      "defaultValue": "1.2.3"
    }
  ],
  "steps": [
    {
      "type": "downloadFile",
      "name": "Download package",
      "url": "https://example.com/package-{{version}}.zip",
      "targetPath": "C:\\Temp\\package-{{version}}.zip",
      "overwrite": true
    },
    {
      "type": "extractArchive",
      "name": "Extract package",
      "archivePath": "C:\\Temp\\package-{{version}}.zip",
      "destinationPath": "C:\\Temp\\package-{{version}}",
      "cleanDestination": true
    },
    {
      "type": "runInstaller",
      "name": "Run setup",
      "filePath": "C:\\Temp\\package-{{version}}\\setup.exe",
      "arguments": ["/silent"],
      "waitForExit": true,
      "successExitCodes": [0, 3010]
    }
  ]
}
```
