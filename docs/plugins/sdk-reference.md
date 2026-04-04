# SDK API Reference

The entire plugin SDK is available via `window.__dhSdk`. The TypeScript type is `DhSdk`, declared in `src/web/src/plugin-sdk/index.ts`.

```ts
const sdk = window.__dhSdk;
```

---

## React core

The host's React instance. Always use these instead of importing from `react`.

| Property | Type | Description |
|---|---|---|
| `React` | `typeof React` | Full React namespace. Use for `React.createElement`, `React.FC`, types, etc. |
| `useState` | `typeof React.useState` | State hook. |
| `useEffect` | `typeof React.useEffect` | Side-effect hook. |
| `useMemo` | `typeof React.useMemo` | Memoization hook. |
| `useCallback` | `typeof React.useCallback` | Stable callback reference hook. |
| `useRef` | `typeof React.useRef` | Mutable ref hook. |

```tsx
const { React, useState, useEffect } = window.__dhSdk;

export default function MyWidget() {
  const [value, setValue] = useState('');
  useEffect(() => { /* ... */ }, [value]);
  return <input value={value} onChange={e => setValue(e.target.value)} />;
}
```

---

## Data fetching — TanStack Query

Backed by the host's shared `QueryClient`. Cache keys are global — be specific to avoid collisions with other plugins or the host.

| Property | Type | Description |
|---|---|---|
| `useQuery` | `typeof useQuery` | Fetch and cache data. |
| `useMutation` | `typeof useMutation` | Trigger a write operation. |
| `queryClient` | `QueryClient` | Direct access to the query client (invalidate, prefetch, etc.). |

```tsx
const { useQuery, useMutation, queryClient, apiBase, apiFetch } = window.__dhSdk;

// Read
const { data, isLoading, error } = useQuery({
  queryKey: ['com.yourname.my-plugin', 'items'],
  queryFn: () => apiFetch(`${apiBase}/plugins/my-plugin/items`).then(r => r.json()),
});

// Write
const mutation = useMutation({
  mutationFn: (body: unknown) =>
    apiFetch(`${apiBase}/plugins/my-plugin/items`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['com.yourname.my-plugin', 'items'] });
  },
});
```

---

## State management — Zustand

| Property | Type | Description |
|---|---|---|
| `createStore` | `typeof create` (Zustand) | Create an isolated store for your plugin's client-side state. |

```ts
const { createStore } = window.__dhSdk;

interface MyStore { count: number; increment: () => void; }

const useMyStore = createStore<MyStore>((set) => ({
  count: 0,
  increment: () => set((s) => ({ count: s.count + 1 })),
}));
```

Store instances created this way are local to the plugin and not shared with the host.

---

## Navigation — React Router

| Property | Type | Description |
|---|---|---|
| `useNavigate` | `() => NavigateFunction` | Programmatic navigation hook. |
| `Link` | `typeof Link` | Declarative `<Link>` component. |

```tsx
const { useNavigate, Link } = window.__dhSdk;

// Programmatic
const navigate = useNavigate();
navigate('/plugins/my-plugin/detail');

// Declarative
<Link to="/plugins/my-plugin/detail">View detail</Link>
```

---

## Host API

| Property | Type | Description |
|---|---|---|
| `apiBase` | `'/api'` | Base path for all API requests. Always prefix your API calls with this. |
| `apiFetch` | `(input, init?) => Promise<Response>` | Authenticated `fetch` wrapper. Always use this instead of `fetch` — it attaches the required `X-Dev-Hub-Token` header. |

```ts
const { apiBase, apiFetch } = window.__dhSdk;
const res = await apiFetch(`${apiBase}/plugins/my-plugin/data`);
```

---

## Plugin settings

Settings declared in `manifest.json settings[]` are shown as a dedicated page in **Settings → Plugins → *Plugin Name*** and saved immediately on change via `PUT /api/plugins/{id}/settings`.

**Do not** read from `window.__dhSdk.settings` for live values — it is a snapshot taken at bundle load time and will not reflect changes made during the session.

**Do** use `useQuery` with the key `[pluginId, 'settings']`:

```ts
const { useQuery, apiFetch, apiBase } = window.__dhSdk;

const PLUGIN_ID = 'com.yourname.my-plugin';

const { data: settings = {} } = useQuery<Record<string, string>>({
  queryKey: [PLUGIN_ID, 'settings'],
  queryFn: () =>
    apiFetch(`${apiBase}/plugins/${encodeURIComponent(PLUGIN_ID)}/settings`)
      .then(r => r.json()),
});

const myOption = settings['myOption'] ?? 'a';  // falls back to default
```

When the user saves a setting, the host invalidates `[pluginId, 'settings']` automatically and your component re-renders with the new value.

> **Query key contract:** always use `[pluginId, 'settings']` — exactly this two-element array — so the host's invalidation reaches your component.

---

## UI framework

| Property | Type | Description |
|---|---|---|
| `ui` | `PluginUi` | Pre-built, theme-aware React components. |

See [UI Component Reference](./ui-reference.md) for the full component list.

```tsx
const { ui } = window.__dhSdk;
const { Button, Card, Input } = ui;
```

---

## Plugin registration

| Property | Type | Description |
|---|---|---|
| `plugin` | `PluginRegistration` | Registration methods. Call from `src/index.ts` only. |

### `plugin.registerWidget(widgetId, component)`

Registers a React component as a dashboard widget.

- `widgetId` must match an entry in `manifest.json contributes.widgets[].id`.
- `component` is a React component (`() => JSX.Element`). It receives no props.
- The host wraps it in a `Panel` (drag handle, title, close button). The component renders the body only.

```ts
plugin.registerWidget('com.yourname.my-plugin.my-widget', MyWidget);
```

### `plugin.registerRoute(path, component)`

Registers a React component as a full page, accessible via the given URL path.

- `path` must match an entry in `manifest.json contributes.routes[].path`.
- `component` is a React component. It receives no props.
- The host renders it inside `AppLayout`. Use `<ui.PageRoot>` as the root element.

```ts
plugin.registerRoute('/plugins/my-plugin', MyPage);
```

---

## Query key conventions

Use your plugin ID as the first segment of every query key to prevent collisions:

```ts
// Good
queryKey: ['com.yourname.my-plugin', 'items']
queryKey: ['com.yourname.my-plugin', 'items', itemId]

// Bad — too generic, may conflict with host or other plugins
queryKey: ['items']
```
