# DevelopmentHub Edge Extension

Simple proof of concept for Microsoft Edge:

- enter a URL
- if a tab with the same normalized URL already exists, focus it
- otherwise open a new tab

## Load in Edge

1. Open `edge://extensions`
2. Enable `Developer mode`
3. Click `Load unpacked`
4. Select this `edge-extension` folder

## Current scope

- manual popup-based test flow
- simple local backend polling bridge for DevelopmentHub
- compares tabs by normalized URL
- removes the URL fragment (`#...`)
- trims a trailing slash

## Local bridge

The background service worker keeps a WebSocket connection to one of these
backend endpoints:

- `ws://localhost:6131/ws/browser-tab-bridge`
- `ws://localhost:5131/ws/browser-tab-bridge`

If DevelopmentHub sends a URL, the extension tries to focus an existing tab
with the same normalized URL or opens a new one, then acknowledges the command
over the same socket. If no extension is connected, DevelopmentHub falls back
to opening the URL normally in the system browser.

## Not implemented yet

- native messaging host
- robust extension-to-app handshake
- production-grade install/update flow
