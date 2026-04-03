import type { AppConfig } from '../types';
import { apiFetch } from './client';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const configApi = {
  get: (): Promise<AppConfig> =>
    apiFetch('/api/config').then(r => handleResponse(r)),

  save: (config: AppConfig): Promise<{ message: string }> =>
    apiFetch('/api/config', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config),
    }).then(r => handleResponse(r)),

  pickFolder: (): Promise<string | null> =>
    apiFetch('/api/folder-picker')
      .then(r => handleResponse<{ cancelled: boolean; path: string | null }>(r))
      .then(res => res.cancelled ? null : res.path),

  pickFile: (filter?: string): Promise<string | null> => {
    const params = filter ? `?filter=${encodeURIComponent(filter)}` : '';
    return apiFetch(`/api/file-picker${params}`)
      .then(r => handleResponse<{ cancelled: boolean; path: string | null }>(r))
      .then(res => res.cancelled ? null : res.path);
  },
};
