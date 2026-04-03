import type { Repository, OpenRepositoryRequest } from '../types';
import { apiFetch } from './client';

const BASE = '/api';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const fetchRepositories = (): Promise<Repository[]> =>
  apiFetch(`${BASE}/repositories`).then(r => handleResponse(r));

export const repositoriesApi = {
  getAll: (): Promise<Repository[]> =>
    fetch(`${BASE}/repositories`).then(r => handleResponse(r)),

  scan: (): Promise<Repository[]> =>
    apiFetch(`${BASE}/repositories/scan`, { method: 'POST' }).then(r => handleResponse(r)),

  toggleFavorite: (id: string): Promise<Repository> =>
    apiFetch(`${BASE}/repositories/${id}/favorite`, { method: 'PATCH' }).then(r => handleResponse(r)),

  open: (id: string, req: OpenRepositoryRequest): Promise<Repository> =>
    apiFetch(`${BASE}/repositories/${id}/open`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }).then(r => handleResponse(r)),

  sync: (id: string): Promise<{ success: boolean; output: string }> =>
    apiFetch(`${BASE}/repositories/${id}/sync`, { method: 'POST' }).then(r => handleResponse(r)),

  updateTags: (id: string, tags: string[]): Promise<void> =>
    apiFetch(`${BASE}/repositories/${id}/tags`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tags }),
    }).then(r => handleResponse(r)),

  openWorkspace: (repositoryIds: string[]): Promise<void> =>
    apiFetch(`${BASE}/repositories/open-workspace`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ repositoryIds }),
    }).then(async r => { if (!r.ok) { const b = await r.text().catch(() => r.statusText); throw new Error(b || `HTTP ${r.status}`); } }),
};
