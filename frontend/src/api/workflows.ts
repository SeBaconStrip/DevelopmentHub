import type {
  RunWorkflowRequest,
  WorkflowDefinition,
  WorkflowExecution,
  WorkflowExecutionDetail,
} from "../types";

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
    fetch(BASE).then((r) => handleResponse(r)),

  listExecutions: (): Promise<WorkflowExecution[]> =>
    fetch(`${BASE}/executions`).then((r) => handleResponse(r)),

  getExecution: (executionId: string): Promise<WorkflowExecutionDetail> =>
    fetch(`${BASE}/executions/${executionId}`).then((r) => handleResponse(r)),

  run: (workflowId: string, request: RunWorkflowRequest): Promise<WorkflowExecution> =>
    fetch(`${BASE}/${workflowId}/run`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    }).then((r) => handleResponse(r)),
};
