# Step Reference

Every step shares two common fields:

| Field | Type | Required | Description |
|---|---|---|---|
| `type` | string | Yes | Identifies which step type to execute (see below) |
| `name` | string | No | A human-readable label shown in the execution log |

The `name` field is optional but strongly recommended. Without it, the step type string is used in log output, which is harder to read.

---

## `downloadFile`

Downloads a file from any publicly accessible URL and saves it to a local path.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `url` | string | Yes | — | The full URL to download from |
| `targetPath` | string | Yes | — | The local path where the file will be saved |
| `overwrite` | boolean | No | `false` | If `false` and the target file already exists, the step fails |

**Notes:**

- Parent directories of `targetPath` are created automatically if they do not exist.
- If the server returns a non-2xx response, the step fails.
- This step does not follow authentication — use `downloadGithubReleaseAsset` or `downloadAzureDevopsPipelineArtifactAsset` for private assets.

**Example:**

```json
{
  "type": "downloadFile",
  "name": "Download release zip",
  "url": "https://example.com/releases/package-{{version}}.zip",
  "targetPath": "C:\\Temp\\package-{{version}}.zip",
  "overwrite": true
}
```

---

## `downloadGithubReleaseAsset`

Downloads a specific asset from a GitHub release. Supports both public and private repositories. When downloading from a private repository, a GitHub PAT with `Contents: Read` permission is required.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `owner` | string | Yes | — | The GitHub organisation or user name |
| `repository` | string | Yes | — | The repository name |
| `releaseTag` | string | Yes | — | The release tag exactly as it appears on GitHub (e.g. `v1.2.3`) |
| `assetName` | string | Yes | — | The file name of the asset to download (must match exactly) |
| `targetPath` | string | Yes | — | The local path where the file will be saved |
| `overwrite` | boolean | No | `false` | If `false` and the target file already exists, the step fails |
| `pat` | string | No | — | Override PAT for this step. Falls back to the GitHub PAT in provider settings |

**PAT resolution order:**

1. The `pat` field on the step (if set and non-empty)
2. `pullRequestProviders.github.pat` from application settings

**Notes:**

- The `releaseTag` must match exactly what GitHub shows, including any `v` prefix.
- The `assetName` must match the file name of the asset exactly, including the extension.
- Parent directories of `targetPath` are created automatically if they do not exist.

**Example:**

```json
{
  "type": "downloadGithubReleaseAsset",
  "name": "Download extension package",
  "owner": "my-org",
  "repository": "my-extension",
  "releaseTag": "v{{version}}",
  "assetName": "extension-{{version}}.zip",
  "targetPath": "C:\\Downloads\\extension-{{version}}.zip",
  "overwrite": true
}
```

---

## `downloadAzureDevopsPipelineArtifactAsset`

Downloads a pipeline artifact from Azure DevOps, either as a ZIP file (`targetPath`) or as extracted content in a directory (`destinationPath`). Supports two lookup modes: by pipeline run, or by build ID.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `organization` | string | No | Provider setting | Azure DevOps organisation name. Falls back to provider settings |
| `project` | string | No | Provider setting | Azure DevOps project name. Falls back to provider settings |
| `pipelineId` | string | No* | — | The pipeline definition ID. Required when using pipeline run mode |
| `pipelineName` | string | No* | — | Pipeline name, resolved to `pipelineId` at runtime |
| `runId` | string | No* | — | The pipeline run ID. Required when using pipeline run mode |
| `runName` | string | No* | — | Run name (e.g. `1.2.3`), resolved to `runId` at runtime |
| `buildId` | string | No* | — | The build ID. Required when using build ID mode |
| `artifactName` | string | Yes | — | The name of the artifact to download |
| `targetPath` | string | No** | — | Local path of the ZIP file to write. Mutually exclusive with `destinationPath` |
| `destinationPath` | string | No** | — | Directory that receives the extracted artifact content, without keeping a ZIP. Mutually exclusive with `targetPath` |
| `cleanDestination` | boolean | No | `false` | If `true`, `destinationPath` is deleted before the download starts |
| `downloadMethod` | string | No | `auto` | `auto`, `azureCli` or `rest` — see below |
| `maxAttempts` | number | No | `3` | Download attempts of the `azureCli` transport before the step fails |
| `overwrite` | boolean | No | `false` | If `false`, the step fails when the target file exists or `destinationPath` is not empty |
| `pat` | string | No | — | Override PAT. Falls back to `pullRequestProviders.azureDevOps.pat` |

