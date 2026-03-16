# Configuration

## Workflow Folder

Workflow definitions are loaded from the folder configured in the application settings UI.

Config field:

- `workflowDefinitionsPath`

This value is stored in the user configuration and points to a folder on disk.

## File Format

The workflow folder can contain any number of `*.json` files.

Each file can contain either:

- a single workflow object
- an array of workflow objects

## Loading Rules

The backend:

- scans the configured folder for `*.json`
- reads file properties case-insensitively
- normalizes workflow definitions
- filters out invalid workflows
- exposes valid workflows via `/api/workflows`

## Validation Rules

A workflow is only loaded if it has:

- a non-empty `id`
- a non-empty `name`
- at least one step

## Inputs

Inputs are defined inside a workflow and can be referenced in step fields using placeholders.

Example placeholder:

- `{{version}}`

## Authentication Sources

Some steps can use existing provider credentials from the application configuration.

GitHub:

- `pullRequestProviders.github.pat`

Azure DevOps:

- `pullRequestProviders.azureDevOps.organization`
- `pullRequestProviders.azureDevOps.project`
- `pullRequestProviders.azureDevOps.pat`

Steps can override these values directly.

## Elevated Steps

Some sensitive operations may require administrator rights.

The workflow engine supports per-step elevation for selected steps.

Current support:

- `restartWindowsService` via `runElevated: true`

This keeps the main DevelopmentHub process non-admin and only prompts for elevation when the specific step is executed.
