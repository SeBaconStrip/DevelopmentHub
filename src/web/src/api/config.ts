import type { AppConfig } from '../types';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const configApi = {
  get: (): Promise<AppConfig> =>
    fetch('/api/config').then(r => handleResponse(r)),

  save: (config: AppConfig): Promise<{ message: string }> =>
    fetch('/api/config', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config),
    }).then(r => handleResponse(r)),

  pickFolder: (): Promise<string | null> =>
    fetch('/api/folder-picker')
      .then(r => handleResponse<{ cancelled: boolean; path: string | null }>(r))
      .then(res => res.cancelled ? null : res.path),

  pickFile: (filter?: string): Promise<string | null> => {
    const params = filter ? `?filter=${encodeURIComponent(filter)}` : '';
    return fetch(`/api/file-picker${params}`)
      .then(r => handleResponse<{ cancelled: boolean; path: string | null }>(r))
      .then(res => res.cancelled ? null : res.path);
  },
};
