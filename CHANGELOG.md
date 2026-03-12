# Changelog

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
