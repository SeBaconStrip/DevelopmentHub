import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { workflowsApi } from "../../api/workflows";
import { WorkflowsWidget } from "../dashboard/widgets/WorkflowsWidget";
import { WorkflowInputModal } from "../dashboard/widgets/WorkflowInputModal";
import { WorkflowExecutionModal } from "../dashboard/widgets/WorkflowExecutionModal";
import type { WorkflowDefinition, WorkflowExecution, RunWorkflowRequest } from "../../types";
import "./WorkflowsPage.css";

type Filter = "all" | "idle" | "running" | "succeeded" | "failed";

export default function WorkflowsPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");
  const [workflowRunError, setWorkflowRunError] = useState<string | null>(null);
  const [workflowInputModal, setWorkflowInputModal] = useState<WorkflowDefinition | null>(null);
  const [runningWorkflowId, setRunningWorkflowId] = useState<string | null>(null);
  const [workflowModal, setWorkflowModal] = useState<{
    workflow: WorkflowDefinition;
    executionId: string | null;
  } | null>(null);

  const { data: workflows = [], isLoading } = useQuery<WorkflowDefinition[]>({
    queryKey: ["workflows"],
    queryFn: workflowsApi.list,
  });

  const { data: workflowExecutions = [] } = useQuery<WorkflowExecution[]>({
    queryKey: ["workflow-executions"],
    queryFn: workflowsApi.listExecutions,
    refetchInterval: 5000,
  });

  const runWorkflow = useMutation({
    mutationFn: ({ workflowId, request }: { workflowId: string; request: RunWorkflowRequest }) => {
      setRunningWorkflowId(workflowId);
      return workflowsApi.run(workflowId, request);
    },
    onSuccess: (execution, variables) => {
      setWorkflowRunError(null);
      queryClient.invalidateQueries({ queryKey: ["workflow-executions"] });
      const workflow = workflows.find((w) => w.id === variables.workflowId);
      if (workflow) {
        setWorkflowModal({ workflow, executionId: execution.id });
      }
    },
    onError: (err) => setWorkflowRunError(err.message),
    onSettled: () => setRunningWorkflowId(null),
  });

  function runWorkflowWithInputs(workflow: WorkflowDefinition, inputs: Record<string, string>) {
    const confirmed =
      !workflow.requiresConfirmation ||
      window.confirm(`Run workflow "${workflow.name}"?`);
    if (!confirmed) return;
    setWorkflowRunError(null);
    runWorkflow.mutate({ workflowId: workflow.id, request: { inputs, confirmed } });
  }

  function getStatus(workflow: WorkflowDefinition): string {
    return workflowExecutions.find((e) => e.workflowId === workflow.id)?.status ?? "idle";
  }

  const statusCounts = (["idle", "running", "succeeded", "failed"] as const).reduce(
    (acc, s) => ({ ...acc, [s]: workflows.filter((w) => getStatus(w) === s).length }),
    {} as Record<string, number>,
  );

  const filtered = workflows
    .filter((w) => filter === "all" || getStatus(w) === filter)
    .filter(
      (w) =>
        !search ||
        w.name.toLowerCase().includes(search.toLowerCase()) ||
        w.description?.toLowerCase().includes(search.toLowerCase()),
    );

  return (
    <div className="workflows-page">
      <div className="workflows-page-card">
        <div className="workflows-page-toolbar">
          <div className="workflows-page-toolbar-left">
            <input
              className="workflows-page-search"
              type="search"
              placeholder="Search by name or description…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <div className="workflows-page-filters">
              {(["all", "idle", "running", "succeeded", "failed"] as Filter[]).map((f) => (
                <button
                  key={f}
                  className={`workflows-page-filter-btn${filter === f ? " workflows-page-filter-btn--active" : ""}`}
                  onClick={() => setFilter(f)}
                >
                  {f === "all"
                    ? `All (${workflows.length})`
                    : `${f.charAt(0).toUpperCase() + f.slice(1)}${statusCounts[f] > 0 ? ` (${statusCounts[f]})` : ""}`}
                </button>
              ))}
            </div>
          </div>
          {filtered.length !== workflows.length && (
            <span className="workflows-page-count">{filtered.length} shown</span>
          )}
        </div>
      </div>

      {isLoading ? (
        <p className="workflows-page-empty">Loading…</p>
      ) : (
        <div className="workflows-page-content-card">
          <WorkflowsWidget
            workflows={filtered}
            executions={workflowExecutions}
            runningWorkflowId={runningWorkflowId}
            workflowRunError={workflowRunError}
            onClearError={() => setWorkflowRunError(null)}
            onRun={(workflow) => {
              if (workflow.inputs.length > 0) {
                setWorkflowInputModal(workflow);
                return;
              }
              runWorkflowWithInputs(workflow, {});
            }}
            onOpenExecution={(workflowId) => {
              const workflow = workflows.find((w) => w.id === workflowId);
              const execution = workflowExecutions.find((e) => e.workflowId === workflowId);
              if (!workflow || !execution) return;
              setWorkflowModal({ workflow, executionId: execution.id });
            }}
          />
        </div>
      )}

      {workflowInputModal && (
        <WorkflowInputModal
          workflow={workflowInputModal}
          onClose={() => setWorkflowInputModal(null)}
          onSubmit={(inputs) => {
            const workflow = workflowInputModal;
            if (!workflow) return;
            setWorkflowInputModal(null);
            runWorkflowWithInputs(workflow, inputs);
          }}
        />
      )}
      {workflowModal && (
        <WorkflowExecutionModal
          workflow={workflowModal.workflow}
          executionId={workflowModal.executionId}
          onClose={() => setWorkflowModal(null)}
        />
      )}
    </div>
  );
}
