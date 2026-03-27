# Overview

## What The Workflow Engine Is For

The workflow engine automates repeated development and operations tasks that would otherwise require manual steps or custom scripts. It is designed so that non-developers can run complex multi-step operations from the dashboard without knowing anything about the underlying tools.

Typical use cases:

- Downloading a release package from GitHub or Azure DevOps and installing it
- Extracting a downloaded archive and copying files to the right locations
- Patching a JSON configuration file after an install
- Restarting a Windows service after a configuration change
- Composing several smaller workflows into one larger automated process

## How It Works: End-to-End Flow

### 1. Defining Workflows

Workflows are written as JSON files and placed in a folder on disk. The folder is configured once in the application settings. There is no database to manage — just files.

A workflow file describes:

- a unique ID and a display name
- an optional description
- zero or more user inputs that are collected before the workflow starts
- an ordered list of steps to execute

### 2. Loading

When the dashboard requests the workflow list, the backend scans the configured folder for `*.json` files. Each file can contain either a single workflow object or an array of workflow objects.

The backend normalizes and validates the definitions. Workflows that are missing required fields are silently skipped. The valid workflows are returned to the dashboard.

### 3. Running a Workflow

When the user clicks **Run** on a workflow card:

1. If the workflow declares inputs, the dashboard opens an input modal and asks the user to fill in the values.
2. If the workflow has `requiresConfirmation: true`, the user is shown a confirmation prompt.
3. The dashboard sends the collected inputs to the backend.
4. The backend starts executing the workflow in the background and immediately returns an execution ID.
5. The dashboard opens the execution log modal and subscribes to live log updates via SignalR.
6. The backend executes each step in order, logging progress after each action.
7. When all steps finish (or one fails), the workflow transitions to `succeeded`, `failed` or `cancelled`.

### 4. Step Execution

Each step is executed by a dedicated handler. Before a step runs, the backend:

- resolves all `{{placeholder}}` markers in the step's string fields by substituting the collected input values
- logs a "Running step…" message
- calls the step handler
- logs "Step finished." on success, or the error message on failure

If any step throws an error, the workflow stops immediately and is marked as `failed`.

### 5. Sub-Workflows

A step of type `callWorkflow` can invoke another workflow inline. The sub-workflow's steps run as part of the parent execution and their log output appears in the same log, prefixed with the sub-workflow name. Sub-workflows can themselves call further sub-workflows. Circular references (A calls B which calls A) are detected and reported as an error before any steps run.

### 6. Elevation

Some steps support `runElevated: true`. When this flag is set, the backend spawns a separate elevated process for that step only and waits for it to finish. Windows shows a UAC prompt to the user. The main DevelopmentHub process never runs as administrator.

## Execution States

| State | Meaning |
|---|---|
| `running` | The workflow is currently executing |
| `succeeded` | All steps completed without errors |
| `failed` | A step threw an error or reported a non-zero exit code |
| `cancelled` | The execution was cancelled |

## Main Components

**Backend:**

- Workflow file loader — scans the folder, deserializes and validates workflow definitions
- Workflow execution service — manages running executions, dispatches steps, tracks state
- Step executors — one per step type, contain the actual implementation
- SignalR log hub — streams log lines to connected dashboard clients in real time

**Frontend:**

- Workflows widget — shows all workflows with their last execution status
- Input modal — collects user input values before a workflow starts
- Execution log modal — shows the live or historical log for an execution
