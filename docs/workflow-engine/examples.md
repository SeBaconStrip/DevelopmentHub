# Examples

## Download a File From a Public URL

Downloads a versioned ZIP from a direct URL.

```json
{
  "id": "download-tool",
  "name": "Download Tool",
  "description": "Downloads a specific version of the tool from the public release server.",
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
      "url": "https://example.com/releases/tool-{{version}}.zip",
      "targetPath": "C:\\Temp\\tool-{{version}}.zip",
      "overwrite": true
    }
  ]
}
```

---

## Download a Private GitHub Release Asset

Downloads an asset from a private GitHub repository. Requires a PAT configured in settings with `Contents: Read` permission.

```json
{
  "id": "download-private-gh-release",
  "name": "Download Private GitHub Release",
  "description": "Downloads a versioned package from the private GitHub repository.",
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
      "type": "downloadGithubReleaseAsset",
      "name": "Download release asset",
      "owner": "my-org",
      "repository": "my-repo",
      "releaseTag": "v{{version}}",
      "assetName": "package-{{version}}.zip",
      "targetPath": "C:\\Downloads\\package-{{version}}.zip",
      "overwrite": true
    }
  ]
}
```

---

## Download an Azure DevOps Pipeline Artifact

Downloads the `drop` artifact from a specific pipeline run.

```json
{
  "id": "download-ado-artifact",
  "name": "Download Pipeline Artifact",
  "description": "Downloads the drop artifact from a specific Azure DevOps pipeline run.",
  "inputs": [
    {
      "name": "runId",
      "label": "Pipeline Run ID",
      "type": "text",
      "defaultValue": ""
    }
  ],
  "steps": [
    {
      "type": "downloadAzureDevopsPipelineArtifactAsset",
      "name": "Download artifact",
      "pipelineId": "42",
      "runId": "{{runId}}",
      "artifactName": "drop",
      "targetPath": "C:\\Temp\\drop-{{runId}}.zip",
      "overwrite": true
    }
  ]
}
```

---

## Download a Large Azure DevOps Artifact via the Azure CLI

Downloads a multi-GB installer artifact straight into a folder. Because `destinationPath` is used instead of `targetPath`, the download runs through `az pipelines runs artifact download` (chunked and resumable) and no separate `extractArchive` step is needed.

```json
{
  "id": "download-large-artifact",
  "name": "Download Large Artifact",
  "description": "Downloads the xyz installer artifact into the installer folder.",
  "inputs": [
    {
      "name": "BuildVersion",
      "label": "Build version",
      "type": "text",
      "defaultValue": "1.2.3"
    }
  ],
  "steps": [
    {
      "type": "downloadAzureDevopsPipelineArtifactAsset",
      "name": "Download pipeline artifact",
      "organization": "my-org",
      "project": "my-project",
      "pipelineName": "MyPipeline.CI",
      "runName": "{{BuildVersion}}",
      "artifactName": "MyLargeArtifact_{{BuildVersion}}",
      "destinationPath": "C:\\artifacts\\MyLargeArtifact_{{BuildVersion}}",
      "downloadMethod": "azureCli",
      "overwrite": true
    }
  ]
}
```

---

## Extract a Local ZIP

Extracts an already-downloaded ZIP to a clean destination folder.

```json
{
  "id": "extract-local-package",
  "name": "Extract Local Package",
  "description": "Extracts a locally downloaded package ZIP, replacing any previous extraction.",
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
      "type": "extractArchive",
      "name": "Extract package",
      "archivePath": "C:\\Downloads\\package-{{version}}.zip",
      "destinationPath": "C:\\Apps\\Package",
      "cleanDestination": true
    }
  ]
}
```

---

## Copy a Config File

Copies a prepared config file to the service directory, overwriting the existing one.

```json
{
  "id": "deploy-config",
  "name": "Deploy Config File",
  "description": "Copies the production config to the service folder.",
  "steps": [
    {
      "type": "copy",
      "name": "Copy appsettings",
      "sourcePath": "C:\\Configs\\appsettings.production.json",
      "destinationPath": "C:\\Apps\\MyService\\appsettings.json",
      "overwrite": true
    }
  ]
}
```

---

## Patch a JSON Config File

Updates values in an `appsettings.json` file in-place. A `.bak` backup is created before any changes are written.

