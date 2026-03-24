# Frontend Development

Plugin frontends are compiled ESM bundles that run inside the host's React application. The host injects all dependencies via `window.__dhSdk` before evaluating the bundle.

## Project setup

A minimal frontend plugin needs four files:

```
my-plugin/
├── package.json
├── tsconfig.json
├── vite.config.ts
└── src/
    ├── env.d.ts       ← global type declarations
    ├── index.ts       ← entry point (registers widgets/routes)
    ├── MyWidget.tsx
    └── MyPage.tsx
```

See [Getting Started](./getting-started.md) for the full content of each file.

## The SDK object

Before the bundle runs, the host assigns `window.__dhSdk`. Every value you need — React, hooks, routing, data fetching, the UI library — comes from there.

```ts
// Destructure at the top of each file that needs it
const { React, useState, useQuery, ui, apiBase, plugin } = window.__dhSdk;
```

Never `import React from 'react'` inside a plugin. The host provides React; importing it would bundle a second copy and break hooks.

See [SDK API Reference](./sdk-reference.md) for every available property.

## Why classic JSX transform

The Vite config uses `jsxRuntime: 'classic'`, which compiles JSX to `React.createElement(...)` calls. This means:

1. The `React` variable must be in scope in every `.tsx` file that contains JSX.
2. No import of `react/jsx-runtime` is generated, so the bundle stays lean.

Always destructure `React` from `window.__dhSdk` at the top of any file with JSX:

```tsx
const { React } = window.__dhSdk;
```

## Registering widgets

Call `plugin.registerWidget(widgetId, Component)` once per widget. The `widgetId` must exactly match an entry in `manifest.json contributes.widgets[].id`.

```ts
import MyWidget from './MyWidget';

const { plugin } = window.__dhSdk;
plugin.registerWidget('com.yourname.my-plugin.my-widget', MyWidget);
```

The component receives no props. It is rendered inside the host's `Panel` wrapper, which provides the drag handle, title, and close button. Your component only needs to render the panel body.

## Registering routes

Call `plugin.registerRoute(path, Component)` once per route. The path must match `manifest.json contributes.routes[].path`.

```ts
import MyPage from './MyPage';

const { plugin } = window.__dhSdk;
plugin.registerRoute('/plugins/my-plugin', MyPage);
```

The component is rendered as a full page inside the host's `AppLayout`. Use `<ui.PageRoot>` as the outermost element of every page component so scrolling and spacing work correctly.

## Entry point pattern

All registrations happen in `src/index.ts`:

```ts
import MyWidget from './MyWidget';
import MyPage from './MyPage';

const { plugin } = window.__dhSdk;

plugin.registerWidget('com.yourname.my-plugin.my-widget', MyWidget);
plugin.registerRoute('/plugins/my-plugin', MyPage);
```

## Using the UI framework

The host exposes a set of pre-built, theme-aware components via `window.__dhSdk.ui`. Use these instead of raw CSS class names so your plugin automatically follows the active theme.

```tsx
const { React, ui } = window.__dhSdk;
const { PageRoot, Card, Button, Input, Chip, Empty, Spinner } = ui;

export default function MyPage() {
  return (
    <PageRoot>
      <Card style={{ maxWidth: 480, margin: '2rem auto', padding: '2rem' }}>
        <Button variant="primary">Click me</Button>
      </Card>
    </PageRoot>
  );
}
```

See [UI Component Reference](./ui-reference.md) for all components and their props.

## Using CSS variables directly

All theme tokens are available as CSS custom properties on `:root`. You can use them in `style` props or in any CSS you write:

```tsx
<p style={{ color: 'var(--text-secondary)', fontSize: 13 }}>Some text</p>
```

Key variables:

| Variable | Usage |
|---|---|
| `--color-primary` | Brand/action color |
| `--color-accent` | Accent color |
| `--surface` | Card / panel background |
| `--surface-muted` | Slightly dimmer background |
| `--text-primary` | Primary body text |
| `--text-secondary` | Subdued text |
| `--text-muted` | Placeholder / hint text |
| `--border` | Standard border color |
| `--input-bg` / `--input-border` | Input field colors |
| `--color-error` / `--color-success` | Semantic status colors |

## Calling the backend

Use `apiBase` from the SDK as the base for all API calls:

```tsx
const { apiBase, useQuery } = window.__dhSdk;

const { data } = useQuery({
  queryKey: ['my-plugin', 'data'],
  queryFn: async () => {
    const res = await fetch(`${apiBase}/plugins/my-plugin/data`);
    if (!res.ok) throw new Error('Request failed');
    return res.json();
  },
});
```

`apiBase` is always `'/api'`. Including it explicitly future-proofs your plugin in case the path ever changes.

## Navigation between plugin pages

```tsx
const { useNavigate, Link } = window.__dhSdk;

// Programmatic navigation
const navigate = useNavigate();
navigate('/plugins/my-plugin/detail');

// Declarative link
<Link to="/plugins/my-plugin/detail">Go to detail</Link>
```

## Building

```sh
npm install
npm run build   # outputs ui/index.js
```

During development, use watch mode to rebuild on every save:

```sh
npm run dev
```

Then restart the host (or reload the WebView) to pick up the new bundle.

## Bundle size

SDK dependencies (React, React Query, etc.) are externalized and not included in your bundle. A typical plugin widget compresses to under 5 kB.
