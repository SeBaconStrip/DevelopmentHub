# Workflow Schema

## Complete Example

```json
{
  "id": "install-package",
  "name": "Install Package",
  "description": "Downloads the release package from GitHub and installs it.",
  "runElevated": true,
  "inputs": [
    {
      "name": "version",
      "label": "Version",
      "type": "text",
      "defaultValue": "1.0.0"
    },
    {
      "name": "env",
      "label": "Environment",
      "type": "select",
      "options": ["staging", "production"],
      "defaultValue": "staging"
    },
    {
      "name": "verbose",
      "label": "Verbose logging",
      "type": "bool",
      "defaultValue": "false"
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
      "destinationPath": "C:\\Apps\\Package",
      "cleanDestination": true
    }
  ]
}
```

---

## Top-Level Fields

### `id`

**Type:** string — **Required**

A unique, stable identifier for this workflow. Used internally to identify the workflow across executions and in `callWorkflow` steps.

Rules:

- Must be non-empty
- Should be unique across all workflow files
- Should be stable — changing the ID breaks execution history links

Good examples: `install-edge-extension`, `deploy-api-service`, `configure-dev-environment`

> **Do not rely on auto-generated IDs.** If you omit the `id` field, the backend generates a deterministic hash from the file path and array index. This hash will change if you rename or move the file. Always set an explicit `id`.

---

### `name`

**Type:** string — **Required**

The display name shown on the workflow card in the dashboard and in execution logs.

---

### `description`

**Type:** string — **Optional**

A short explanation of what the workflow does. Shown below the workflow name on the dashboard card. Helps users understand the purpose of the workflow before they run it.

---

### `runElevated`

**Type:** boolean — **Optional, defaults to `false`**

When `true`, DevelopmentHub shows **one UAC prompt** when the workflow starts, then routes all privileged operations through a single elevated helper process for the duration of the workflow. No further UAC prompts appear during execution.

Compared to per-step elevation:

| | Workflow-level (`runElevated` on the workflow) | Per-step (`runElevated` on a step) |
|---|---|---|
| UAC prompts | One, at workflow start | One per elevated step |
| stdout/stderr captured | Yes | No |
| Sub-workflows | Inherit the elevated context | Each elevated step prompts independently |

The workflow card and input modal show an **elevated** badge so users know a UAC prompt is expected before the workflow starts.

When `runElevated: true` is set on the workflow, individual `runElevated` flags on steps are ignored — the workflow-level elevated helper handles all privileged operations.

---

### `inputs`

**Type:** array of input objects — **Optional**

Defines the values that the user is asked to provide before the workflow starts. Each input becomes a named placeholder that can be used in any string field of any step.

If a workflow has no inputs, it starts immediately when the user clicks Run (after the optional confirmation prompt).

See [Input Schema](#input-schema) below for the full field reference.

---

### `steps`

**Type:** array of step objects — **Required**

The ordered list of operations the workflow will perform. Steps execute one at a time, in the order they appear in this array. If any step fails, the workflow stops immediately — subsequent steps do not run.

A workflow must have at least one step to be loaded.

See the [Step Reference](./step-reference.md) for all available step types and their fields.

---

## Input Schema

Each entry in the `inputs` array defines one value the user must supply.

```json
{
  "name": "version",
  "label": "Version",
  "type": "text",
  "defaultValue": "1.0.0"
}
```

### `name`

**Type:** string — **Required**

The internal name of the input. This is the key used in `{{placeholders}}` throughout the workflow.

Example: if `name` is `version`, you can use `{{version}}` in any step field.

### `label`

**Type:** string — **Optional**

The text shown next to the input field in the user's input modal. Defaults to the `name` value if omitted.

### `type`

**Type:** string — **Optional, defaults to `"text"`**

The type of input control shown in the modal. Supported values:

| Value | Control | Submitted value |
|-------|---------|-----------------|
| `"text"` | Single-line text field | The string the user typed |
| `"bool"` | Checkbox | `"true"` or `"false"` |
| `"select"` | Dropdown menu | The selected option string |

For `"select"`, you must also provide the [`options`](#options) field.

### `defaultValue`

**Type:** string — **Optional**

The value pre-filled when the input modal opens. The user can change it before running.

- For `"text"` inputs: the pre-filled string. Defaults to empty.
- For `"bool"` inputs: `"true"` or `"false"`. Defaults to unchecked (`"false"`) if omitted.
- For `"select"` inputs: the option that is selected by default. Defaults to the first option if omitted or if the value is not in the options list.

### `options`

**Type:** array of strings — **Required for `"select"` type, ignored otherwise**

The list of choices shown in the dropdown. Each string is used as both the display label and the submitted value.

```json
{
  "name": "env",
  "label": "Environment",
  "type": "select",
  "options": ["dev", "staging", "production"],
  "defaultValue": "staging"
}
```

---

## The Placeholder System

Placeholders let you inject user-provided input values into step fields at runtime. They use double curly brace syntax:

```
{{inputName}}
```

### Where Placeholders Work

Placeholders are resolved in **any string field** of any step, including nested fields. This includes paths, URLs, arguments, service names, and JSON patch values.

### How Substitution Works

Before a step runs, the backend scans every string field in the step and replaces all `{{inputName}}` occurrences with the corresponding resolved input value. Matching is case-insensitive.

### Example

Given this input definition:

```json
{
  "name": "version",
  "label": "Version",
  "type": "text",
  "defaultValue": "2.0.0"
}
```

And this step:

```json
{
  "type": "downloadFile",
  "url": "https://example.com/package-{{version}}.zip",
  "targetPath": "C:\\Temp\\package-{{version}}.zip"
}
```

If the user enters `3.1.0`, the step runs as if it were written:

```json
{
  "type": "downloadFile",
  "url": "https://example.com/package-3.1.0.zip",
  "targetPath": "C:\\Temp\\package-3.1.0.zip"
}
```

### Placeholders in `patchJson` Values

Inside `patchJson` operations, placeholders are only substituted inside **string values**. Boolean and numeric values are used as-is.

```json
{ "op": "set", "path": "$.App.Version", "value": "{{version}}" }
```

This works — `{{version}}` is in a string value.

```json
{ "op": "set", "path": "$.App.Enabled", "value": true }
```

This also works — no substitution is needed because `true` is a boolean.

---

## Defining Multiple Workflows in One File

You can put several workflows in a single file by using a JSON array at the top level:

```json
[
  {
    "id": "workflow-a",
    "name": "Workflow A",
    "steps": [...]
  },
  {
    "id": "workflow-b",
    "name": "Workflow B",
    "steps": [...]
  }
]
```

This is useful for grouping related workflows (for example, all workflows related to one service) into a single file for easier management.

---

## Step Execution Model

Steps share the following behaviour regardless of type:

- **Sequential** — steps run one at a time, in order
- **Fail-fast** — if a step throws an error or returns a non-zero exit code (for `runExecutable`), the workflow stops immediately
- **Placeholder resolution** — all `{{input}}` markers are resolved before the step handler is called
- **Logging** — each step logs a start message, its own progress messages, and either a success or error message
- **Named steps** — the `name` field on a step is optional but recommended; it appears in log output to identify which step is running
