const urlInput = document.getElementById("url");
const status = document.getElementById("status");
const submitButton = document.getElementById("submit");
const currentTabButton = document.getElementById("current-tab");

function setStatus(message, type = "") {
  status.textContent = message;
  status.className = `status${type ? ` status--${type}` : ""}`;
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

useCurrentTabUrl().catch(() => {
  setStatus("Ready.");
});
