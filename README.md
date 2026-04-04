# DevelopmentHub

[![Build & Release](https://github.com/SeBaconStrip/DevelopmentHub/actions/workflows/build.yml/badge.svg)](https://github.com/SeBaconStrip/DevelopmentHub/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-blue)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512bd4)
![React 19](https://img.shields.io/badge/React-19-61dafb)
![TypeScript](https://img.shields.io/badge/TypeScript-5.9-3178c6)

A Windows desktop developer dashboard that consolidates your daily tools into one keyboard-accessible window. Monitor repositories, review pull requests, manage todos, run automation workflows, and extend everything with a first-class plugin system — all without leaving your keyboard.

---

## Features

**Repositories** — Scan local folders for Git repos. See branch, ahead/behind status, and open any repo in VS Code, Visual Studio, or a custom tool with one click. Multi-select repos to open a shared `.code-workspace`. Tag and filter repos. Inline warnings for fetch failures, invalid paths, or permission issues.

**Pull Requests** — Merged feed from Azure DevOps and GitHub. Filter by All / Mine / Reviewer / Draft. Click a PR to open it in your browser via the companion Edge extension, which reuses an existing tab rather than opening a new one.

**Todos** — Local todo list with bidirectional sync to Microsoft To Do (Microsoft Graph). Create, complete, restore, and delete tasks. Background sync keeps both sides consistent; a manual "Sync Now" button is available.

**Workflow Engine** — File-based automation engine. Define workflows in JSON and run them from the dashboard with optional user inputs. Steps cover: shell scripts, file downloads (GitHub/Azure DevOps authenticated), ZIP extraction, JSON patching, running executables, and restarting Windows services. Workflows support UAC elevation, static variables, built-in path variables, sub-workflow calls, and real-time execution logs.

**Quick Links** — Pinned URLs and Explorer folders. One click to open.

**Plugin System** — First-class extensibility. Plugins contribute dashboard widgets and full pages without modifying the host. The backend loads plugins into isolated `AssemblyLoadContext`s; the frontend injects a typed SDK (`window.__dhSdk`) with React, TanStack Query, Zustand, React Router, and a pre-built themed UI component set.

**Quality-of-life** — Global hotkey to show/hide the window, hide-to-tray, frameless window with VS Code dark theme, drag-and-drop resizable dashboard panels, SignalR real-time updates.

---

## Architecture

```
src/
├── api/              ASP.NET Core backend (Kestrel, LiteDB, LibGit2Sharp)
├── app/              WPF host shell (WebView2 embeds the React frontend)
├── web/              React 19 + TypeScript + TailwindCSS SPA
├── plugins/          Plugin SDK — DevelopmentHub.Plugins NuGet + @developmenthub/plugin-sdk npm
├── workflow/         Workflow execution engine
├── browser-extension/  Microsoft Edge extension (tab reuse for PR links)
└── installer/        Inno Setup installer script
```

The WPF shell hosts an embedded Kestrel server and a WebView2 control pointed at it. A per-process authentication token is injected into the WebView at startup; all API calls require the `X-Dev-Hub-Token` header. SignalR pushes real-time updates (repository scans, todo syncs) to the frontend without polling.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Desktop shell | WPF + WebView2 (.NET 9) |
| Backend | ASP.NET Core 9, Kestrel |
| Database | LiteDB (embedded, no server) |
| Git | LibGit2Sharp |
| Real-time | SignalR |
| Frontend | React 19, TypeScript 5.9, Vite 7 |
| Styling | TailwindCSS 4 |
| State | Zustand 5, TanStack Query 5 |
| CI/CD | GitHub Actions → Inno Setup installer + GitHub Releases |

---

## Prerequisites

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- [Node.js 20+](https://nodejs.org/)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (included with Windows 11; install separately on Windows 10)

---

## Getting Started

### Development

```bash
# 1. Install frontend dependencies
cd src/web
npm ci

# 2. Run the frontend dev server (hot reload on port 5173 by default)
npm run dev

# 3. In a separate terminal, run the backend in Development mode
cd src/app
dotnet run
```

The WPF window opens and loads the React dev server. Changes to frontend files update instantly.

### Production Build

```bash
# Build frontend, then publish the self-contained Windows executable
cd src/app
dotnet publish -c Release -r win-x64 --self-contained -o ../../publish
```

The publish step automatically runs `npm run build` and copies the `dist/` output into `wwwroot/` inside the executable's directory.

### Installer

The CI pipeline builds an Inno Setup installer (`DevelopmentHub-Setup-*.exe`) on every push to `main`. Download the latest from [Releases](../../releases).

---

## Configuration

On first run, open **Settings** (gear icon) and configure:

- **Repositories → Folders** — root directories to scan for Git repos
- **Integrations → Azure DevOps** — organization, project, user email, PAT (`Code: Read`, `Pull Request: Read`)
- **Integrations → GitHub** — PAT (`repo: read`, `user: read`)
- **Todos → Microsoft To Do** — Azure AD app registration client ID for device-code auth

All settings are stored in `appsettings.local.json` (gitignored) and LiteDB.

---

## Plugin Development

Plugins can add dashboard widgets and full pages without touching the host code.

**Minimal plugin structure:**
```
my-plugin/
├── manifest.json          Plugin metadata and contribution declarations
├── MyPlugin.dll           Backend (implements IPlugin, optional)
└── bundle.js              Frontend (calls window.__dhSdk.plugin.register*, optional)
```

Install the SDKs:
```bash
# .NET backend SDK
dotnet add package DevelopmentHub.Plugins

# TypeScript frontend types
npm install --save-dev @developmenthub/plugin-sdk
```

See [docs/plugins/](docs/plugins/) for the full guide, manifest reference, SDK API, and a worked example (the counter plugin in `src/plugins/counter-plugin/`).

---

## Workflow Engine

Workflows are JSON files in a configured folder. Example — restart a Windows service:

```json
{
  "name": "Restart API Service",
  "runElevated": true,
  "steps": [
    {
      "type": "restartWindowsService",
      "serviceName": "MyApiService"
    }
  ]
}
```

See [docs/workflow-engine/](docs/workflow-engine/) for the full schema, all step types, variables, sub-workflows, elevation, and examples.

---

## Documentation

| Topic | Link |
|-------|------|
| Plugin system | [docs/plugins/](docs/plugins/) |
| Workflow engine | [docs/workflow-engine/](docs/workflow-engine/) |
| Frontend architecture | [docs/frontend-architecture.md](docs/frontend-architecture.md) |
| Changelog | [CHANGELOG.md](CHANGELOG.md) |

---

## License

[MIT](LICENSE)
