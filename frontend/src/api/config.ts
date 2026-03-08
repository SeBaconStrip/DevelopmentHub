import type { AppConfig, DashboardConfig } from '../types';

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
};

export const dashboardConfigApi = {
  get: (): Promise<DashboardConfig> =>
    fetch('/api/config/dashboard').then(r => handleResponse(r)),

  save: (config: DashboardConfig): Promise<{ message: string }> =>
    fetch('/api/config/dashboard', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config),
    }).then(r => handleResponse(r)),
};
