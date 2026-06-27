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

## Commit Message Convention

This project uses [Conventional Commits](https://www.conventionalcommits.org/) to automate versioning and changelog generation via [release-please](https://github.com/googleapis/release-please).

| Prefix | When to use | Version bump |
|--------|-------------|--------------|
| `feat:` | New user-facing feature | minor (`1.0.0` → `1.1.0`) |
| `fix:` | Bug fix | patch (`1.0.0` → `1.0.1`) |
| `perf:` | Performance improvement | patch |
| `refactor:` | Internal restructure, no behaviour change | patch |
| `docs:` | Documentation only | none (hidden in changelog) |
| `chore:` | Maintenance, dependency bumps | none (hidden in changelog) |
| `test:` | Test additions or changes | none (hidden in changelog) |
| `ci:` | CI/CD changes | none (hidden in changelog) |

For a **breaking change**, append `!` after the type or add `BREAKING CHANGE:` in the commit footer — this triggers a major bump (`1.0.0` → `2.0.0`).

```
feat!: remove support for legacy workflow format

BREAKING CHANGE: workflow files must be updated to the v2 schema.
```

Release-please runs on every push to `main` and opens or updates a Release PR that bumps `version.txt` and prepends a changelog entry. Merging that PR publishes the GitHub Release and triggers the build workflow to attach artifacts.

## Making Changes

1. Fork the repository and create a branch from `main`.
2. Keep changes focused — one feature or fix per pull request.
3. Use the Conventional Commits format for all commit messages (see above).
4. Test your changes manually before opening a PR.
5. Write a clear PR description explaining what changed and why.

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
