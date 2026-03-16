function normalizeUrl(rawUrl) {
  try {
    const url = new URL(rawUrl);
    url.hash = "";
    url.protocol = url.protocol.toLowerCase();
    url.hostname = url.hostname.toLowerCase();

    const params = [...url.searchParams.entries()]
      .sort(([leftKey, leftValue], [rightKey, rightValue]) => {
        if (leftKey === rightKey) {
          return leftValue.localeCompare(rightValue);
        }
        return leftKey.localeCompare(rightKey);
      });

    url.search = "";
    for (const [key, value] of params) {
      url.searchParams.append(key, value);
    }

    if (
      (url.protocol === "https:" && url.port === "443") ||
      (url.protocol === "http:" && url.port === "80")
    ) {
      url.port = "";
    }

    let normalized = url.toString();
    if (normalized.endsWith("/")) {
      normalized = normalized.slice(0, -1);
    }
    return normalized;
  } catch {
    return rawUrl.trim();
  }
}

function normalizeUrlWithoutSearch(rawUrl) {
  try {
    const url = new URL(rawUrl);
    url.hash = "";
    url.search = "";
    url.protocol = url.protocol.toLowerCase();
    url.hostname = url.hostname.toLowerCase();

    if (
      (url.protocol === "https:" && url.port === "443") ||
      (url.protocol === "http:" && url.port === "80")
    ) {
      url.port = "";
    }

    let normalized = url.toString();
    if (normalized.endsWith("/")) {
      normalized = normalized.slice(0, -1);
    }
    return normalized;
  } catch {
    return rawUrl.trim();
  }
}

const WS_BASES = [
  "ws://localhost:6131/ws/browser-tab-bridge",
  "ws://localhost:5131/ws/browser-tab-bridge",
];

let bridgeSocket = null;
let reconnectTimer = null;
let consecutiveFailures = 0;
let bridgeState = "disconnected";
let activeSocketUrl = null;
let lastSocketUrl = null;
let heartbeatTimer = null;
let lastHeartbeatAt = null;
let lastDisconnectAt = null;
const BASE_RECONNECT_INTERVAL_MS = 1500;
const HEARTBEAT_INTERVAL_MS = 15000;
const MAX_BACKOFF_MS = 30000;
const BRIDGE_MAINTENANCE_ALARM = "bridge-maintenance";
const BRIDGE_MAINTENANCE_PERIOD_MINUTES = 0.5;

function getReconnectInterval() {
  if (consecutiveFailures > 0) {
    return Math.min(
      BASE_RECONNECT_INTERVAL_MS * Math.pow(2, consecutiveFailures - 1),
      MAX_BACKOFF_MS,
    );
  }

  return BASE_RECONNECT_INTERVAL_MS;
}

async function findMatchingTab(targetUrl) {
  const normalizedTarget = normalizeUrl(targetUrl);
  const normalizedTargetWithoutSearch = normalizeUrlWithoutSearch(targetUrl);
  const tabs = await chrome.tabs.query({});
  let fallbackMatch = null;

  for (const tab of tabs) {
    if (!tab.id || !tab.url) {
      continue;
    }

    const normalizedTabUrl = normalizeUrl(tab.url);
    if (normalizedTabUrl === normalizedTarget) {
      return tab;
    }

    if (
      fallbackMatch === null &&
      normalizeUrlWithoutSearch(tab.url) === normalizedTargetWithoutSearch
    ) {
      fallbackMatch = tab;
    }
  }

  return fallbackMatch;
}

async function focusOrOpenUrl(rawUrl) {
  const url = rawUrl.trim();
  if (!url) {
    throw new Error("URL is required.");
  }

  const target = new URL(url).toString();
  const existingTab = await findMatchingTab(target);

  if (existingTab?.id) {
    await chrome.tabs.update(existingTab.id, { active: true });
    if (existingTab.windowId !== chrome.windows.WINDOW_ID_NONE) {
      await chrome.windows.update(existingTab.windowId, { focused: true });
    }
    return { reused: true, tabId: existingTab.id };
  }

  const createdTab = await chrome.tabs.create({ url: target, active: true });
  if (createdTab.windowId !== chrome.windows.WINDOW_ID_NONE) {
    await chrome.windows.update(createdTab.windowId, { focused: true });
  }
  return { reused: false, tabId: createdTab.id ?? null };
}

function sendBridgeMessage(message) {
  if (bridgeSocket?.readyState !== WebSocket.OPEN) {
    return false;
  }

  bridgeSocket.send(JSON.stringify(message));
  return true;
}

function stopHeartbeat() {
  if (heartbeatTimer !== null) {
    clearInterval(heartbeatTimer);
    heartbeatTimer = null;
  }
}

function sendHeartbeat() {
  const sentAt = new Date().toISOString();
  const sent = sendBridgeMessage({
    type: "heartbeat",
    sentAt,
  });

  if (sent) {
    lastHeartbeatAt = sentAt;
    return true;
  }

  stopHeartbeat();
  return false;
}