\* Exactly one lookup mode. \*\* Exactly one of `targetPath` / `destinationPath`.

**Lookup modes:**

You must use exactly one of these combinations:

- **Pipeline run mode:** provide `pipelineId` (or `pipelineName`) + `runId` (or `runName`)
- **Build ID mode:** provide `buildId`

**Transports (`downloadMethod`):**

| Value | Behaviour |
|---|---|
| `auto` (default) | Uses `azureCli` when `destinationPath` is set and the Azure CLI is installed, otherwise `rest` |
| `azureCli` | Runs `az pipelines runs artifact download`. Requires `destinationPath`; fails when the Azure CLI is not installed |
| `rest` | Streams the artifact ZIP from the Azure DevOps REST API |

Prefer `destinationPath` for large artifacts. The Azure CLI delegates to ArtifactTool, which transfers dedup artifacts in parallel chunks with per-chunk retries, whereas the REST transport pulls one long-lived ZIP stream — a multi-GB artifact (e.g. a large installer package) regularly fails midway. The CLI transport also skips the separate `extractArchive` step and the double disk usage of ZIP + extracted content.

**Credential resolution order (for `organization`, `project`, `pat`):**

1. The value on the step (if set and non-empty)
2. The corresponding value in `pullRequestProviders.azureDevOps`

**Notes:**

- The `artifactName` must match the name of the published artifact exactly (case-sensitive).
- Both transports produce the same layout under `destinationPath`: the artifact's own root folder is flattened away, so the content lands directly in the directory.
- With `targetPath`, the artifact is downloaded as a whole (a ZIP file), not an individual file inside it. Use `extractArchive` afterwards to unpack it.
- The PAT is passed to the Azure CLI through the `AZURE_DEVOPS_EXT_PAT` environment variable of the CLI process. With `downloadMethod: "azureCli"` and an explicit `runId`/`buildId`, no PAT is needed at all — the CLI then uses its own `az login` session. Resolving `pipelineName`/`runName` always requires a PAT.
- The `azure-devops` CLI extension is installed automatically on first use.

**Example writing extracted content (recommended for large artifacts):**

```json
{
  "type": "downloadAzureDevopsPipelineArtifactAsset",
  "name": "Download build artifact",
  "pipelineName": "MyPipeline.CI",
  "runName": "{{BuildVersion}}",
  "artifactName": "MyArtifact_{{BuildVersion}}",
  "destinationPath": "C:\\artifacts\\MyArtifact_{{BuildVersion}}",
  "overwrite": true
}
```

**Example using pipeline run:**

```json
{
  "type": "downloadAzureDevopsPipelineArtifactAsset",
  "name": "Download pipeline artifact",
  "pipelineId": "42",
  "runId": "{{runId}}",
  "artifactName": "drop",
  "targetPath": "C:\\Temp\\drop-{{runId}}.zip",
  "overwrite": true
}
```

**Example using build ID:**

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

---

## `extractArchive`

Extracts a ZIP archive to a local directory.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `archivePath` | string | Yes | — | Path to the ZIP file to extract |
| `destinationPath` | string | Yes | — | Directory where the contents will be extracted |
| `cleanDestination` | boolean | No | `false` | If `true`, the destination directory is deleted before extraction |

**Notes:**

