import type { WorkflowDefinition, WorkflowExecution } from "../../../types";
import { Empty } from "./shared";
import { ErrorBar } from "../../../components/ErrorBar";
import "./WorkflowsWidget.css";

export function WorkflowsWidget({
  workflows,
  executions,
  runningWorkflowId,
  lastExecutionIdByWorkflow = {},
  workflowRunError,
  onRun,
  onOpenExecution,
  onClearError,
  onTagClick,
}: {
  workflows: WorkflowDefinition[];
  executions: WorkflowExecution[];
  runningWorkflowId: string | null;
  lastExecutionIdByWorkflow?: Record<string, string>;
  workflowRunError: string | null;
  onRun: (workflow: WorkflowDefinition) => void;
  onOpenExecution: (workflowId: string, executionId: string) => void;
  onClearError: () => void;
  onTagClick?: (tag: string) => void;
}) {
  return (
    <>
      <ErrorBar message={workflowRunError} onDismiss={onClearError} />
      {workflows.length === 0 ? (
        <Empty text="No workflows yet. Add them in Settings > Workflows." />
      ) : (
        <div className="workflow-list">
          {workflows.map((workflow) => {
            const latestExecution = executions
              .filter((e) => e.workflowId === workflow.id)
              .sort((a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime())[0];
            const isRunning = runningWorkflowId === workflow.id;
            const currentExecutionId = lastExecutionIdByWorkflow[workflow.id];

            return (
              <div key={workflow.id} className="workflow-card">
                <div className="workflow-copy">
                  <div className="workflow-title-row">
                    <span className="item-name">{workflow.name}</span>
                    {workflow.runElevated && (
                      <span className="workflow-elevated-badge" title="Runs elevated (UAC)">elevated</span>
                    )}
                    <span
                      className={`workflow-status workflow-status--${latestExecution?.status ?? "idle"}`}
                    >
                      {latestExecution?.status ?? "idle"}
                    </span>
                  </div>
                  {workflow.description && (
                    <span className="item-meta">{workflow.description}</span>
                  )}
                  {workflow.tags && workflow.tags.length > 0 && (
                    <div className="workflow-tags">
                      {workflow.tags.map((tag) => (
                        <span
                          key={tag}
                          className={`workflow-tag${onTagClick ? " workflow-tag--clickable" : ""}`}
                          onClick={onTagClick ? () => onTagClick(tag) : undefined}
                        >
                          {tag}
                        </span>
                      ))}
                    </div>
                  )}
                  <span className="item-meta">
                    {workflow.steps.length} step
                    {workflow.steps.length !== 1 ? "s" : ""}
                    {workflow.inputs.length > 0
                      ? ` · ${workflow.inputs.length} input${workflow.inputs.length !== 1 ? "s" : ""}`
                      : ""}
                  </span>
                  {isRunning && currentExecutionId && (
                    <button
                      className="workflow-link-btn"
                      onClick={() => onOpenExecution(workflow.id, currentExecutionId)}
                    >
                      ⟳ View current run…
                    </button>
                  )}
                  {!isRunning && latestExecution && (
                    <button
                      className="workflow-link-btn"
                      onClick={() => onOpenExecution(workflow.id, latestExecution.id)}
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
