# Workflow Engine

The workflow engine lets DevelopmentHub run repeatable setup, install and maintenance tasks directly from the dashboard — no scripting required.

Workflows are defined as JSON files on disk. The backend loads them automatically, collects any required inputs from the user, executes the steps in order and streams the log output live to the dashboard.

## Documentation Map

| Document | What it covers |
|---|---|
| [Overview](./overview.md) | How the engine works end to end |
| [Configuration](./configuration.md) | Setting up the workflow folder and credentials |
| [Workflow Schema](./workflow-schema.md) | All top-level fields, inputs and the placeholder system |
| [Step Reference](./step-reference.md) | Every step type with all fields and examples |
| [Examples](./examples.md) | Complete ready-to-use workflow files |
| [Troubleshooting](./troubleshooting.md) | Common problems and how to fix them |

## Supported Step Types

| Step type | What it does |
|---|---|
| `downloadFile` | Download a file from a direct URL |
| `downloadGithubReleaseAsset` | Download a GitHub release asset (public or private) |
| `downloadAzureDevopsPipelineArtifactAsset` | Download an Azure DevOps pipeline artifact |
| `extractArchive` | Extract a ZIP archive |
| `copy` | Copy a file or directory |
| `runExecutable` | Run any executable or installer |
| `patchJson` | Apply in-place changes to a JSON config file |
| `restartWindowsService` | Restart a Windows service |
| `callWorkflow` | Invoke another workflow inline as a sub-workflow |

## Key Behaviours

- Workflow files are read from a single folder on disk. New files are picked up automatically on the next request.
- JSON property names in workflow files are read case-insensitively.
- Steps run in the order they are defined. If any step fails, the workflow stops immediately.
- Each step can log its own progress messages. All messages are streamed live to the dashboard.
- Individual steps can request UAC elevation without requiring the whole application to run as administrator.
- Workflows can call other workflows using the `callWorkflow` step. Circular references are detected and reported as errors.
