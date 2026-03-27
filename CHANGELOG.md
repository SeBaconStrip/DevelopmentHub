# Changelog

All notable changes are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## 0.13 – 2026-03-27

### ✨ Added

- Workflow `callWorkflow` step — invoke another workflow inline as a sub-workflow; its steps run as part of the parent execution with no separate execution record
- Sub-workflow inputs are passed explicitly via an `inputs` map on the step; values support `{{placeholder}}` substitution from the parent workflow
- Circular reference detection for `callWorkflow` — if a workflow calls itself directly or transitively, execution fails immediately with a clear error message listing the full chain (e.g. `workflow-a → workflow-b → workflow-a`)
- Sub-workflow log lines appear inline in the parent execution log, prefixed with `[SubWorkflowName]` so output from each level is visually distinguishable; nested calls stack the prefix
- Refresh button on the Workflows dashboard widget (panel header, consistent with the Repositories widget)
- Refresh button on the Workflows full page (toolbar, disabled while a fetch is in flight)
- Workflow engine documentation fully rewritten — all pages updated with field tables, detailed behaviour notes, sub-workflow documentation, and new troubleshooting entries for `callWorkflow`

---

## 0.12 – 2026-03-24

### ✨ Added

- WebView2 bridge typed via `interface Window` declaration — `window.chrome?.webview?.postMessage` is now fully type-safe, `as any` cast removed

### 🔧 Changed

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

### 🐛 Fixed

- Unused TypeScript imports (`WidgetId`, `UseMutationResult`) caused strict-mode build errors in CI — removed
- Dead `pullRequestsApi.getOpen()` export removed from `pullRequests.ts` — only `fetchPullRequests` was ever used
- Unused `ScriptDto`, `ExecutionDto`, and `ExecutionDetailDto` types deleted — were never referenced

---

## 0.11 – 2026-03-24

### ✨ Added

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

## 0.11 – 2026-03-24

### ✨ Added

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

## 0.10 – 2026-03-23

### ✨ Added

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

### 🔧 Changed

- Open With icons in the Repositories page are now always rendered in fixed positions (hidden via `visibility: hidden` when not applicable) so all icons stay vertically aligned across every row
- Text selection disabled globally via `user-select: none` on `body`; re-enabled for `input`, `textarea`, and `contenteditable` elements
- Settings modal widened to 1020 px so opener rows fit without wrapping
- Opener icon buttons and the Explorer icon rendered in a single unified grid cell in the Repositories dashboard widget, giving all icons identical spacing and size (20 px) across every row
- `item-open-icon` buttons given a fixed 28 × 28 px footprint so icon buttons are consistently sized regardless of icon type
- Activating a maximised Visual Studio window no longer restores it to normal size — `ShowWindowAsync` is only called when the window is actually minimised

### 🐛 Fixed

- `RepositoryOpeners` was missing from `ConfigDto`, so opener settings were silently discarded on every save and never returned to the frontend — openers are now fully round-tripped through the API
- `IconPath` field was absent from `RepositoryOpenerDto`, preventing it from being persisted or served
- Maximized window covered the taskbar when using `WindowStyle="None"` — fixed by adding `WindowChrome` so WPF constrains the maximized bounds to the work area automatically
- Starting a second instance of the app showed a raw LiteDB file-lock exception — a named mutex now detects duplicate instances and shows a friendly message instead
- Local installer build used a hardcoded version instead of reading from `version.txt` — the build task now reads the version and appends `-localbuild`; `#ifndef` in the `.iss` file lets the command-line value take precedence
- Installer shortcut creation failed with `0x80070005 Access Denied` because `{commondesktop}` requires admin rights — changed to `{userdesktop}` to match the `PrivilegesRequired=lowest` setting
- git startup failure (e.g. `0xc0000142` DLL init error, often triggered without network) showed a raw Windows system dialog — the OS error popup is now suppressed via `SetErrorMode` and the failure is reported as a clean `GitFailedToStart` issue on the repository
- After installing a new version, the WebView2 could serve a cached `index.html` requiring a manual F5 reload — `Cache-Control: no-cache` is now set on all HTML responses so the frontend is always fetched fresh

---

## 0.9 – 2026-03-21

