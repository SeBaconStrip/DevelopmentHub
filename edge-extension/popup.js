const urlInput = document.getElementById("url");
const status = document.getElementById("status");
const submitButton = document.getElementById("submit");
const currentTabButton = document.getElementById("current-tab");
const hubStatus = document.getElementById("hub-status");
const hubStatusText = document.getElementById("hub-status-text");

function setStatus(message, type = "") {
  status.textContent = message;
  status.className = `status${type ? ` status--${type}` : ""}`;
}

function formatRelativeAge(isoTimestamp) {
  if (!isoTimestamp) {
    return "";
  }

  const then = new Date(isoTimestamp).getTime();
  if (Number.isNaN(then)) {
    return "";
  }

  const seconds = Math.max(0, Math.round((Date.now() - then) / 1000));
  if (seconds < 60) {
    return `${seconds}s ago`;
  }

  const minutes = Math.round(seconds / 60);
  return `${minutes}m ago`;
}

function setHubStatus(statusInfo = {}) {
  const { state, activeUrl, lastUrl, lastHeartbeatAt, lastDisconnectAt } = statusInfo;
  const details = activeUrl || lastUrl || "";
  const normalizedState = ["connected", "connecting", "disconnected"].includes(state)
    ? state
    : "unknown";

  hubStatus.className = `hub-status hub-status--${normalizedState}`;

  if (normalizedState === "connected") {
    const heartbeatAge = formatRelativeAge(lastHeartbeatAt);
    hubStatusText.textContent = details
      ? `Connected to Development Hub at ${details}${heartbeatAge ? ` | heartbeat ${heartbeatAge}` : ""}`
      : `Connected to Development Hub${heartbeatAge ? ` | heartbeat ${heartbeatAge}` : ""}`;
    return;
  }

  if (normalizedState === "connecting") {
    hubStatusText.textContent = details
      ? `Connecting to Development Hub at ${details}...`
      : "Connecting to Development Hub...";
    return;
  }

  if (normalizedState === "disconnected") {
    const disconnectAge = formatRelativeAge(lastDisconnectAt);
    hubStatusText.textContent = details
      ? `Development Hub unavailable. Last tried ${details}${disconnectAge ? ` | likely sleeping/disconnected ${disconnectAge}` : ""}`
      : `Development Hub unavailable${disconnectAge ? ` | likely sleeping/disconnected ${disconnectAge}` : ""}`;
    return;
  }

  hubStatusText.textContent = "Checking Development Hub connection...";
}

async function refreshHubStatus() {
  try {
    const response = await chrome.runtime.sendMessage({
      type: "get-bridge-status",
    });

    setHubStatus(response ?? {});
  } catch {
    setHubStatus();
  }
}

async function useCurrentTabUrl() {
  const [activeTab] = await chrome.tabs.query({
    active: true,
    currentWindow: true,
  });

  if (activeTab?.url) {
    urlInput.value = activeTab.url;
    setStatus("Current tab URL inserted.");
    urlInput.focus();
    urlInput.select();
  } else {
    setStatus("No active tab URL available.", "error");
  }
}

async function focusOrOpen() {
  const url = urlInput.value.trim();
  if (!url) {
    setStatus("Please enter a URL.", "error");
    return;
  }

  setStatus("Working...");

  try {
    const response = await chrome.runtime.sendMessage({
      type: "focus-or-open-url",
      url,
    });

    if (!response?.ok) {
      throw new Error(response?.error || "Request failed.");
    }

    setStatus(
      response.reused ? "Existing tab focused." : "New tab opened.",
      "success",
    );
  } catch (error) {
    setStatus(
      error instanceof Error ? error.message : "Unexpected error.",
      "error",
    );
  }
}

submitButton.addEventListener("click", focusOrOpen);
currentTabButton.addEventListener("click", useCurrentTabUrl);
urlInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    focusOrOpen();
  }
});

refreshHubStatus();
useCurrentTabUrl().catch(() => {
  setStatus("Ready.");
});