function startHeartbeat() {
  stopHeartbeat();
  sendHeartbeat();
  heartbeatTimer = setInterval(() => {
    sendHeartbeat();
  }, HEARTBEAT_INTERVAL_MS);
}

function setBridgeState(state, socketUrl = null) {
  bridgeState = state;
  if (socketUrl) {
    lastSocketUrl = socketUrl;
  }

  if (state === "connected") {
    activeSocketUrl = socketUrl;
    lastDisconnectAt = null;
    return;
  }

  activeSocketUrl = null;
  if (state === "disconnected") {
    lastDisconnectAt = new Date().toISOString();
  }
}

async function handleBridgeCommand(command) {
  try {
    if (command?.type === "focus-or-open-url") {
      await focusOrOpenUrl(command.url);
      sendBridgeMessage({
        type: "complete-command",
        commandId: command.commandId,
        handled: true,
      });
      return;
    }
  } catch {
    // fall through to handled=false
  }

  if (command?.commandId) {
    sendBridgeMessage({
      type: "complete-command",
      commandId: command.commandId,
      handled: false,
    });
  }
}

function scheduleReconnect() {
  if (reconnectTimer !== null) {
    return;
  }

  setBridgeState("connecting", lastSocketUrl);
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    connectBridge().catch(() => {
      scheduleReconnect();
    });
  }, getReconnectInterval());
}

function ensureMaintenanceAlarm() {
  chrome.alarms.create(BRIDGE_MAINTENANCE_ALARM, {
    periodInMinutes: BRIDGE_MAINTENANCE_PERIOD_MINUTES,
  });
}

function openBridgeSocket(socketUrl) {
  return new Promise((resolve, reject) => {
    const socket = new WebSocket(socketUrl);
    let settled = false;
    setBridgeState("connecting", socketUrl);

    socket.addEventListener("open", () => {
      bridgeSocket = socket;
      consecutiveFailures = 0;
      setBridgeState("connected", socketUrl);
      startHeartbeat();
      settled = true;
      resolve();
    });

    socket.addEventListener("message", (event) => {
      try {
        const command = JSON.parse(event.data);
        handleBridgeCommand(command);
      } catch {
        // ignore malformed payloads
      }
    });

    socket.addEventListener("close", () => {
      if (bridgeSocket === socket) {
        bridgeSocket = null;
      }
      stopHeartbeat();
      setBridgeState("disconnected", socketUrl);

      if (!settled) {
        settled = true;
        reject(new Error("Bridge connection closed before opening."));
        return;
      }

      consecutiveFailures++;
      scheduleReconnect();
    });

    socket.addEventListener("error", () => {
      stopHeartbeat();
      setBridgeState("disconnected", socketUrl);

      if (settled) {
        return;
      }

      settled = true;
      try {
        socket.close();
      } catch {
        // ignore
      }
      reject(new Error(`Failed to connect to ${socketUrl}`));
    });
  });
}

async function connectBridge() {
  if (
    bridgeSocket &&
    (bridgeSocket.readyState === WebSocket.OPEN ||
      bridgeSocket.readyState === WebSocket.CONNECTING)
  ) {
    return;
  }

  setBridgeState("connecting", lastSocketUrl);

  for (const socketUrl of WS_BASES) {
    try {
      await openBridgeSocket(socketUrl);
      return;
    } catch {
      // try next websocket URL
    }
  }

  consecutiveFailures++;
  setBridgeState("disconnected", lastSocketUrl);
  scheduleReconnect();
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "get-bridge-status") {
    sendResponse({
      state: bridgeState,
      activeUrl: activeSocketUrl,
      lastUrl: lastSocketUrl,
      lastHeartbeatAt,
      lastDisconnectAt,
      heartbeatIntervalMs: HEARTBEAT_INTERVAL_MS,
    });
    return false;
  }

  if (message?.type !== "focus-or-open-url") {
    return false;
  }

  focusOrOpenUrl(message.url)
    .then((result) => sendResponse({ ok: true, ...result }))
    .catch((error) =>
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "Unknown error",
      }),
    );

  return true;
});

chrome.runtime.onStartup?.addListener(() => {
  ensureMaintenanceAlarm();
  connectBridge().catch(() => {
    scheduleReconnect();
  });
});

chrome.runtime.onInstalled.addListener(() => {
  ensureMaintenanceAlarm();
  connectBridge().catch(() => {
    scheduleReconnect();
  });
});

chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name !== BRIDGE_MAINTENANCE_ALARM) {
    return;
  }

  if (bridgeSocket?.readyState === WebSocket.OPEN) {
    sendHeartbeat();
    return;
  }

  connectBridge().catch(() => {
    scheduleReconnect();
  });
});

chrome.runtime.onSuspend?.addListener(() => {
  stopHeartbeat();

  if (reconnectTimer !== null) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }

  try {
    bridgeSocket?.close();
  } catch {
    // ignore
  }

  bridgeSocket = null;
  setBridgeState("disconnected", lastSocketUrl);
});

ensureMaintenanceAlarm();
connectBridge().catch(() => {
  scheduleReconnect();
});
