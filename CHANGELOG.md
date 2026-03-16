# Changelog

## 0.6 - 2026-03-16

- Added a Microsoft Edge tab-reuse extension prototype and a WebSocket bridge so PR links can reuse existing browser tabs
- Improved extension resilience with reconnect handling, heartbeats, wake-up alarms, and stale-client filtering on the backend
- Updated the pull request and repository widgets so their data grids share the same surface styling as quick links
- Restored dark theme provider icon contrast in the pull request widget
- Packaged the Edge extension in CI and synchronized its manifest version with the application release version
- Simplified VS Code workspace tasks and aligned `run all` with the desktop app-based development flow

## 0.5 - 2026-03-15

- Refactored pull request integrations to use adapter-based providers instead of Azure DevOps-only logic
- Pull requests can now be loaded from Azure DevOps and GitHub at the same time and appear in one merged list
- Added provider icons to the pull request list so GitHub and Azure DevOps entries are visually distinguishable
- Reworked pull request settings to a provider-based configuration model and removed the old single-provider selection flow
- Changed GitHub pull request loading from single-repository polling to user-based search with optional extra search qualifiers
- Added a simple Microsoft Edge extension prototype that reuses existing tabs for matching URLs instead of always opening a new tab
- Added a lightweight backend-to-extension bridge so DevelopmentHub can hand PR URLs to the Edge extension locally
- Extended GitHub Actions build/release workflow to package the Edge extension as a versioned ZIP artifact and attach it to releases

## 0.4 - 2026-03-12

- Refactored settings modal to sidebar-style navigation with per-panel config pages
- Fixed repository scan not removing deleted repos from the list; background scan interval now re-read on each cycle
- Fixed VS Code opening folder instead of .code-workspace; Visual Studio now uses shell association instead of devenv.exe
- Frontend repository list now updates instantly via SignalR push when a background scan completes
- Added configurable PR refresh interval (stored in LiteDB, replaces hardcoded 120 s)

## 0.3 - 2026-03-10

- Fixed Repo refresh button
- Changed from MongoDB to LiteDB, moved uiConfig to Browser cache

## 0.2 – 2026-03-09

- Added theming support with five built-in themes (Violet, Dark, Ocean, Orange, Nature)

## 0.1 – initial release

- Repository list with VS Code, Visual Studio and Explorer buttons
- Current branch, ahead/behind display, favorites, usage-based sorting
- Pull Requests widget (Azure DevOps)
- Drag-and-drop resizable dashboard panels
- Hide to tray, global hotkey, configurable keybinding
- Inno Setup installer, GitHub Actions CI/CD
