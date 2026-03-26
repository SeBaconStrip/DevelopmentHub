import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { workflowsApi } from "../../api/workflows";
import { WorkflowsWidget } from "../dashboard/widgets/WorkflowsWidget";
import { WorkflowInputModal } from "../dashboard/widgets/WorkflowInputModal";
import { WorkflowExecutionModal } from "../dashboard/widgets/WorkflowExecutionModal";
import { FilterToolbar } from "../../components/FilterToolbar";
import { useWorkflowModals } from "../../hooks/useWorkflowModals";
import type { WorkflowDefinition, WorkflowExecution } from "../../types";
import "./WorkflowsPage.css";

type Filter = "all" | "idle" | "running" | "succeeded" | "failed";

export default function WorkflowsPage() {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");

  const { data: workflows = [], isLoading } = useQuery<WorkflowDefinition[]>({
    queryKey: ["workflows"],
    queryFn: workflowsApi.list,
  });

  const { data: workflowExecutions = [] } = useQuery<WorkflowExecution[]>({
    queryKey: ["workflow-executions"],
    queryFn: workflowsApi.listExecutions,
    refetchInterval: 5000,
  });

  const {
    workflowRunError,
    setWorkflowRunError,
    workflowInputModal,
    setWorkflowInputModal,
    runningWorkflowId,
    lastExecutionIdByWorkflow,
    workflowModal,
    setWorkflowModal,
    runWorkflowWithInputs,
  } = useWorkflowModals({
    workflows,
    confirmMessage: (workflow) => `Run workflow "${workflow.name}"?`,
  });

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

  const workflowFilters = [
    { value: "all", label: `All (${workflows.length})` },
    ...( ["idle", "running", "succeeded", "failed"] as const).map((s) => ({
      value: s,
      label: `${s.charAt(0).toUpperCase() + s.slice(1)}${statusCounts[s] > 0 ? ` (${statusCounts[s]})` : ""}`,
    })),
  ];

  return (
    <div className="workflows-page">
      <div className="workflows-page-card">
        <FilterToolbar
          search={search}
          onSearchChange={setSearch}
          filter={filter}
          onFilterChange={(f) => setFilter(f as Filter)}
          filters={workflowFilters}
          searchPlaceholder="Search by name or description…"
          shownCount={filtered.length}
          totalCount={workflows.length}
        />
      </div>

      {isLoading ? (
        <p className="workflows-page-empty">Loading…</p>
      ) : (
        <div className="workflows-page-content-card">
          <WorkflowsWidget
            workflows={filtered}
            executions={workflowExecutions}
            runningWorkflowId={runningWorkflowId}
            lastExecutionIdByWorkflow={lastExecutionIdByWorkflow}
            workflowRunError={workflowRunError}
            onClearError={() => setWorkflowRunError(null)}
            onRun={(workflow) => setWorkflowInputModal(workflow)}
            onOpenExecution={(workflowId, executionId) => {
              const workflow = workflows.find((w) => w.id === workflowId);
              if (!workflow) return;
              setWorkflowModal({ workflow, executionId });
            }}
          />
        </div>
      )}

      {workflowInputModal && (
        <WorkflowInputModal
          workflow={workflowInputModal}
          onClose={() => setWorkflowInputModal(null)}
          onSubmit={(inputs, skippedSteps) => {
            const workflow = workflowInputModal;
            if (!workflow) return;
            setWorkflowInputModal(null);
            runWorkflowWithInputs(workflow, inputs, skippedSteps);
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