- The destination directory is created automatically if it does not exist.
- Set `cleanDestination: true` when you want a clean install — this removes any leftover files from a previous extraction before the new files are written.
- Only ZIP files are supported.

**Example:**

```json
{
  "type": "extractArchive",
  "name": "Extract package",
  "archivePath": "C:\\Temp\\package-{{version}}.zip",
  "destinationPath": "C:\\Apps\\Package",
  "cleanDestination": true
}
```

---

## `copy`

Copies a file or an entire directory tree to a new location.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `sourcePath` | string | Yes | — | The file or directory to copy |
| `destinationPath` | string | Yes | — | The destination file path or directory path |
| `overwrite` | boolean | No | `true` | If `false` and the destination already exists, the step fails |

**Behaviour:**

- If `sourcePath` is a file, it is copied to `destinationPath` (treated as a file path).
- If `sourcePath` is a directory, the entire directory tree is copied recursively to `destinationPath`.
- Parent directories of the destination are created automatically.

**Example:**

```json
{
  "type": "copy",
  "name": "Deploy config",
  "sourcePath": "C:\\Configs\\appsettings.production.json",
  "destinationPath": "C:\\Apps\\MyService\\appsettings.json",
  "overwrite": true
}
```

---

## `runExecutable`

Runs any executable, installer or script. Optionally waits for the process to finish and validates its exit code.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `filePath` | string | Yes | — | Full path to the executable to run |
| `arguments` | string array | No | `[]` | Arguments to pass to the executable |
| `waitForExit` | boolean | No | `true` | If `true`, the step waits for the process to finish before continuing |
| `successExitCodes` | integer array | No | `[0]` | Exit codes that are considered successful. Any other code causes the step to fail |
| `runElevated` | boolean | No | `false` | If `true`, the process runs with administrative privileges (see notes) |

**Notes:**

- Arguments that contain spaces are automatically wrapped in double quotes.
- If `waitForExit` is `false`, the process is launched and the step immediately succeeds. Exit code validation is skipped.
- Common `successExitCodes` for Windows installers: `0` (success) and `3010` (success, reboot required).
- **Elevation behaviour** depends on where `runElevated` is set:
  - If the **workflow** has `runElevated: true`, the process runs through the workflow's shared elevated helper — no UAC prompt at this step, and stdout/stderr are captured normally.
  - If only this **step** has `runElevated: true`, Windows shows a UAC prompt for this step only. If the user cancels, the step fails. Output is not captured.

**Example:**

```json
{
  "type": "runExecutable",
  "name": "Run installer",
  "filePath": "C:\\Temp\\package-{{version}}\\setup.exe",
  "arguments": ["/silent", "/norestart"],
  "waitForExit": true,
  "successExitCodes": [0, 3010],
  "runElevated": true
}
```

---

## `patchJson`

Reads a JSON file, applies a list of patch operations, and writes the result back. A backup of the original file is created at `<filePath>.bak` before any changes are written.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `filePath` | string | Yes | — | Path to the JSON file to patch |
| `operations` | array | Yes | — | Ordered list of patch operations to apply |
| `runElevated` | boolean | No | `false` | If `true`, the file write runs with administrative privileges (see `runExecutable` elevation notes) |

### Operations

Each operation has an `op` field that determines what it does.

#### `set`

Sets a property to a value. If the property already exists, its value is replaced. If it does not exist, it is created.

```json
{ "op": "set", "path": "$.FeatureFlags.Enabled", "value": true }
```

#### `remove`

Removes a property from its parent object. Has no effect if the property does not exist.

```json
{ "op": "remove", "path": "$.LegacySettings.OldKey" }
```

#### `append`

Appends a value to an existing array. The target path must point to an array.

```json
{ "op": "append", "path": "$.AllowedHosts", "value": "localhost" }
```

### Path Syntax

Paths use a subset of JSONPath syntax:

