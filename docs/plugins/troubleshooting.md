# Troubleshooting

## Frontend

### Plugin does not appear after restart

1. Check the host logs for lines containing the plugin ID — a warning indicates why it was skipped.
2. Verify `manifest.json` is valid JSON (`frontend.sdkVersion` must be `"1"`).
3. Confirm `ui/index.js` exists (run `npm run build`).
4. Confirm `frontend.bundle` in `manifest.json` points to the correct relative path.

### Widget appears but is blank / throws an error

Open browser DevTools (F12 in the WebView) and check the console for errors.

Common causes:

- **"Cannot read properties of undefined (reading '…')"** — you are trying to read a property of something that is `undefined`. This is often caused by destructuring from `window.__dhSdk` incorrectly or by a mismatched property name.
- **JSX renders nothing** — ensure `React` is in scope in every `.tsx` file. With the classic JSX transform (`jsxRuntime: 'classic'`), you must destructure `React` at the top of every file containing JSX.

### Build error: `Rollup failed to resolve import "react/jsx-runtime"`

The `vite.config.ts` is missing `'react/jsx-runtime'` from the `external` list, **or** the plugin is using the automatic JSX runtime (`jsxRuntime: 'automatic'` or not set). Use `classic`:

```ts
plugins: [react({ jsxRuntime: 'classic' })],
rollupOptions: {
  external: ['react', 'react-dom', 'react/jsx-runtime', ...],
},
```

### TypeScript error: `Property '__dhSdk' does not exist on type 'Window'`

The `tsconfig.json` is missing or does not include `src/`. Ensure:

1. `tsconfig.json` exists in the plugin root.
2. It has `"include": ["src"]`.
3. `src/env.d.ts` contains `declare global { interface Window { __dhSdk: DhSdk; } }`.

### TypeScript error: Parameter `e` implicitly has type `any`

Usually a consequence of `window.__dhSdk` not being typed (see above). Fix the `tsconfig.json` / `env.d.ts` issue first.

### Hook rules violation: "Hooks can only be called inside a function component"

This happens when two copies of React are loaded. Ensure:

- `react` and `react-dom` are in the `external` list in `vite.config.ts`.
- You never `import React from 'react'` anywhere in the plugin source.

---

## Backend

### Backend not loaded / no log line for the plugin

1. Confirm `manifest.json` has `"backend": { "enabled": true }`.
2. Confirm the DLL exists at the path specified by `backend.assembly` (relative to the plugin dir).
3. Run `dotnet build backend/MyPlugin.csproj` and check for compiler errors.

### `No IPlugin implementation found in {Assembly}`

The compiler produced the DLL but it does not contain a class implementing `IPlugin`. Check:

- The class is `public` and `non-abstract`.
- It directly implements `DevelopmentHub.Plugins.IPlugin`.
- The `DevelopmentHub.Plugins` project reference resolves correctly.

### Controller routes return 404

Verify that `ConfigureServices` calls:

```csharp
services.AddControllers()
    .AddApplicationPart(typeof(MyPlugin).Assembly);
```

Without `AddApplicationPart`, MVC does not scan the plugin assembly for controllers.

Also check that your route attribute prefix is under `/api/plugins/`:

```csharp
[Route("api/plugins/my-plugin")]
```

### Dependency injection error at startup

If a service registered in `ConfigureServices` fails to resolve, the host logs the exception at startup. Check:

- All constructor dependencies are also registered (either in the plugin or already in the host).
- Third-party packages that are not in the host have their DLLs present in `backend-dist/`. If a package is in the host and your plugin brings its own version, the host version wins — ensure version compatibility.

### `FileNotFoundException` for a third-party DLL

The `PluginAssemblyLoadContext` resolves dependencies from the plugin's output directory using `AssemblyDependencyResolver`. Ensure the missing DLL is in `backend-dist/`.

If you set `Private="false"` and `ExcludeAssets="runtime"` on the `DevelopmentHub.Plugins` reference, those settings prevent only the SDK DLL from being copied. All other NuGet package DLLs will still be copied to `backend-dist/` by default.

---

## General

### Plugin loads on first run but fails after rebuild

The host loads assemblies once at startup. After rebuilding the backend, restart the host application.

For the frontend, the bundle is served with `Cache-Control: no-store`. Rebuild `ui/index.js` and reload the WebView — no host restart needed.

### `manifest.json` changes are not picked up

The manifest is read at startup. Restart the host after any change to `manifest.json`.

### Settings changes in the UI are not reflected in the plugin

If your plugin reads `window.__dhSdk.settings`, it will always show the value from bundle load time. Switch to `useQuery`:

```ts
const { data: settings = {} } = useQuery<Record<string, string>>({
  queryKey: [PLUGIN_ID, 'settings'],
  queryFn: () =>
    apiFetch(`${apiBase}/plugins/${encodeURIComponent(PLUGIN_ID)}/settings`)
      .then(r => r.json()),
});
```

The host automatically invalidates this query when the user saves a setting, causing the component to re-render with the new value.

### Settings page shows stale values after closing and reopening settings

This is caused by `PUT /api/config` overwriting the plugin's non-`enabled` settings with snapshot data. The host's config endpoint only manages the `enabled` flag per plugin — all other settings are owned by `PUT /api/plugins/{id}/settings`. Ensure you are not sending plugin settings through the config endpoint.
