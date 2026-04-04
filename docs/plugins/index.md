# Plugin Development

Plugins extend DevelopmentHub with new dashboard widgets, pages, and backend API endpoints — without touching the host application source.

## Documentation Map

- [Overview](./overview.md) — architecture, how plugins are loaded, capabilities
- [Getting Started](./getting-started.md) — create your first plugin from scratch
- [Manifest Reference](./manifest.md) — complete `manifest.json` field reference
- [Frontend Development](./frontend.md) — SDK, JSX setup, registering widgets and routes
- [UI Component Reference](./ui-reference.md) — built-in themed components (`Button`, `Card`, `Input`, …)
- [SDK API Reference](./sdk-reference.md) — full `window.__dhSdk` API
- [Backend Development](./backend.md) — `IPlugin`, controllers, services, dependency injection
- [Examples](./examples.md) — annotated end-to-end examples
- [Troubleshooting](./troubleshooting.md) — common issues and fixes

## What a plugin can do

| Capability | How |
|---|---|
| Add a dashboard widget | `plugin.registerWidget()` + manifest `contributes.widgets` |
| Add a full page with nav link | `plugin.registerRoute()` + manifest `contributes.routes` |
| Expose backend API endpoints | Implement `IPlugin`, add controllers |
| Register background services | `IPlugin.ConfigureServices()` |
| Call host API or external services | `apiFetch(apiBase + '/...')` or direct HTTP from the backend |
| User-configurable settings | `manifest.json settings[]` + `useQuery([pluginId, 'settings'])` |

## Notes

- Plugins are discovered from the configured plugin directory at startup
- Each plugin lives in its own subdirectory containing `manifest.json`
- Frontend bundles are served by the host at `/api/plugins/{id}/ui/bundle.js`
- Backend assemblies are loaded into an isolated `AssemblyLoadContext`
- All SDK objects (React, React Query, etc.) are injected by the host — plugins must not bundle them