```json
{
  "id": "patch-appsettings",
  "name": "Patch App Settings",
  "description": "Updates the connection string and feature flags in appsettings.json.",
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
        },
        {
          "op": "set",
          "path": "$.FeatureFlags.Enabled",
          "value": true
        },
        {
          "op": "remove",
          "path": "$.LegacySettings"
        }
      ]
    }
  ]
}
```

---

## Restart a Windows Service (per-step elevation)

Restarts a service and waits for it to be running again. Uses per-step `runElevated` — one UAC prompt for this step only. If multiple steps in a workflow need elevation, consider using `runElevated: true` on the workflow instead (one prompt for everything).

```json
{
  "id": "restart-api-service",
  "name": "Restart API Service",
  "description": "Restarts the API Windows service. Requires UAC elevation.",
  "steps": [
    {
      "type": "restartWindowsService",
      "name": "Restart service",
      "serviceName": "MyApiService",
      "waitForRunning": true,
      "timeoutSeconds": 60,
      "runElevated": true
    }
  ]
}
```

---

## Download, Extract and Run an Installer

A complete install workflow: downloads a versioned package, extracts it, and runs the installer silently.

```json
{
  "id": "install-package",
  "name": "Install Package",
  "description": "Downloads the release package, extracts it and runs the silent installer.",
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
      "type": "downloadGithubReleaseAsset",
      "name": "Download package",
      "owner": "my-org",
      "repository": "my-repo",
      "releaseTag": "v{{version}}",
      "assetName": "package-{{version}}.zip",
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
      "type": "runExecutable",
      "name": "Run installer",
      "filePath": "C:\\Temp\\package-{{version}}\\setup.exe",
      "arguments": ["/silent", "/norestart"],
      "waitForExit": true,
      "successExitCodes": [0, 3010],
      "runElevated": true
    }
  ]
}
```

---

## Full Deploy: Download, Install, Patch Config and Restart Service

A realistic end-to-end deploy workflow. Downloads a release, installs it, patches the config and restarts the service.

```json
{
  "id": "full-deploy",
  "name": "Full Deploy",
  "description": "Downloads the release, runs the installer, patches the config and restarts the service.",
  "inputs": [
    {
      "name": "version",
      "label": "Version",
      "type": "text",
      "defaultValue": "1.0.0"
    },
    {
      "name": "connectionString",
      "label": "Connection String",
      "type": "text",
      "defaultValue": "Server=.;Database=App;Trusted_Connection=True;"
    }
  ],
  "steps": [
    {
      "type": "downloadGithubReleaseAsset",
      "name": "Download installer",
      "owner": "my-org",
      "repository": "my-repo",
      "releaseTag": "v{{version}}",
      "assetName": "setup-{{version}}.exe",
      "targetPath": "C:\\Temp\\setup-{{version}}.exe",
      "overwrite": true
    },
    {
      "type": "runExecutable",
      "name": "Run installer",
      "filePath": "C:\\Temp\\setup-{{version}}.exe",
      "arguments": ["/silent", "/norestart"],
      "waitForExit": true,
      "successExitCodes": [0, 3010],
      "runElevated": true
    },
    {
      "type": "patchJson",
      "name": "Patch config",
      "filePath": "C:\\Apps\\MyService\\appsettings.json",
      "operations": [
        {
          "op": "set",
          "path": "$.ConnectionStrings.Main",
          "value": "{{connectionString}}"
        },
        {
          "op": "set",
          "path": "$.App.Version",
          "value": "{{version}}"
        }
      ]
    },
    {
      "type": "restartWindowsService",
      "name": "Restart service",
      "serviceName": "MyApiService",
      "waitForRunning": true,
      "timeoutSeconds": 60,
      "runElevated": true
    }
  ]
}
```

---

## Run an Entire Workflow Elevated

Use `runElevated: true` on the workflow to show **one UAC prompt** at the start and run all privileged steps through a shared elevated helper. No further UAC prompts appear during execution, and stdout/stderr from elevated processes is captured and shown in the log. The dashboard shows an **elevated** badge on the workflow card and in the input modal.

