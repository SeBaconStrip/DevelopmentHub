import { apiFetch } from './client';

const BASE = '/api/launcher';

export const launcherApi = {
  openUrl: (url: string): Promise<void> =>
    apiFetch(`${BASE}/open-url`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url }),
    }).then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); }),

  openExplorer: (target: string): Promise<void> =>
    apiFetch(`${BASE}/open-explorer`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ target }),
    }).then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); }),
};
