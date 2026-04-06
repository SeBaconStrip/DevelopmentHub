# Contributing

Thanks for your interest in contributing to DevelopmentHub.

## Prerequisites

- Windows 10/11
- .NET 9 SDK
- Node.js 20+
- Microsoft Edge WebView2 Runtime

## Development Setup

```bash
# Install frontend dependencies
cd src/web
npm ci

# Start the frontend dev server (hot reload)
npm run dev

# In a separate terminal, start the backend
cd src/app
dotnet run
```

The WPF window opens and loads the React dev server. Frontend changes update instantly without restarting the backend.

## Project Structure

| Path | Purpose |
|------|---------|
| `src/api/` | ASP.NET Core backend — controllers, services, SignalR |
| `src/app/` | WPF shell — window, WebView2, tray, hotkey |
| `src/web/` | React SPA — components, pages, hooks, API client |
| `src/plugins/` | Plugin SDK packages (NuGet + npm) |
| `src/workflow/` | Workflow execution engine and step executors |
| `src/browser-extension/` | Microsoft Edge tab-reuse extension |
| `src/installer/` | Inno Setup installer script |
| `docs/` | Documentation — plugin system, workflow engine, frontend architecture |

## Making Changes

1. Fork the repository and create a branch from `main`.
2. Keep changes focused — one feature or fix per pull request.
3. Test your changes manually before opening a PR.
4. Write a clear PR description explaining what changed and why.

The CI pipeline runs on every PR and produces a build artifact (`DevelopmentHub-Setup-*.exe`) you can use to verify the installer.

## Adding a Workflow Step

1. Add a step model in `src/api/Models/` (inherit from the base step type).
2. Add an executor in `src/workflow/` implementing the executor interface.
3. Register the executor in the workflow engine's DI setup.
4. Document the new step in [docs/workflow-engine/step-reference.md](docs/workflow-engine/step-reference.md).

## Writing a Plugin

See [docs/plugins/getting-started.md](docs/plugins/getting-started.md) for a full walkthrough. The counter plugin in `src/plugins/counter-plugin/` is the canonical example.

## Reporting Issues

Open an issue describing the problem, steps to reproduce, and the version shown in the app title bar.
