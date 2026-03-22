import type { Repository, OpenRepositoryRequest } from '../types';

const BASE = '/api';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const fetchRepositories = (): Promise<Repository[]> =>
  fetch(`${BASE}/repositories`).then(r => handleResponse(r));

export const repositoriesApi = {
  getAll: (): Promise<Repository[]> =>
    fetch(`${BASE}/repositories`).then(r => handleResponse(r)),

  scan: (): Promise<Repository[]> =>
    fetch(`${BASE}/repositories/scan`, { method: 'POST' }).then(r => handleResponse(r)),

  toggleFavorite: (id: string): Promise<Repository> =>
    fetch(`${BASE}/repositories/${id}/favorite`, { method: 'PATCH' }).then(r => handleResponse(r)),

  open: (id: string, req: OpenRepositoryRequest): Promise<Repository> =>
    fetch(`${BASE}/repositories/${id}/open`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }).then(r => handleResponse(r)),

  sync: (id: string): Promise<{ success: boolean; output: string }> =>
    fetch(`${BASE}/repositories/${id}/sync`, { method: 'POST' }).then(r => handleResponse(r)),

  openWorkspace: (repositoryIds: string[]): Promise<void> =>
    fetch(`${BASE}/repositories/open-workspace`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ repositoryIds }),
    }).then(async r => { if (!r.ok) { const b = await r.text().catch(() => r.statusText); throw new Error(b || `HTTP ${r.status}`); } }),
};
