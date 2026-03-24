// Type declarations for the WebView2 host bridge injected by the WPF application.
interface Window {
  chrome?: {
    webview?: {
      postMessage: (message: string) => void;
    };
  };
}