```json
{
  "id": "full-deploy-elevated",
  "name": "Full Deploy",
  "description": "Downloads the release, installs it, patches the config and restarts the service. Requires UAC.",
  "runElevated": true,
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
      "type": "downloadGithubReleaseAsset",
      "name": "Download installer",
      "owner": "my-org",
      "repository": "my-repo",
      "releaseTag": "v{{version}}",
      "assetName": "setup-{{version}}.exe",
      "targetPath": "C:\\Temp\\setup-{{version}}.exe",
      "overwrite": true
    },
    {
      "type": "runExecutable",
      "name": "Run installer",
      "filePath": "C:\\Temp\\setup-{{version}}.exe",
      "arguments": ["/silent", "/norestart"],
      "waitForExit": true,
      "successExitCodes": [0, 3010]
    },
    {
      "type": "patchJson",
      "name": "Patch config",
      "filePath": "C:\\Apps\\MyService\\appsettings.json",
      "operations": [
        { "op": "set", "path": "$.App.Version", "value": "{{version}}" }
      ]
    },
    {
      "type": "restartWindowsService",
      "name": "Restart service",
      "serviceName": "MyApiService",
      "waitForRunning": true,
      "timeoutSeconds": 60
    }
  ]
}
```

Note that none of the steps need `"runElevated": true` individually — the workflow-level flag covers all of them.

---

## Tagging Workflows

Add a `"tags"` array to group workflows and make them filterable on the Workflows page. Tags appear as coloured chips on each workflow card.

```json
[
  {
    "id": "deploy-api",
    "name": "Deploy API",
    "description": "Downloads the latest API release and installs it.",
    "tags": ["deploy", "api"],
    "inputs": [
      { "name": "version", "label": "Version", "type": "text", "defaultValue": "1.0.0" }
    ],
    "steps": [
      {
        "type": "downloadGithubReleaseAsset",
        "name": "Download release",
        "owner": "my-org",
        "repository": "my-api",
        "releaseTag": "v{{version}}",
        "assetName": "setup-{{version}}.exe",
        "targetPath": "C:\\Temp\\setup-{{version}}.exe",
        "overwrite": true
      }
    ]
  },
  {
    "id": "deploy-frontend",
    "name": "Deploy Frontend",
    "description": "Deploys the frontend bundle to the web server.",
    "tags": ["deploy", "frontend"],
    "steps": [
      {
        "type": "copy",
        "name": "Copy bundle",
        "sourcePath": "C:\\Build\\dist",
        "destinationPath": "C:\\WebServer\\wwwroot",
        "overwrite": true
      }
    ]
  },
  {
    "id": "restart-all-services",
    "name": "Restart All Services",
    "description": "Restarts all application services.",
    "tags": ["infra"],
    "runElevated": true,
    "steps": [
      {
        "type": "restartWindowsService",
        "name": "Restart API service",
        "serviceName": "MyApiService",
        "waitForRunning": true,
        "timeoutSeconds": 60
      }
    ]
  }
]
```

On the Workflows page, clicking the **deploy** tag chip filters the list to `Deploy API` and `Deploy Frontend`. Clicking **infra** shows only `Restart All Services`. Click the active tag again to clear the filter.

---

## Bool and Select Inputs

Use `"type": "bool"` for a checkbox and `"type": "select"` for a dropdown. Both are submitted as strings (`"true"`/`"false"` for bool, the selected option for select) and can be used in `{{placeholders}}` just like text inputs.

```json
{
  "id": "deploy-with-options",
  "name": "Deploy with Options",
  "description": "Deploys to a selected environment with optional verbose logging.",
  "inputs": [
    {
      "name": "env",
      "label": "Environment",
      "type": "select",
      "options": ["dev", "staging", "production"],
      "defaultValue": "dev"
    },
    {
      "name": "verbose",
      "label": "Verbose logging",
      "type": "bool",
      "defaultValue": "false"
    },
    {
      "name": "version",
      "label": "Version",
      "type": "text",
      "defaultValue": "1.0.0"
    }
  ],
  "steps": [
    {
      "type": "runExecutable",
      "name": "Run deploy script",
      "filePath": "C:\\Scripts\\deploy.cmd",
      "arguments": ["--env", "{{env}}", "--version", "{{version}}", "--verbose", "{{verbose}}"],
      "waitForExit": true
    }
  ]
}
```

The `{{env}}` placeholder will resolve to whichever option the user picks (e.g. `"staging"`), and `{{verbose}}` will be `"true"` or `"false"` depending on whether the checkbox is checked.

