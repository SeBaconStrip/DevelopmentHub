# Troubleshooting

## No Workflows Appear in the Dashboard

**Check the configured path:**

- Open Settings and verify that **Workflow definitions path** is set.
- Make sure the path points to a folder that actually exists on disk.
- The path must be an absolute path (e.g. `C:\DevelopmentHub\Workflows`), not relative.

**Check the files:**

- The folder must contain at least one `*.json` file.
- Files in subfolders are not loaded — only files directly in the configured folder.

**Check the JSON:**

- Each workflow must have a non-empty `id`, a non-empty `name`, and at least one step.
- Validate your JSON syntax using a tool like [jsonlint.com](https://jsonlint.com) or your editor's JSON linter.
- Property names in the JSON are read case-insensitively, but the JSON itself must be structurally valid.

**Check for duplicate IDs:**

- If two workflows share the same `id`, only the first one is loaded. The second is silently ignored.

**Still nothing?**

- Reload the dashboard (hard refresh with Ctrl+Shift+R).
- Check the backend logs for warning messages about failed workflow file loads.

---

## A Workflow Appears but Cannot Be Started

**Check:**

- The workflow `id` is explicitly set and matches what you see in the dashboard.
- Try calling `GET /api/workflows` directly to verify the workflow is being returned by the backend.
- Make sure both the backend and frontend are running up-to-date builds.

---

## A Workflow Shows "0 Steps" or No Name

This means the workflow was loaded but the `steps` or `name` field did not deserialize correctly.

**Check:**

- The `steps` field is an array, not an object.
- Each step object has a `type` field with a valid step type name.
- The JSON structure matches the expected schema — compare it against the examples in [Examples](./examples.md).

---

## Execution Fails Immediately With "Unsupported Step Type"

The `type` value on a step does not match any known step type.

**Check:**

- The `type` value is spelled correctly and matches one of the supported types listed in [Step Reference](./step-reference.md).
- Type matching is case-insensitive, so `downloadFile` and `DownloadFile` are both valid.

---

## GitHub Release Download Fails

**Check:**

- `owner` matches the GitHub organisation or user name exactly.
- `repository` matches the repository name exactly.
- `releaseTag` matches the release tag exactly as it appears on GitHub, including any `v` prefix (e.g. `v1.2.3`).
- `assetName` matches the file name of the release asset exactly, including the extension.

**For private repositories:**

- A GitHub PAT is configured in Settings under the GitHub provider.
- The PAT has the **Contents: Read** fine-grained permission for the repository.
- The PAT has not expired.

**Tip:** Try accessing the GitHub releases page in your browser. If you can see the release and the asset listed there, the `releaseTag` and `assetName` values are correct.

---

## Azure DevOps Artifact Download Fails

**Check:**

- `organization` and `project` are set — either on the step or in the Azure DevOps provider settings.
- You are using exactly one lookup mode:
  - Pipeline run mode: `pipelineId` + `runId` are both set.
  - Build ID mode: `buildId` is set.
- `artifactName` matches the published artifact name exactly (this is case-sensitive).
- The PAT has permission to read build artifacts for the project.

**Common mistake:** Using the artifact's file name instead of the artifact container name. The `artifactName` is the name of the published artifact (e.g. `drop`), not the file inside it.

---

## `patchJson` Writes `null` Instead of the Expected Value

This happens when a `set` or `append` operation is missing the `value` field.

**Check:**

- Every `set` and `append` operation has a `value` field.
- The field is named exactly `value` — not `valueJson`, `data`, or anything else.

---

## `patchJson` Fails With a Path Error

**Check:**

- The `path` starts with `$.` (e.g. `$.Section.Key`).
- The path navigates to a property that exists in the document (for `remove` and `append`).
- For `append`: the target is an array, not an object or a scalar value.
- The file at `filePath` exists and contains valid JSON.

**Tip:** Open the target file and manually verify the property path. A typo in a property name (including wrong casing) will cause the path to not resolve.

---

## `runExecutable` Step Fails With a Non-Zero Exit Code

By default, only exit code `0` is treated as success.

**Check:**

- If the installer returns `3010` (success, reboot required), add it to `successExitCodes`: `[0, 3010]`.
- Check the installer's documentation for its exit codes.
- If you do not care about the exit code, you can set `waitForExit: false` — but this also means the workflow will not wait for the installer to finish.

---

## `restartWindowsService` Fails

**Check:**

- `serviceName` is the internal service name, not the display name. Open `services.msc`, find the service, and look at the "Service name" field on the General tab.
- The service exists and can be restarted manually on the machine.
- If the service takes a long time to start, increase `timeoutSeconds`.
- If the service requires administrator rights to restart, set `runElevated: true` on the step.

---

## UAC Prompt Does Not Appear

When a step has `runElevated: true`, a UAC prompt should appear when that step runs.

**Check:**

- UAC is enabled on the machine. If UAC is disabled system-wide, elevation prompts are suppressed.
- The application is not already running as administrator — if it is, elevation prompts are also suppressed because the process is already elevated.

---

## `callWorkflow` Step Fails With "Circular Reference Detected"

This means a workflow is (directly or indirectly) calling itself.

**Example chain:** `workflow-a` calls `workflow-b`, which calls `workflow-a` again.

The error message shows the full chain, for example:

```
Circular workflow reference detected: workflow-a → workflow-b → workflow-a
```

**Fix:** Review the chain of `callWorkflow` steps and remove the cycle. Each workflow in a call chain must be distinct.

---

## `callWorkflow` Step Fails With "Workflow Not Found"

The `workflowId` value does not match the `id` of any loaded workflow.

**Check:**

- The `workflowId` value matches the `id` field of the target workflow exactly (comparison is case-insensitive).
- The target workflow file is in the configured workflow folder and is being loaded successfully.
- The target workflow passes validation (has a non-empty `id`, `name`, and at least one step).
- If the `workflowId` uses a `{{placeholder}}`, verify the placeholder resolves to the correct value at runtime.

---

## Sub-Workflow Inputs Are Empty or Wrong

When calling a workflow with `callWorkflow`, the sub-workflow **does not** inherit the parent's inputs automatically. You must pass each value explicitly in the `inputs` object on the `callWorkflow` step.

**Example — incorrect (sub-workflow will not receive `version`):**

```json
{
  "type": "callWorkflow",
  "workflowId": "shared-setup"
}
```

**Example — correct (explicitly passing `version` to the sub-workflow):**

```json
{
  "type": "callWorkflow",
  "workflowId": "shared-setup",
  "inputs": {
    "version": "{{version}}"
  }
}
```

Any inputs declared in the sub-workflow that are not provided in the `inputs` object will fall back to their `defaultValue`. If no `defaultValue` is defined, the value will be an empty string.
