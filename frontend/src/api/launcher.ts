const BASE = '/api/launcher';

export const launcherApi = {
  openUrl: (url: string): Promise<void> =>
    fetch(`${BASE}/open-url`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url }),
    }).then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); }),
};