---

## Calling Another Workflow (Sub-Workflows)

### Shared Sub-Workflow Pattern

This pattern splits a large workflow into reusable pieces. A `shared-setup` workflow handles the common steps, and specific workflows call it before doing their own work.

**shared-setup.json** — the reusable part:

```json
{
  "id": "shared-setup",
  "name": "Shared Setup",
  "description": "Creates the working directory and copies base config files.",
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
      "type": "extractArchive",
      "name": "Extract base package",
      "archivePath": "C:\\Temp\\base-{{version}}.zip",
      "destinationPath": "C:\\Apps\\Service",
      "cleanDestination": true
    },
    {
      "type": "copy",
      "name": "Copy base config",
      "sourcePath": "C:\\Configs\\appsettings.base.json",
      "destinationPath": "C:\\Apps\\Service\\appsettings.json",
      "overwrite": true
    }
  ]
}
```

**deploy-production.json** — calls the shared workflow, then does environment-specific work:

```json
{
  "id": "deploy-production",
  "name": "Deploy to Production",
  "description": "Runs shared setup and applies production-specific configuration.",
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
      "type": "callWorkflow",
      "name": "Run shared setup",
      "workflowId": "shared-setup",
      "inputs": {
        "version": "{{version}}"
      }
    },
    {
      "type": "patchJson",
      "name": "Apply production config",
      "filePath": "C:\\Apps\\Service\\appsettings.json",
      "operations": [
        {
          "op": "set",
          "path": "$.ConnectionStrings.Main",
          "value": "Server=prod-db;Database=App;Trusted_Connection=True;"
        },
        {
          "op": "set",
          "path": "$.Logging.Level",
          "value": "Warning"
        }
      ]
    },
    {
      "type": "restartWindowsService",
      "name": "Restart service",
      "serviceName": "MyApiService",
      "waitForRunning": true,
      "timeoutSeconds": 60,
      "runElevated": true
    }
  ]
}
```

When this runs, the execution log will look like:

```
Starting workflow 'Deploy to Production'.
Running step 'Run shared setup' (callWorkflow).
[Shared Setup] Starting sub-workflow 'Shared Setup'.
[Shared Setup] Running step 'Extract base package' (extractArchive).
[Shared Setup] Step 'Extract base package' finished.
[Shared Setup] Running step 'Copy base config' (copy).
[Shared Setup] Step 'Copy base config' finished.
[Shared Setup] Sub-workflow 'Shared Setup' completed.
Step 'Run shared setup' finished.
Running step 'Apply production config' (patchJson).
Step 'Apply production config' finished.
...
```

### Grouped Workflows in One File

You can put multiple related workflows in a single file:

```json
[
  {
    "id": "deploy-staging",
    "name": "Deploy to Staging",
    "description": "Deploys to the staging environment.",
      "inputs": [
      { "name": "version", "label": "Version", "type": "text", "defaultValue": "1.0.0" }
    ],
    "steps": [
      {
        "type": "callWorkflow",
        "name": "Run shared setup",
        "workflowId": "shared-setup",
        "inputs": { "version": "{{version}}" }
      },
      {
        "type": "patchJson",
        "name": "Apply staging config",
        "filePath": "C:\\Apps\\Service\\appsettings.json",
        "operations": [
          { "op": "set", "path": "$.ConnectionStrings.Main", "value": "Server=staging-db;Database=App;Trusted_Connection=True;" }
        ]
      }
    ]
  },
  {
    "id": "deploy-production",
    "name": "Deploy to Production",
    "description": "Deploys to the production environment.",
      "inputs": [
      { "name": "version", "label": "Version", "type": "text", "defaultValue": "1.0.0" }
    ],
    "steps": [
      {
        "type": "callWorkflow",
        "name": "Run shared setup",
        "workflowId": "shared-setup",
        "inputs": { "version": "{{version}}" }
      },
      {
        "type": "patchJson",
        "name": "Apply production config",
        "filePath": "C:\\Apps\\Service\\appsettings.json",
        "operations": [
          { "op": "set", "path": "$.ConnectionStrings.Main", "value": "Server=prod-db;Database=App;Trusted_Connection=True;" }
        ]
      }
    ]
  }
]
```