- Start with `$` (the root of the document)
- Use `.` to navigate into properties
- Examples: `$.Property`, `$.Nested.Property`, `$.Section.SubSection.Key`

### Value Types

The `value` field accepts any JSON-compatible value:

| Type | Example |
|---|---|
| string | `"hello"` |
| string with placeholder | `"{{version}}"` |
| boolean | `true` or `false` |
| number | `42` |
| object | `{"key": "value"}` |
| array | `["a", "b", "c"]` |

> Placeholders (`{{inputName}}`) are only resolved inside **string** values. Boolean and numeric values are used as-is.

### Full Example

```json
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
      "op": "append",
      "path": "$.AllowedHosts",
      "value": "localhost"
    },
    {
      "op": "remove",
      "path": "$.LegacySettings"
    }
  ]
}
```

---

## `restartWindowsService`

Stops and starts a Windows service using PowerShell. Optionally waits until the service is back in the `Running` state before the step completes.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `serviceName` | string | Yes | — | The exact name of the Windows service (not the display name) |
| `waitForRunning` | boolean | No | `true` | If `true`, the step waits until the service reaches `Running` state |
| `timeoutSeconds` | integer | No | `60` | How many seconds to wait for the service to reach `Running` state |
| `runElevated` | boolean | No | `false` | If `true`, the service restart runs with administrative privileges (see `runExecutable` elevation notes) |

**Notes:**

- Use the service's internal name (as shown in `services.msc` under "Service name"), not its display name.
- If the service does not start within `timeoutSeconds`, the step fails.
- See the `runExecutable` step for a full explanation of per-step vs. workflow-level elevation behaviour.

**Example:**

```json
{
  "type": "restartWindowsService",
  "name": "Restart API service",
  "serviceName": "MyApiService",
  "waitForRunning": true,
  "timeoutSeconds": 60,
  "runElevated": true
}
```

---

## `callWorkflow`

Invokes another workflow inline as a sub-workflow. The sub-workflow's steps run as part of the current execution — there is no separate execution record created. All log output from the sub-workflow appears in the parent's log, prefixed with the sub-workflow name so you can tell which messages come from which workflow.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `workflowId` | string | Yes | — | The `id` of the workflow to invoke. Supports `{{placeholder}}` substitution |
| `inputs` | object | No | `{}` | Key-value pairs of input values to pass to the sub-workflow. Values support `{{placeholder}}` substitution |

**Important behaviours:**

- **Inputs are explicit.** The sub-workflow does not automatically inherit the parent's input values. You must pass each value you want to share explicitly in the `inputs` object.
- **Inputs fall back to defaults.** Any inputs declared in the sub-workflow that are not provided in the `inputs` object will use that input's `defaultValue`. If there is no default, the value is empty.
- **Circular references are detected.** If workflow A calls B and B calls A (at any depth), the step fails immediately with a clear error message listing the chain. The detection happens before any steps of the sub-workflow run.
- **Failure propagates.** If any step in the sub-workflow fails, the sub-workflow stops and the failure propagates to the parent workflow, which also stops.
- **Log prefixing.** Sub-workflow log lines are prefixed with `[SubWorkflowName]`. If sub-workflows are nested, the prefixes stack: `[Outer] [Inner] message`.

**Example — calling a shared setup workflow:**

```json
{
  "type": "callWorkflow",
  "name": "Run shared setup",
  "workflowId": "shared-setup",
  "inputs": {
    "environment": "production",
    "version": "{{version}}"
  }
}
```

**Example — dynamic workflow ID from an input:**

```json
{
  "type": "callWorkflow",
  "name": "Run environment-specific workflow",
  "workflowId": "deploy-{{environment}}",
  "inputs": {
    "version": "{{version}}"
  }
}
```

**Example — calling a workflow with no inputs:**

```json
{
  "type": "callWorkflow",
  "name": "Run cleanup",
  "workflowId": "cleanup-temp-files"
}
```
