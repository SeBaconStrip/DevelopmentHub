# Changelog

All notable changes are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
