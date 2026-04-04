# Plugin System Overview

## Architecture

A plugin is a self-contained directory. The host discovers it at startup, reads its `manifest.json`, and optionally loads a compiled frontend bundle and/or a .NET assembly.

```
plugins/
└── my-plugin/
    ├── manifest.json          ← required: describes the plugin
    ├── ui/
    │   └── index.js           ← frontend ESM bundle (built by Vite)
    ├── backend-dist/
    │   └── MyPlugin.dll       ← .NET backend assembly
    └── backend/               ← backend source (.csproj + .cs files)
        ├── MyPlugin.csproj
        ├── MyPlugin.cs
        └── MyController.cs
```

## Plugin directory location

Configure the plugins folder in **Settings → Plugins → Plugins Folder**. Every subdirectory of that folder that contains a `manifest.json` is treated as a plugin. The default when left empty is `%LOCALAPPDATA%\DevelopmentHub\plugins`.

The reference plugin bundled with the repository lives at `src/plugins/counter-plugin/`.

## Load sequence

1. **Manifest read** — host parses `manifest.json` for each subdirectory.
2. **SDK version check** — plugins requiring an unsupported frontend SDK version are skipped.
3. **Backend assembly load** — if `backend.enabled` is `true` and the assembly exists, the host loads it into an isolated `AssemblyLoadContext` and calls `IPlugin.ConfigureServices()` then `IPlugin.Configure()`.
4. **Frontend bundle load** — on first page render the host fetches `/api/plugins/{id}/ui/bundle.js`, injects `window.__dhSdk`, then evaluates the ESM bundle.
5. **Registration** — the bundle calls `plugin.registerWidget()` / `plugin.registerRoute()`, which makes the widget and nav link appear immediately.

## What the host provides to frontend plugins

All dependencies are injected via `window.__dhSdk` before the bundle runs. Plugins must **never** bundle React, React Query, Zustand, or React Router — the host provides the same instances it uses itself, ensuring hooks work correctly across the boundary.

`window.__dhSdk.settings` contains a **snapshot** of the plugin's saved settings taken at bundle load time. Do not use it for live settings — use `useQuery([pluginId, 'settings'], ...)` instead (see [SDK API Reference](./sdk-reference.md#plugin-settings)).

See [SDK API Reference](./sdk-reference.md) for the full list.

## Isolation

- **Frontend**: each plugin's JSX uses the host's React instance — there is one React tree.
- **Backend**: each plugin assembly loads into its own `AssemblyLoadContext`. Shared framework assemblies (ASP.NET Core, `Microsoft.Extensions.*`) are resolved from the host's default context to avoid type-identity mismatches.
- Plugins cannot access each other's code directly.

## Versioning

`manifest.json` declares a `minHostVersion` field. The host may enforce this in future releases. The `frontend.sdkVersion` field is currently checked — only `"1"` is supported.
