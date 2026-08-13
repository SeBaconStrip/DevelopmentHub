# Configuration

## Setting the Workflow Folder

Workflow definitions are loaded from a folder on disk. You configure the path once in the application settings UI.

Settings field: **Workflow definitions path** (`workflowDefinitionsPath`)

This should be an absolute path to a folder that exists on disk, for example:

```
C:\DevelopmentHub\Workflows
```

The backend reads `*.json` files from this folder every time the workflow list is requested. You do not need to restart the application when you add or modify workflow files.

> **Tip:** Keep all your workflow files in one folder. You can use subfolders for organisation, but only files directly in the configured folder (not in subfolders) are loaded.

## File Format

Each `*.json` file in the folder can contain either:

**A single workflow:**
```json
{
  "id": "my-workflow",
  "name": "My Workflow",
  "steps": [...]
}
```

**An array of workflows:**
```json
[
  {
    "id": "workflow-one",
    "name": "Workflow One",
    "steps": [...]
  },
  {
    "id": "workflow-two",
    "name": "Workflow Two",
    "steps": [...]
  }
]
```

Both forms are supported and can be mixed across files. You can organise your workflows however you like — one file per workflow, or group related workflows together in a single file.

## Validation Rules

A workflow is loaded only if all of the following are true:

- `id` is present and non-empty
- `name` is present and non-empty
- `steps` contains at least one step

Workflows that fail these checks are silently skipped. If no workflows appear in the dashboard, check the troubleshooting guide.

## Duplicate IDs

If two workflows have the same `id`, the first one encountered wins and the second is ignored. The load order follows the filesystem's file enumeration order. To avoid surprises, always use unique IDs.

## Authentication: Using Provider Credentials in Steps

Some step types can authenticate using credentials that are already configured in the application settings for pull request providers. This means you do not need to embed tokens in workflow files.

### GitHub

The GitHub PAT is read from the provider configuration:

- Settings field: `pullRequestProviders.github.pat`

Used by: `downloadGithubReleaseAsset`

The step will use this PAT automatically unless the step itself specifies an override `pat` field.

Required GitHub fine-grained PAT permission:

- **Contents: Read** (for private repository release assets)

### Azure DevOps

The Azure DevOps credentials are read from the provider configuration:

- `pullRequestProviders.azureDevOps.organization`
- `pullRequestProviders.azureDevOps.project`
- `pullRequestProviders.azureDevOps.pat`

Used by: `downloadAzureDevopsPipelineArtifactAsset`

Any of these three values can be overridden directly on the step. Values set on the step take priority over the provider configuration.

#### Azure CLI (optional, recommended for large artifacts)

`downloadAzureDevopsPipelineArtifactAsset` downloads through the Azure CLI when the step writes extracted content (`destinationPath`) and `az` is on the PATH. This is significantly faster and more reliable for multi-GB artifacts. Install it once:

```powershell
winget install --id Microsoft.AzureCLI
```

The `azure-devops` CLI extension is installed automatically on first use. The configured PAT is handed to the CLI via its `AZURE_DEVOPS_EXT_PAT` environment variable, so no `az login` is required. When the CLI is missing, the step falls back to the REST API — no workflow change needed.

## Per-Step UAC Elevation

Some steps support running elevated (as administrator) even when DevelopmentHub itself is not running as administrator.

To enable elevation on a step, set:

```json
"runElevated": true
```

When the step runs, Windows will show a UAC prompt. The user must confirm the prompt to allow the step to proceed. If the user cancels, the step fails and the workflow stops.

Currently supported on:

- `restartWindowsService`
- `runExecutable`

This mechanism keeps the main DevelopmentHub process non-admin. Only the specific step that needs elevated rights will prompt for elevation.
