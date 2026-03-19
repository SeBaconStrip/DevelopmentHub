import type { WorkflowDefinition, WorkflowExecution } from "../../../types";
import { Empty } from "./shared";
import "./WorkflowsWidget.css";

export function WorkflowsWidget({
  workflows,
  executions,
  runningWorkflowId,
  workflowRunError,
  onRun,
  onOpenExecution,
  onClearError,
}: {
  workflows: WorkflowDefinition[];
  executions: WorkflowExecution[];
  runningWorkflowId: string | null;
  workflowRunError: string | null;
  onRun: (workflow: WorkflowDefinition) => void;
  onOpenExecution: (workflowId: string) => void;
  onClearError: () => void;
}) {
  return (
    <>
      {workflowRunError && (
        <div className="panel-error-bar">
          <span>⚠ {workflowRunError}</span>
          <button onClick={onClearError}>✕</button>
        </div>
      )}
      {workflows.length === 0 ? (
        <Empty text="No workflows yet. Add them in Settings > Workflows." />
      ) : (
        <div className="workflow-list">
          {workflows.map((workflow) => {
            const latestExecution = executions.find(
              (execution) => execution.workflowId === workflow.id,
            );
            const isRunning = runningWorkflowId === workflow.id;

            return (
              <div key={workflow.id} className="workflow-card">
                <div className="workflow-copy">
                  <div className="workflow-title-row">
                    <span className="item-name">{workflow.name}</span>
                    <span
                      className={`workflow-status workflow-status--${latestExecution?.status ?? "idle"}`}
                    >
                      {latestExecution?.status ?? "idle"}
                    </span>
                  </div>
                  {workflow.description && (
                    <span className="item-meta">{workflow.description}</span>
                  )}
                  <span className="item-meta">
                    {workflow.steps.length} step
                    {workflow.steps.length !== 1 ? "s" : ""}
                    {workflow.inputs.length > 0
                      ? ` · ${workflow.inputs.length} input${workflow.inputs.length !== 1 ? "s" : ""}`
                      : ""}
                  </span>
                  {latestExecution && (
                    <button
                      className="workflow-link-btn"
                      onClick={() => onOpenExecution(workflow.id)}
                    >
                      Last run: {latestExecution.status} ·{" "}
                      {new Date(latestExecution.startedAt).toLocaleString()}
                    </button>
                  )}
                </div>
                <button
                  className="btn-primary workflow-run-btn"
                  onClick={() => onRun(workflow)}
                  disabled={isRunning}
                >
                  {isRunning ? "Running..." : "Run"}
                </button>
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}
