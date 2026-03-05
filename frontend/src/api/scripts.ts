import type { Script, Execution, ExecutionDetail } from '../types';

const BASE = '/api/scripts';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const scriptsApi = {
  getAll: (): Promise<Script[]> =>
    fetch(BASE).then(r => handleResponse(r)),

  execute: (scriptId: string): Promise<Execution> =>
    fetch(`${BASE}/${scriptId}/execute`, { method: 'POST' }).then(r => handleResponse(r)),

  cancel: (executionId: string): Promise<void> =>
    fetch(`${BASE}/executions/${executionId}/cancel`, { method: 'POST' }).then(r => handleResponse(r)),

  getHistory: (limit = 50): Promise<Execution[]> =>
    fetch(`${BASE}/executions?limit=${limit}`).then(r => handleResponse(r)),

  getDetail: (executionId: string): Promise<ExecutionDetail> =>
    fetch(`${BASE}/executions/${executionId}`).then(r => handleResponse(r)),
};

export const fetchScripts = (): Promise<Script[]> => scriptsApi.getAll();
export const fetchExecutions = (limit?: number): Promise<Execution[]> => scriptsApi.getHistory(limit);
