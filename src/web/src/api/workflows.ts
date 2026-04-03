import type {
  RunWorkflowRequest,
  WorkflowDefinition,
  WorkflowExecution,
  WorkflowExecutionDetail,
} from "../types";
import { apiFetch } from './client';

const BASE = "/api/workflows";

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }

  return res.json() as Promise<T>;
}

export const workflowsApi = {
  list: (): Promise<WorkflowDefinition[]> =>
    apiFetch(BASE).then((r) => handleResponse(r)),

  listExecutions: (): Promise<WorkflowExecution[]> =>
    apiFetch(`${BASE}/executions`).then((r) => handleResponse(r)),

  getExecution: (executionId: string): Promise<WorkflowExecutionDetail> =>
    apiFetch(`${BASE}/executions/${executionId}`).then((r) => handleResponse(r)),

  run: (workflowId: string, request: RunWorkflowRequest): Promise<WorkflowExecution> =>
    apiFetch(`${BASE}/${workflowId}/run`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    }).then((r) => handleResponse(r)),
};
