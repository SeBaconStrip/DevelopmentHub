# Workflow Engine

The workflow engine lets DevelopmentHub run repeatable setup, install and maintenance tasks from the dashboard.

Use this documentation set as the source of truth for:

- where workflows are stored
- how workflow files are structured
- which step types are supported
- how authentication is resolved
- how to write and maintain workflow JSON files

## Documentation Map

- [Overview](./overview.md)
- [Configuration](./configuration.md)
- [Workflow Schema](./workflow-schema.md)
- [Step Reference](./step-reference.md)
- [Examples](./examples.md)
- [Troubleshooting](./troubleshooting.md)

## Current Capabilities

The current workflow engine supports these step types:

- `downloadFile`
- `downloadGithubReleaseAsset`
- `downloadAzureDevopsPipelineArtefactAsset`
- `extractArchive`
- `runInstaller`
- `patchJson`
- `restartWindowsService`

## Notes

- workflow files are loaded from disk by the backend
- workflow file property names are read case-insensitively
- invalid workflows are ignored instead of shown in the dashboard
- workflow execution logs are streamed live to the UI
