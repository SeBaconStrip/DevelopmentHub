import type { PullRequest } from '../types';
import { apiFetch } from './client';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const fetchPullRequests = (): Promise<PullRequest[]> =>
  apiFetch('/api/pullrequests').then(r => handleResponse(r));
