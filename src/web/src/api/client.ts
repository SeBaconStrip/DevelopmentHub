/**
 * Wrapper around fetch that attaches the per-process API token injected by the WebView host.
 * All API calls must go through this function so that the backend token middleware accepts them.
 */
export function apiFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers);
  headers.set('X-Dev-Hub-Token', window.__devHubToken ?? '');
  return fetch(input, { ...init, headers });
}