### ✨ Added

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

### 🔧 Changed

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
- Patch version in CI now counted via `git tag -l` with full tag fetch (`fetch-depth: 0`) instead of the GitHub releases API, fixing always-zero patch numbers

### 🐛 Fixed

- PR widget branch/author/repository columns now truncate with ellipsis instead of overflowing
- Repository widget branch column now truncates with ellipsis
- Settings sidebar active item border no longer gets clipped

---

## 0.8 – 2026-03-17

### ✨ Added

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

### 🐛 Fixed

- Duplicate `JsonDerivedType` registration in workflow step deserialization removed
- One failing repository no longer aborts the full scan with HTTP 500
- Workflow loading hardened with case-insensitive JSON parsing, stable fallback IDs, invalid-workflow filtering, and improved elevated-step error reporting

---

## 0.7 – 2026-03-16

### ✨ Added

- Todo widget with create, edit, complete, restore, delete, and clear-completed actions
- Inline links in todos — write text and a URL in a single field, the widget extracts and opens the link directly

### 🔧 Changed

- Dashboard panel editing allows dragging from the full widget area; resize affordances simplified; minimum width of the quick links panel reduced
- Focus and panel control styling updated to follow the active theme across grips, close buttons, quick links, and resize borders
- Todo widget collapsed Done section and cleaner action layout with Font Awesome Free icons
- Explorer launching reuses an already open Explorer window for the same folder when possible

### 🐛 Fixed

- Global hotkey toggle reliably brings the window to the foreground, hides when already focused, and restores maximized windows correctly

---

## 0.6 – 2026-03-16

### ✨ Added

- Microsoft Edge tab-reuse extension prototype and WebSocket bridge so PR links reuse existing browser tabs
- Edge extension packaged in CI with manifest version synchronized to the application release version

### 🔧 Changed

- Extension resilience improved with reconnect handling, heartbeats, wake-up alarms, and stale-client filtering
- Pull request and repository widgets share the same surface styling as quick links
- VS Code workspace tasks simplified and aligned with the desktop app-based development flow

### 🐛 Fixed

- Dark theme provider icon contrast restored in the pull request widget

---

## 0.5 – 2026-03-15

### ✨ Added

- Pull requests can now be loaded from Azure DevOps and GitHub simultaneously in one merged list
- Provider icons in the pull request list so GitHub and Azure DevOps entries are visually distinguishable
- Microsoft Edge extension prototype that reuses existing tabs for matching URLs
- Lightweight backend-to-extension bridge so DevelopmentHub can hand PR URLs to the Edge extension locally
- GitHub Actions workflow packages the Edge extension as a versioned ZIP artifact attached to releases

### 🔧 Changed

- Pull request integrations refactored to adapter-based providers instead of Azure DevOps-only logic
- Pull request settings reworked to a provider-based configuration model
- GitHub pull request loading changed from single-repository polling to user-based search with optional extra qualifiers

---

## 0.4 – 2026-03-12

### ✨ Added

- Configurable PR refresh interval stored in LiteDB (replaces hardcoded 120 s)

### 🔧 Changed

- Settings modal refactored to sidebar-style navigation with per-panel config pages
- Frontend repository list updates instantly via SignalR push when a background scan completes

### 🐛 Fixed

- Repository scan now removes deleted repos from the list; background scan interval re-read on each cycle
- VS Code now opens `.code-workspace` instead of the folder; Visual Studio uses shell association instead of `devenv.exe`

---

## 0.3 – 2026-03-10

### 🔧 Changed

- Migrated from MongoDB to LiteDB; UI config moved to browser cache

### 🐛 Fixed

- Repository refresh button

---

## 0.2 – 2026-03-09

### ✨ Added

- Theming support with five built-in themes: Violet, Dark, Ocean, Orange, Nature

---

## 0.1 – initial release

### ✨ Added

- Repository list with VS Code, Visual Studio, and Explorer buttons
- Current branch, ahead/behind display, favorites, and usage-based sorting
- Pull Requests widget (Azure DevOps)
- Drag-and-drop resizable dashboard panels
- Hide to tray, global hotkey, configurable keybinding
- Inno Setup installer and GitHub Actions CI/CD
