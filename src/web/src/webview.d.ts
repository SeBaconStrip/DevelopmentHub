// Type declarations for the WebView2 host bridge injected by the WPF application.
interface Window {
  chrome?: {
    webview?: {
      postMessage: (message: string) => void;
    };
  };
  /** Per-process API token injected by the WPF host via AddScriptToExecuteOnDocumentCreatedAsync. */
  __devHubToken?: string;
}
