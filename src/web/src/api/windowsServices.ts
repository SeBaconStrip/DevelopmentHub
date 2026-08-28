import type { WindowsServiceInfo, WindowsServiceSummary } from '../types';
import { apiFetch } from './client';

const BASE = '/api/windows-services';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const windowsServicesApi = {
  getStatuses: (): Promise<WindowsServiceInfo[]> =>
    apiFetch(BASE).then(r => handleResponse(r)),

  getAvailable: (): Promise<WindowsServiceSummary[]> =>
    apiFetch(`${BASE}/available`).then(r => handleResponse(r)),

  start: (name: string): Promise<{ message: string }> =>
    apiFetch(`${BASE}/${encodeURIComponent(name)}/start`, { method: 'POST' })
      .then(r => handleResponse(r)),

  stop: (name: string): Promise<{ message: string }> =>
    apiFetch(`${BASE}/${encodeURIComponent(name)}/stop`, { method: 'POST' })
      .then(r => handleResponse(r)),

  restart: (name: string): Promise<{ message: string }> =>
    apiFetch(`${BASE}/${encodeURIComponent(name)}/restart`, { method: 'POST' })
      .then(r => handleResponse(r)),

  grantPermission: (name: string): Promise<{ message: string }> =>
    apiFetch(`${BASE}/${encodeURIComponent(name)}/grant-permission`, { method: 'POST' })
      .then(r => handleResponse(r)),
};
