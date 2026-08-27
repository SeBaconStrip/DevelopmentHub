# Changelog

All notable changes are documented here. Starting with v1.1.0 this file is
managed automatically by [release-please](https://github.com/googleapis/release-please)
from [Conventional Commits](https://www.conventionalcommits.org/).

<!-- release-please inserts new version sections above this comment -->

---

> **Historical changelog** — entries below were written manually before
> release-please was adopted (v0.1.0 – v1.0.0).

---

## [1.2.0](https://github.com/SeBaconStrip/DevelopmentHub/compare/v1.1.1...v1.2.0) (2026-08-27)


### Features

* show application version in settings ([#55](https://github.com/SeBaconStrip/DevelopmentHub/issues/55)) ([958bac0](https://github.com/SeBaconStrip/DevelopmentHub/commit/958bac015093577e9c341aa6aaa51616a4100342))


### Bug Fixes

* make plugin labels and status colours theme-aware ([#54](https://github.com/SeBaconStrip/DevelopmentHub/issues/54)) ([dabfeeb](https://github.com/SeBaconStrip/DevelopmentHub/commit/dabfeeb8bb6dc5c7948ce728151a0e89c4224829))
* report why a plugin frontend bundle failed to load ([#56](https://github.com/SeBaconStrip/DevelopmentHub/issues/56)) ([f070591](https://github.com/SeBaconStrip/DevelopmentHub/commit/f0705916a9ac04c257b8d1d140b0088ce0b9d693))

## [1.1.1](https://github.com/SeBaconStrip/DevelopmentHub/compare/v1.1.0...v1.1.1) (2026-08-26)


### Bug Fixes

* upload release assets from release-please run ([#50](https://github.com/SeBaconStrip/DevelopmentHub/issues/50)) ([43bf4ad](https://github.com/SeBaconStrip/DevelopmentHub/commit/43bf4ad9c7385ee87a0b9467bf37120f29581f40))

## [1.1.0](https://github.com/SeBaconStrip/DevelopmentHub/compare/v1.0.7...v1.1.0) (2026-08-17)


### Features

* migrate versioning and changelog to release-please ([f6f32e6](https://github.com/SeBaconStrip/DevelopmentHub/commit/f6f32e6133f0dcd837b91083027450a93408f2bf))
* migrate versioning and changelog to release-please ([3d0f643](https://github.com/SeBaconStrip/DevelopmentHub/commit/3d0f64349941b50d7d14b6f80802b23ff358942e))
* migrate versioning and changelog to release-please ([#44](https://github.com/SeBaconStrip/DevelopmentHub/issues/44)) ([fb52b88](https://github.com/SeBaconStrip/DevelopmentHub/commit/fb52b886cdc063b10c05c46c38bb74faff37c347))
* Use az cli (if available) for faster downloads from azure devops ([#46](https://github.com/SeBaconStrip/DevelopmentHub/issues/46)) ([7c99f2d](https://github.com/SeBaconStrip/DevelopmentHub/commit/7c99f2da8a529332955ebb1ee39d8e406a895993))


### Bug Fixes

* trigger release please after config fix ([#47](https://github.com/SeBaconStrip/DevelopmentHub/issues/47)) ([e2a7830](https://github.com/SeBaconStrip/DevelopmentHub/commit/e2a783087f308721322706e894d4ea451d3680f1))

## [1.0.0] (2026-05-02)

### Features

- Public release — repository is now open source
- `SearchInput` component in the plugin SDK (`window.__dhSdk.ui.SearchInput`) — themed search input with an inline ✕ clear button; requires controlled `value` + `onChange` props

### Bug Fixes

- Repositories page sort order now matches the dashboard widget — favorites first, then by usage score descending; previously the page sorted alphabetically by name within each group, diverging from the backend and widget order
- `PatchJsonExecutor` — `{{placeholder}}` rendering now recurses into array elements and nested object string values; previously only top-level string values were rendered, leaving placeholders unexpanded in `set` operations that supply an array or object as the value
- `PatchJsonExecutor` — when an input name matches a JSON path property name (e.g. input `Path` + path `$.Path` + value `{{Path}}`), the user-provided input value is now used correctly; previously the template could resolve to the wrong value

### Miscellaneous Chores

- npm dependencies updated (`npm audit fix`) — 0 vulnerabilities
- All `FilterToolbar` search inputs now show an inline ✕ clear button on the right when the field is non-empty

---

## [0.18.0] (2026-04-06)

### Features

- Workflow tags — add a `"tags"` array to any workflow JSON file to label and group workflows (e.g. `"tags": ["deploy", "build"]`)
- Tag chips displayed on each workflow card in both the dashboard widget and the Workflows page
- Tag filter bar on the Workflows page — click a tag chip to filter the list to matching workflows; click again to clear
- Clicking a tag chip on a workflow card on the Workflows page activates the corresponding tag filter
- Dashboard scroll padding — the dashboard page adds extra bottom padding automatically when the content is tall enough to scroll, preventing the last widget from sitting flush against the viewport edge

### Bug Fixes

- Workflow tags were silently dropped by the `Normalize` method in `WorkflowService` when reconstructing the `WorkflowDefinition` object — `Tags` is now forwarded correctly
- Adding a new repository root path no longer requires F5 to see the updated list — the frontend now subscribes to a new `ScanStarted` SignalR event and shows a spinner on the refresh button while the scan is running; `RepositoriesUpdated` clears the spinner and reloads the list as before
- Repositories page was not connected to SignalR — it now subscribes to `RepositoriesUpdated` so the list refreshes automatically after a scan without a page reload
- Config save now immediately invalidates the `repositories` query so orphan removal (deleted root paths) is reflected at once, without waiting for the full scan to complete
- Redundant `POST /api/repositories/scan` call from the settings modal removed — the backend already triggers a scan internally on every config save
- DevTools per F12 öffnen — auch in der Production-Version öffnet F12 das WebView2-DevTools-Fenster; `AreDevToolsEnabled` ist jetzt immer aktiviert
- Plugin-Ladefehler in den Einstellungen sichtbar — lädt ein Plugin-Bundle nicht (Netzwerkfehler, Laufzeitfehler im Bundle), erscheint unter dem entsprechenden Plugin-Eintrag ein roter Hinweistext mit der Fehlermeldung; `getPluginLoadErrors()` in `PluginLoader.ts` exportiert eine readonly Map (pluginId → Fehlermeldung)

### Miscellaneous Chores

- Refresh button on both the Repositories dashboard widget and the Repositories page becomes a spinner while a scan is in progress, replacing the separate scan-banner approach
- `.scan-spinner` added as a shared global CSS utility in `index.css`, available across all pages without per-component CSS imports
- `README.md` — Todos and Quick Links sections now include widget screenshots; `screenshot.mjs` extended to capture the Todos widget alongside the existing four

---

## [0.17.0] (2026-04-06)

### Features

- Unit test suite — `src/tests/` (xUnit, Moq, FluentAssertions) covering `TodoService`, `UserConfigService`, `RepositoryService`, `PullRequestService`, `WorkflowService`, and workflow executors (`CopyExecutor`, `ExtractArchiveExecutor`, `PatchJsonExecutor`); 80 tests total
- Frontend test suite — `src/web/src/__tests__/` (Vitest, React Testing Library) covering API clients (`client`, `todos`, `repositories`), hooks (`useTodos`, `useRepositoryScan`), and utilities (`repositoryUtils`)
- CI test steps — backend (`dotnet test`) and frontend (`npm test`) now run on every PR and push before the publish step, blocking the build on failure

### Miscellaneous Chores

- Architecture diagram in `README.md` replaced with a Mermaid flowchart showing the WPF shell → WebView2 → Kestrel → plugin/workflow/external-service relationships
- `README.md` updated with `CONTRIBUTING.md` link in the documentation table
- `CONTRIBUTING.md` added — covers setup, project structure, PR guidelines, and quick guides for adding workflow steps and plugins
- `LICENSE` (MIT) added

---

## [0.16.0] (2026-04-04)

### Features

- Per-plugin settings pages — each plugin with declared `settings[]` gets a dedicated sub-page under **Settings → Plugins → _Plugin Name_**; settings save immediately on change without a Save button
- `GET /api/plugins/{id}/settings` and `PUT /api/plugins/{id}/settings` — dedicated endpoints that own all non-`enabled` plugin settings; `PUT /api/config` now only manages the `enabled` flag per plugin
- Bundle cache-busting via `BundleMtime` — the host sets a cache-buster on the bundle URL based on the file's last-write timestamp rather than the manifest version, so rebuilt bundles are always picked up without bumping the version string
- `apiFetch` exposed on `window.__dhSdk` — authenticated `fetch` wrapper that attaches the required `X-Dev-Hub-Token` header; plugins should use this instead of raw `fetch`
- Counter plugin (`src/plugins/counter-plugin/`) replaces the old example plugin — demonstrates a configurable step setting read live via `useQuery`

### Bug Fixes

- Plugin folder not loading on startup when the path was stored in LiteDB — the startup DB read now happens before the singleton opens the file, avoiding the exclusive lock
- Plugin settings reverting to wrong values on reopening the settings modal — caused by `PUT /api/config` overwriting all `PluginSettings`; fixed by `MergePluginEnabledFlags()`
- `ReferenceError: Field is not defined` crash when opening plugin settings — `Field` was removed from the import in `SettingsSectionPlugins.tsx` but was still referenced on the folder path input
- Plugin settings not applying without a page reload — counter plugin bundle was stale; fixed by running `npm run build` and introducing `BundleMtime`-based cache-busting as a permanent solution

### Miscellaneous Chores

- Plugin settings are now split from host config: `PUT /api/config` merges only the `enabled` flag using `MergePluginEnabledFlags()`; all other setting keys are preserved, preventing settings from reverting when the config modal is saved
- Plugin bundle endpoint sets `Cache-Control: no-store` so the browser never serves a stale bundle
- Plugin startup DB read moved before `DashboardDatabase` singleton registration to avoid a LiteDB exclusive file-lock conflict that silently prevented plugins from loading when a plugins folder path was stored in the DB
- `window.__dhSdk.settings` documented as a bundle-load snapshot only — plugins must use `useQuery([pluginId, 'settings'])` for live values; the host invalidates this query automatically after a settings save
- Example plugin replaced with the counter plugin; old `plugins/example-plugin/` and `src/plugins/example-plugin/` directories removed
- Plugin documentation fully updated: `overview.md`, `manifest.md`, `sdk-reference.md`, `frontend.md`, `getting-started.md`, `examples.md`, `troubleshooting.md`, `index.md`

---

## [0.15.0] (2026-03-29)

### Features

- Workflow-level elevation — set `runElevated: true` on the workflow to issue a single UAC prompt at startup; all steps that require admin rights run through a shared elevated helper process for the duration of the workflow with no further prompts
- Elevated helper communicates with the main process over a TCP loopback connection (non-elevated process is the server on a random OS-assigned port) — avoids named-pipe access restrictions between elevation levels
- `bool` input type — renders a checkbox; submitted value is `"true"` or `"false"`
- `select` input type — renders a dropdown; requires an `options` array; submitted value is the selected option string
- Elevated badge shown on workflow cards and the input modal when `runElevated` is set
- `variables` block — declare static key-value pairs inside the workflow definition; available as `{{placeholders}}` in all step fields without prompting the user
- Built-in variables `workflowDir` and `workflowFile` — automatically injected into every workflow from the location of its JSON file; useful for referencing scripts or config files stored alongside the workflow

### Bug Fixes

- `GET /api/icon-extractor`: path is now resolved to an absolute path before use, neutralising directory traversal sequences; requests for file types outside `.exe`, `.dll`, `.ico`, and `.com` are rejected
- `RepositoryService.OpenAsync`: caller-supplied `EntryPointPath` is validated to be within the repository directory before being passed to the launcher; scanned entry points remain trusted
- `RepositoryService.OpenWorkspaceAsync`: workspace JSON is now built with `JsonSerializer.Serialize` instead of string interpolation, preventing JSON injection via repository paths containing quote characters
- All exception `Message` values that were previously returned to clients in API error responses are replaced with generic messages; full exceptions continue to be logged server-side
- `RestartWindowsServiceExecutor`: the validated service name is now assigned to a `$ServiceName` PowerShell variable once at script startup; all cmdlets reference the variable rather than re-interpolating user data into command text

### Miscellaneous Chores

- Variable resolution follows a four-tier priority chain: declared `variables` → built-in variables → input defaults → user-provided input values
- Workflow-level `runElevated` supersedes per-step `runElevated` flags — when the workflow-level worker is active, individual step flags are ignored and all privileged operations route through the shared helper
- Removed `requiresConfirmation` — the field had no meaningful use case and has been removed from the schema, backend, and frontend

---

## [0.14.0] (2026-03-29)

### Features

- Microsoft To Do sync — bidirectional todo synchronisation via Microsoft Graph API; connect with an Azure AD app registration (device code flow, no redirect server required)
- Background sync service runs on a configurable interval (default 300 s, minimum 30 s); interval is re-read from config each cycle without a restart
- SignalR push notification (`TodosUpdated`) emitted after every background sync so the frontend refreshes instantly without waiting for its own poll timer
- Todo sync settings section — connect/disconnect, list picker, manual "Sync Now" button, and sync interval control
- Soft-delete: todos cleared or deleted locally are hidden immediately and removed from the remote provider on the next sync cycle, preventing re-pull

### Miscellaneous Chores

- Conflict resolution uses last-write-wins (`LocalUpdatedAt` vs `RemoteLastModifiedAt`); falls back to field-level comparison when the provider timestamp does not update on status changes

---

## [0.13.0] (2026-03-27)

### Features

- Workflow `callWorkflow` step — invoke another workflow inline as a sub-workflow; its steps run as part of the parent execution with no separate execution record
- Sub-workflow inputs are passed explicitly via an `inputs` map on the step; values support `{{placeholder}}` substitution from the parent workflow
- Circular reference detection for `callWorkflow` — if a workflow calls itself directly or transitively, execution fails immediately with a clear error message listing the full chain (e.g. `workflow-a → workflow-b → workflow-a`)
- Sub-workflow log lines appear inline in the parent execution log, prefixed with `[SubWorkflowName]` so output from each level is visually distinguishable; nested calls stack the prefix
- Refresh button on the Workflows dashboard widget (panel header, consistent with the Repositories widget)
- Refresh button on the Workflows full page (toolbar, disabled while a fetch is in flight)
- Workflow engine documentation fully rewritten — all pages updated with field tables, detailed behaviour notes, sub-workflow documentation, and new troubleshooting entries for `callWorkflow`

---

## [0.12.0] (2026-03-24)

### Features

- WebView2 bridge typed via `interface Window` declaration — `window.chrome?.webview?.postMessage` is now fully type-safe, `as any` cast removed

### Bug Fixes

- Unused TypeScript imports (`WidgetId`, `UseMutationResult`) caused strict-mode build errors in CI — removed
- Dead `pullRequestsApi.getOpen()` export removed from `pullRequests.ts` — only `fetchPullRequests` was ever used
- Unused `ScriptDto`, `ExecutionDto`, and `ExecutionDetailDto` types deleted — were never referenced

### Code Refactoring

- Frontend shared components extracted: `ErrorBar`, `FilterToolbar` (with consolidated CSS replacing five per-page copies), `Modal`, `OpenerIcon`, and `TagEditor` moved to `src/components/`
- `DashboardSettingsModal` decomposed from 1 155 lines to 306 — each settings tab is now a dedicated component under `src/components/settings/`
- `TodosWidget` split into `TodoCreateForm`, `TodoItem`, and `TodosCompletedSection` sub-components (341 → 136 lines)
- Hooks extracted to eliminate duplication: `useWorkflowModals` (shared between DashboardPage and WorkflowsPage), `useRepositoryHub` (SignalR setup removed from DashboardPage), `useTodos`, `useRepositoryScan`
- `getScanIssueLabel` and `OpenerIcon` deduplicated — were defined identically in both `RepositoriesWidget` and `RepositoriesPage`; now live in `src/utils/repositoryUtils.ts` and `src/components/OpenerIcon.tsx`
- Backend DAOs moved to `Models/Dao/` with updated namespaces; services reorganized into domain subfolders (`Repositories/`, `PullRequests/`, `Todos/`, `Config/`, `Workflows/`, `Launcher/`, `Infrastructure/`)
- Provider ID magic strings `"azureDevOps"` and `"github"` replaced with `ProviderId` constants in `UserConfigService`
- `RunInstallerExecutor` / `RunInstallerStep` renamed to `RunExecutableExecutor` / `RunExecutableStep` to reflect their actual purpose
- Source tree reorganized: `ext/` → `src/browser-extension/`, `installer/` → `src/installer/`, `plugins/example-plugin/` → `src/plugins/example-plugin/`
- Solution folder wrappers removed — the four projects now appear directly under the solution root in Solution Explorer instead of each being nested inside a named folder

---

## [0.11.0] (2026-03-24)

### Features

- Plugin system — third-party plugins can extend DevelopmentHub with custom dashboard widgets and full pages without modifying the host application
- `manifest.json` per plugin declares the plugin id, version, minimum host version, backend assembly, frontend bundle, widget contributions, and route contributions
- Backend plugin interface (`IPlugin`) — plugins implement `ConfigureServices` and `Configure` to register ASP.NET Core services and endpoints; each plugin is loaded in an isolated `AssemblyLoadContext` to prevent assembly conflicts
- `PluginLoader` scans a configured plugins directory, reads each `manifest.json`, loads the backend assembly if present, and registers all `IPlugin` implementations
- `PluginRegistry` tracks loaded plugins and exposes their manifests to the host API
- Frontend plugin loading — the host injects a `__dhSdk` object on `window` exposing React, TanStack Query, Zustand, React Router, the host API base path, and a set of pre-built themed UI components; each plugin's JS bundle is loaded dynamically and calls `__dhSdk.plugin.registerWidget` / `registerRoute` to contribute its UI
- Themed UI component set available to plugins: `Button`, `Card`, `Input`, `Chip`, `Empty`, `PageRoot`, `Spinner`
- `@developmenthub/plugin-sdk` npm package — TypeScript type definitions for the entire `DhSdk` interface; install as a dev dependency in any plugin project for full type safety
- `DevelopmentHub.Plugins` NuGet package — ships `IPlugin`, `PluginManifest`, and related contracts; reference with `ExcludeAssets="runtime"` so the host assembly is not copied into the plugin output
- Both SDK packages published as CI artifacts on every PR and attached to GitHub Releases on main push
- Example plugin (`src/plugins/example-plugin/`) demonstrating a backend controller, a dashboard widget, and a full page built against both SDKs

---

## [0.10.0] (2026-03-23)

### Features

- Repository tags — custom labels can be added and removed per repository; stored in LiteDB via `PATCH /api/repositories/{id}/tags`
- Tag column in the Repositories page with an inline editor: click `+` to type a tag, Enter/Blur to save, `×` to remove
- Tag filter chips in the Repositories page toolbar — click one or more tags to filter the list (AND logic); a Clear button appears when any tag filter is active
- Tag column in the Repositories dashboard widget — read-only chips; column disappears before the Branch column on narrow panel widths via container queries
- Multi-repository workspace — checkbox column in the Repositories page lets you select two or more repositories; an `⧉ Workspace (N)` button appears in the toolbar and opens all selected repositories together in a single temporary VS Code `.code-workspace` file generated in `%TEMP%\DevelopmentHub\`
- Configurable repository openers — define any number of file-extension → program mappings in Settings → Repositories → Openers; VS Code and Visual Studio are pre-configured as defaults
- Icon type dropdown (VS Code / Visual Studio / Custom) in the opener settings row
- Icon path field for custom openers — point to any `.exe`, `.ico`, or `.dll` and the app extracts and displays the embedded icon automatically via `GET /api/icon-extractor`
- Native file-open dialog for browsing executable and icon files (`GET /api/file-picker`), separate from the existing folder picker
- Visual Studio instance reuse — when opening a `.sln` file, the app scans the Windows Running Object Table for a running VS instance that already has the solution loaded and activates it instead of opening a new window; falls back to a fresh launch when none is found

### Bug Fixes

- `RepositoryOpeners` was missing from `ConfigDto`, so opener settings were silently discarded on every save and never returned to the frontend — openers are now fully round-tripped through the API
- `IconPath` field was absent from `RepositoryOpenerDto`, preventing it from being persisted or served
- Maximized window covered the taskbar when using `WindowStyle="None"` — fixed by adding `WindowChrome` so WPF constrains the maximized bounds to the work area automatically
- Starting a second instance of the app showed a raw LiteDB file-lock exception — a named mutex now detects duplicate instances and shows a friendly message instead
- Local installer build used a hardcoded version instead of reading from `version.txt` — the build task now reads the version and appends `-localbuild`; `#ifndef` in the `.iss` file lets the command-line value take precedence
- Installer shortcut creation failed with `0x80070005 Access Denied` because `{commondesktop}` requires admin rights — changed to `{userdesktop}` to match the `PrivilegesRequired=lowest` setting
- git startup failure (e.g. `0xc0000142` DLL init error, often triggered without network) showed a raw Windows system dialog — the OS error popup is now suppressed via `SetErrorMode` and the failure is reported as a clean `GitFailedToStart` issue on the repository
- After installing a new version, the WebView2 could serve a cached `index.html` requiring a manual F5 reload — `Cache-Control: no-cache` is now set on all HTML responses so the frontend is always fetched fresh

### Miscellaneous Chores

- Open With icons in the Repositories page are now always rendered in fixed positions (hidden via `visibility: hidden` when not applicable) so all icons stay vertically aligned across every row
- Text selection disabled globally via `user-select: none` on `body`; re-enabled for `input`, `textarea`, and `contenteditable` elements
- Settings modal widened to 1020 px so opener rows fit without wrapping
- Opener icon buttons and the Explorer icon rendered in a single unified grid cell in the Repositories dashboard widget, giving all icons identical spacing and size (20 px) across every row
- `item-open-icon` buttons given a fixed 28 × 28 px footprint so icon buttons are consistently sized regardless of icon type
- Activating a maximised Visual Studio window no longer restores it to normal size — `ShowWindowAsync` is only called when the window is actually minimised

---

## [0.9.0] (2026-03-21)

### Features

- Frameless window with custom title bar — minimize, maximize/restore, and close buttons integrated into the dashboard header
- Double-click header to toggle maximize/restore; dragging from maximized automatically restores and begins moving the window
- Closing via the header button hides the app to the tray with a balloon notification showing the hotkey to bring it back
- Dedicated full pages for all five features: Repositories, Pull Requests, Todos, Workflows, and Quick Links — each accessible via the navigation bar
- Pull Requests page: search by title, repository, or author; filter tabs for All / Mine / Reviewer / Draft
- Todos page: search by title; filter tabs for All / Active / Completed
- Workflows page: search by name or description; filter tabs for All / Idle / Running / Succeeded / Failed
- Quick Links page: search by name or URL; filter tabs for All / Web / Explorer
- Clicking a dashboard panel title navigates to the corresponding full page
- Navigation links for all pages added to the app header; active link highlighted with accent underline
- VS Code dark theme (`#1e1e1e` / `#252526`) with VS Code blue accent and teal success colors
- Integrations settings page — GitHub and Azure DevOps credentials moved out of Pull Requests into their own dedicated page
- Tooltip info icons on all Integrations fields showing field descriptions, required PAT scopes, and which features use each credential

### Bug Fixes

- PR widget branch/author/repository columns now truncate with ellipsis instead of overflowing
- Repository widget branch column now truncates with ellipsis
- Settings sidebar active item border no longer gets clipped

### Miscellaneous Chores

- Dashboard frontend split into individual widget components (`PullRequestsWidget`, `RepositoriesWidget`, `QuickLinksWidget`, `TodosWidget`, `WorkflowsWidget`)
- All widgets are now responsive via CSS container queries — columns collapse based on the panel width, not the viewport
- Removed minimum size constraints from all dashboard panels so any size is allowed
- Header is now fixed and stays visible while scrolling; only the content area below it scrolls
- Settings button is now always visible in the header regardless of which page is open
- Dashboard Edit Layout button only appears on the dashboard
- Scrollbars are now themed to match the active color theme
- GitHub and Azure DevOps provider logos inverted to white in the VS Code theme
- Resize handles in edit mode replaced with visible accent-colored pills and corner square; default arrow icon suppressed
- Focus ring removed from nav links and window control buttons
- Minimize button SVG aligned to match the height of the maximize and close buttons

---

## [0.8.0] (2026-03-17)

### Features

- Workflow `copy` step to copy files and folders as part of workflow execution
- Workflow contracts wired on both backend and frontend (`CopyStep`, `CopyExecutor`, shared API step typing)
- Per-repository fetch isolation with bounded timeout handling and safe git process cancellation
- Repository validation before fetch (`path exists`, `.git` present)
- Scan issue classification for ownership/safe-directory errors, invalid repositories, missing paths, fetch timeouts, and remote access failures (including Azure DevOps TF401019)
- Scan issue fields in repository API responses with persisted state
- Repository scan warnings surfaced in the dashboard with per-repository labels and detailed tooltips
- Request cancellation through `POST /api/repositories/scan` so client cancellation is handled cleanly
- File-based workflow engine that loads workflow definitions from a configured folder
- Workflow input modal, live execution log modal, and real-time execution status updates
- Workflow step support for direct downloads, ZIP extraction, installer execution, JSON patching, and Windows service restarts
- Authenticated download steps for GitHub release assets and Azure DevOps pipeline artifacts
- Optional per-step elevation for Windows service restarts (UAC prompt only for the specific action)
- Structured documentation under `docs/workflow-engine/`

### Bug Fixes

- Duplicate `JsonDerivedType` registration in workflow step deserialization removed
- One failing repository no longer aborts the full scan with HTTP 500
- Workflow loading hardened with case-insensitive JSON parsing, stable fallback IDs, invalid-workflow filtering, and improved elevated-step error reporting

---

## [0.7.0] (2026-03-16)

### Features

- Todo widget with create, edit, complete, restore, delete, and clear-completed actions
- Inline links in todos — write text and a URL in a single field, the widget extracts and opens the link directly

### Bug Fixes

- Global hotkey toggle reliably brings the window to the foreground, hides when already focused, and restores maximized windows correctly

### Miscellaneous Chores

- Dashboard panel editing allows dragging from the full widget area; resize affordances simplified; minimum width of the quick links panel reduced
- Focus and panel control styling updated to follow the active theme across grips, close buttons, quick links, and resize borders
- Todo widget collapsed Done section and cleaner action layout with Font Awesome Free icons
- Explorer launching reuses an already open Explorer window for the same folder when possible

---

## [0.6.0] (2026-03-16)

### Features

- Microsoft Edge tab-reuse extension prototype and WebSocket bridge so PR links reuse existing browser tabs
- Edge extension packaged in CI with manifest version synchronized to the application release version

### Bug Fixes

- Dark theme provider icon contrast restored in the pull request widget

### Miscellaneous Chores

- Extension resilience improved with reconnect handling, heartbeats, wake-up alarms, and stale-client filtering
- Pull request and repository widgets share the same surface styling as quick links
- VS Code workspace tasks simplified and aligned with the desktop app-based development flow

---

## [0.5.0] (2026-03-15)

### Features

- Pull requests can now be loaded from Azure DevOps and GitHub simultaneously in one merged list
- Provider icons in the pull request list so GitHub and Azure DevOps entries are visually distinguishable
- Microsoft Edge extension prototype that reuses existing tabs for matching URLs
- Lightweight backend-to-extension bridge so DevelopmentHub can hand PR URLs to the Edge extension locally
- GitHub Actions workflow packages the Edge extension as a versioned ZIP artifact attached to releases

### Code Refactoring

- Pull request integrations refactored to adapter-based providers instead of Azure DevOps-only logic
- Pull request settings reworked to a provider-based configuration model
- GitHub pull request loading changed from single-repository polling to user-based search with optional extra qualifiers

---

## [0.4.0] (2026-03-12)

### Features

- Configurable PR refresh interval stored in LiteDB (replaces hardcoded 120 s)

### Bug Fixes

- Repository scan now removes deleted repos from the list; background scan interval re-read on each cycle
- VS Code now opens `.code-workspace` instead of the folder; Visual Studio uses shell association instead of `devenv.exe`

### Code Refactoring

- Settings modal refactored to sidebar-style navigation with per-panel config pages
- Frontend repository list updates instantly via SignalR push when a background scan completes

---

## [0.3.0] (2026-03-10)

### Bug Fixes

- Repository refresh button

### Code Refactoring

- Migrated from MongoDB to LiteDB; UI config moved to browser cache

---

## [0.2.0] (2026-03-09)

### Features

- Theming support with five built-in themes: Violet, Dark, Ocean, Orange, Nature

---

## [0.1.0] (initial release)

### Features

- Repository list with VS Code, Visual Studio, and Explorer buttons
- Current branch, ahead/behind display, favorites, and usage-based sorting
- Pull Requests widget (Azure DevOps)
- Drag-and-drop resizable dashboard panels
- Hide to tray, global hotkey, configurable keybinding
- Inno Setup installer and GitHub Actions CI/CD
