function normalizeUrl(rawUrl) {
  try {
    const url = new URL(rawUrl);
    url.hash = "";
    let normalized = url.toString();
    if (normalized.endsWith("/")) {
      normalized = normalized.slice(0, -1);
    }
    return normalized;
  } catch {
    return rawUrl.trim();
  }
}

const API_BASES = [
  "http://localhost:6131/api/browser-tab-bridge",
  "http://localhost:5131/api/browser-tab-bridge",
];

let pollTimer = null;

async function findMatchingTab(targetUrl) {
  const normalizedTarget = normalizeUrl(targetUrl);
  const tabs = await chrome.tabs.query({});

  for (const tab of tabs) {
    if (!tab.id || !tab.url) {
      continue;
    }

    if (normalizeUrl(tab.url) === normalizedTarget) {
      return tab;
    }
  }

  return null;
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

async function fetchNextCommand() {
  for (const baseUrl of API_BASES) {
    try {
      const response = await fetch(`${baseUrl}/next`, { method: "GET" });
      if (response.status === 204) {
        return null;
      }

      if (!response.ok) {
        continue;
      }

      const command = await response.json();
      return { baseUrl, command };
    } catch {
      // try next backend URL
    }
  }

  return null;
}

async function completeCommand(baseUrl, commandId, handled) {
  try {
    await fetch(`${baseUrl}/complete`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ commandId, handled }),
    });
  } catch {
    // best effort
  }
}

async function pollBackend() {
  const next = await fetchNextCommand();
  if (!next?.command) {
    return;
  }

  const { baseUrl, command } = next;
  try {
    if (command.type === "focus-or-open-url") {
      await focusOrOpenUrl(command.url);
      await completeCommand(baseUrl, command.commandId, true);
      return;
    }
  } catch {
    // fall through to handled=false
  }

  await completeCommand(baseUrl, command.commandId, false);
}

function startPolling() {
  if (pollTimer !== null) {
    return;
  }

  pollTimer = setInterval(() => {
    pollBackend().catch(() => {});
  }, 1000);
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
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
  startPolling();
});

chrome.runtime.onInstalled.addListener(() => {
  startPolling();
});

startPolling();
