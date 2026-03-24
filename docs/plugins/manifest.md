# Manifest Reference

Every plugin must contain a `manifest.json` file at the root of its directory.

## Full schema

```json
{
  "id": "com.yourname.my-plugin",
  "version": "1.0.0",
  "name": "My Plugin",
  "description": "Short description shown in the UI.",
  "minHostVersion": "0.10.0",

  "backend": {
    "assembly": "backend-dist/MyPlugin.dll",
    "enabled": true
  },

  "frontend": {
    "bundle": "ui/index.js",
    "enabled": true,
    "sdkVersion": "1"
  },

  "contributes": {
    "widgets": [
      {
        "id": "com.yourname.my-plugin.my-widget",
        "label": "My Widget",
        "icon": "🔌",
        "defaultLayout": { "w": 4, "h": 6 }
      }
    ],
    "routes": [
      {
        "path": "/plugins/my-plugin",
        "navLabel": "My Plugin",
        "navOrder": 100
      }
    ]
  }
}
```

## Top-level fields

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | yes | Unique plugin identifier. Use reverse-domain notation: `com.yourname.plugin-name`. |
| `version` | string | yes | Plugin version string. |
| `name` | string | yes | Human-readable name shown in the host. |
| `description` | string | no | Short description. |
| `minHostVersion` | string | no | Minimum host version required. Not yet enforced but reserved. |
| `backend` | object | no | Backend assembly configuration. Omit if the plugin has no backend. |
| `frontend` | object | no | Frontend bundle configuration. Omit if the plugin has no UI. |
| `contributes` | object | no | Widgets and routes the plugin contributes. |

## `backend`

| Field | Type | Default | Description |
|---|---|---|---|
| `assembly` | string | — | Path to the compiled DLL, relative to the plugin directory. |
| `enabled` | bool | `true` | Set to `false` to disable backend loading without removing the manifest entry. |

The assembly must contain exactly one non-abstract class implementing `IPlugin`. The host will skip the plugin (with a warning) if the file is missing or no `IPlugin` implementation is found.

## `frontend`

| Field | Type | Default | Description |
|---|---|---|---|
| `bundle` | string | — | Path to the compiled ESM bundle, relative to the plugin directory. |
| `enabled` | bool | `true` | Set to `false` to disable frontend loading. |
| `sdkVersion` | string | — | Required. Must be `"1"`. Plugins with a different value are skipped. |

## `contributes.widgets[]`

| Field | Type | Description |
|---|---|---|
| `id` | string | Unique widget ID. Must start with the plugin ID. Passed to `plugin.registerWidget()`. |
| `label` | string | Display name shown in the dashboard panel header and widget picker. |
| `icon` | string | Emoji or short string shown in the panel header. |
| `defaultLayout.w` | number | Default width in grid columns (max 12). |
| `defaultLayout.h` | number | Default height in grid rows. |

## `contributes.routes[]`

| Field | Type | Description |
|---|---|---|
| `path` | string | URL path. Must start with `/plugins/`. Passed to `plugin.registerRoute()`. |
| `navLabel` | string | Label shown in the sidebar navigation. |
| `navOrder` | number | Relative sort order among plugin nav links. Lower numbers appear first. |

## Notes

- JSON property names are read **case-insensitively** by the host.
- All paths inside `manifest.json` are relative to the plugin's own directory.
- A plugin with neither `backend` nor `frontend` sections is loaded but contributes nothing — this is valid.
