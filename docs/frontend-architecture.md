# Frontend Architecture

## Overview

The frontend is a React + TypeScript single-page application rendered inside a WebView2 window. It uses TanStack Query for server state, Zustand for client UI state, and React Router for navigation.

```
src/web/src/
├── api/            # Typed fetch wrappers per domain
├── components/     # Shared UI components (reused across ≥2 pages)
│   └── settings/   # Settings modal section components
├── hooks/          # Custom hooks for shared data/mutation logic
├── pages/
│   ├── dashboard/
│   │   ├── widgets/    # Widget components (used on dashboard + full pages)
│   │   └── components/ # Dashboard-only components (Panel)
│   ├── repositories/
│   ├── pull-requests/
│   ├── todos/
│   ├── workflows/
│   └── quick-links/
├── plugins/        # Plugin loader, registry, and host SDK injection
├── store/          # Zustand UI store (layouts, theme, widget list)
├── types/          # Shared TypeScript interfaces
└── plugin-sdk/     # @developmenthub/plugin-sdk type definitions
```

---

## Layer responsibilities

### `api/`

One file per domain. Each file exports plain async functions that call `fetch` and throw on error. No React code here — these are framework-agnostic.

```
api/config.ts          configApi.get / save
api/repositories.ts    repositoriesApi.getAll / open / openWorkspace / ...
api/pullRequests.ts    fetchPullRequests
api/todos.ts           todosApi.getAll / create / update / toggle / delete / clearCompleted
api/workflows.ts       workflowsApi.getAll / run / getExecution
```

### `hooks/`

Reusable logic that involves React state or TanStack Query mutations. A hook lives here when the same mutation or effect is needed in more than one component.

| Hook | Used by |
|---|---|
| `useTodos` | `DashboardPage`, `TodosPage` |
| `useRepositoryScan` | `DashboardPage`, `RepositoriesPage`, `DashboardSettingsModal` |
| `useWorkflowModals` | `DashboardPage`, `WorkflowsPage` |
| `useRepositoryHub` | `DashboardPage` |
| `useLogHub` | `DashboardPage` (workflow execution log) |

### `components/`

Shared UI components used across two or more pages. Widget-specific sub-components stay in the `widgets/` folder.

| Component | Purpose |
|---|---|
| `AppLayout` | Navigation, header, WebView2 bridge |
| `FilterToolbar` | Search input + filter buttons used by all five full pages |
| `ErrorBar` | Dismissible inline error banner used by all widgets |
| `Modal` | Overlay + card shell used by workflow modals |
| `OpenerIcon` | Repository opener icon (VS Code / Visual Studio / custom) |
| `TagEditor` | Inline tag chip editor used in the Repositories page |
| `DashboardSettingsModal` | Settings shell; delegates to `settings/` section components |

---

## Widget / Page pattern

Every feature ships **two surfaces** built from the same widget component:

```
Widget (pure display)
│
├── Dashboard panel  — Panel wrapper, compact layout, receives data from DashboardPage
└── Full page        — FilterToolbar wrapper, full-width layout, fetches its own data
```

The widget component is **props-only**: it receives data and callbacks and has no knowledge of where it lives. The two surfaces provide the data differently:

- **DashboardPage** fetches all data once via `useQuery` and passes it to each widget. Mutations are shared via the `useTodos`, `useRepositoryScan`, and `useWorkflowModals` hooks.
- **Full pages** each call `useQuery` independently with the same cache keys, so React Query deduplicates the network requests when both surfaces are mounted.

### Example — Todos

```
useTodos hook          (create / update / toggle / delete / clearCompleted mutations)
    │
    ├── DashboardPage  → <TodosWidget todos={filtered} onCreate={...} ... />
    └── TodosPage      → <FilterToolbar /> + <TodosWidget todos={filtered} onCreate={...} ... />
```

`TodosWidget` itself is composed of three sub-components:
- `TodoCreateForm` — the new-todo input row
- `TodoItem` — a single active todo (display + inline edit modes)
- `TodosCompletedSection` — the collapsible completed list

---

## Dashboard widget system

The dashboard is a drag-and-drop grid powered by `react-grid-layout`. The widget registry lives in two places:

### 1. `uiStore` (Zustand) — identity and layout

```ts
DashboardWidget { id: string; label: string; icon: string; enabled: boolean }
```

Each widget has a single unique `id` string (e.g. `"todos"`). The grid layout is stored per breakpoint (`lg`, `md`, `sm`) in `localStorage`, keyed by this same `id`.

Built-in IDs: `repositories`, `pullRequests`, `quickLinks`, `todos`, `workflows`.

Plugin widgets are added at runtime via `addPluginWidget()`.

### 2. `widgetMap` in `DashboardPage` — rendering

`DashboardPage` builds a `widgetMap: Record<string, WidgetConfig>` that maps each `id` to:
- the JSX body to render inside the panel
- the panel title, badge count, and optional header actions

The grid iterates `dashboardWidgets`, looks up each `id` in `widgetMap`, and renders a `<Panel>` around it.

### Limitations

**Only one instance of each widget type is supported.** The `id` is used as both the unique layout key and the `widgetMap` lookup key, so placing two `"todos"` widgets would require distinct IDs but currently no mechanism exists to differentiate them from the same widget type. Supporting multiple instances would require:

1. Separating instance ID from widget type ID (e.g. instance `"todos-2"`, type `"todos"`)
2. Deriving the widget type from the instance ID in `widgetMap` lookups
3. Allowing `addWidget` in the store to create new instances rather than toggling visibility

---

## Plugin system

Plugins are loaded at startup by `PluginLoader`, which calls `GET /api/plugins` to get the list of registered plugins and their frontend bundle paths.

Each plugin bundle is injected as a `<script>` tag. Before injection, the host calls `initPluginLoader(queryClient)` to populate `window.__dhSdk` with:
- React, hooks (`useState`, `useEffect`, `useMemo`, `useCallback`, `useRef`)
- TanStack Query (`useQuery`, `useMutation`, `queryClient`)
- Zustand (`createStore`)
- React Router (`useNavigate`, `Link`)
- Host API base path (`apiBase: "/api"`)
- Themed UI components (`ui.Button`, `ui.Card`, `ui.Input`, `ui.Chip`, `ui.Empty`, `ui.PageRoot`, `ui.Spinner`)
- Registration functions (`plugin.registerWidget`, `plugin.registerRoute`)

Once the bundle executes, the registered widgets and routes are available via `PluginRegistry`. `DashboardPage` then renders them alongside built-in widgets; `App.tsx` adds their routes to React Router.

See [`@developmenthub/plugin-sdk`](../src/web/src/plugin-sdk/index.ts) for the full TypeScript interface.

---

## Data flow

```
User action
    │
    ▼
Component calls mutation  (e.g. createTodo.mutateAsync(...))
    │
    ▼
api/ function             (fetch POST /api/todos)
    │
    ▼
onSuccess: invalidateQueries(["todos"])
    │
    ▼
React Query refetches     (all components subscribed to ["todos"] re-render)
```

Server-push updates (repository scan results) arrive via SignalR (`useRepositoryHub`) and call `queryClient.setQueryData` directly to avoid an extra round-trip.

---

## State management

| Concern | Location |
|---|---|
| Server data (repositories, PRs, todos, etc.) | TanStack Query cache |
| Dashboard layout, theme, widget visibility | Zustand `uiStore` + `localStorage` |
| Edit mode, modal open/close | Local `useState` in the component that owns the interaction |
| Shared mutation state (isBusy, error) | Custom hooks (`useTodos`, `useWorkflowModals`) |
