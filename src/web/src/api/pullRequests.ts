import type { PullRequest } from '../types';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const fetchPullRequests = (): Promise<PullRequest[]> =>
  fetch('/api/pullrequests').then(r => handleResponse(r));

export const pullRequestsApi = {
  getOpen: (): Promise<PullRequest[]> =>
    fetch('/api/pullrequests').then(r => handleResponse(r)),
};
