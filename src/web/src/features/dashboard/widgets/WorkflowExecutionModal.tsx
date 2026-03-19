import { useState, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { workflowsApi } from "../../../api/workflows";
import { useLogHub } from "../../../hooks/useLogHub";
import type { WorkflowDefinition, WorkflowExecutionDetail } from "../../../types";
import "./WorkflowsWidget.css";

export function WorkflowExecutionModal({
  workflow,
  executionId,
  onClose,
}: {
  workflow: WorkflowDefinition;
  executionId: string | null;
  onClose: () => void;
}) {
  const [logLines, setLogLines] = useState<WorkflowExecutionDetail["logLines"]>(
    [],
  );
  const [status, setStatus] = useState<string>("running");
  const { data } = useQuery({
    queryKey: ["workflow-execution", executionId],
    queryFn: () =>
      executionId ? workflowsApi.getExecution(executionId) : Promise.resolve(null),
    enabled: executionId !== null,
  });

  useEffect(() => {
    if (!data) return;
    setLogLines(data.logLines);
    setStatus(data.status);
  }, [data]);

  useLogHub(
    executionId,
    (line) => setLogLines((prev) => [...prev, line]),
    (completed) => setStatus(completed.status),
  );

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-card workflow-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="workflow-modal-header">
          <div>
            <h2 className="settings-modal-title">{workflow.name}</h2>
            <p className="settings-modal-sub">
              Status:{" "}
              <span className={`workflow-status workflow-status--${status}`}>
                {status}
              </span>
            </p>
          </div>
          <button className="settings-modal-close" onClick={onClose}>
            ×
          </button>
        </div>
        <div className="workflow-log">
          {logLines.length === 0 ? (
            <p className="empty-msg">Waiting for log output...</p>
          ) : (
            logLines.map((line, index) => (
              <div
                key={`${line.timestamp}-${index}`}
                className={`workflow-log-line workflow-log-line--${line.stream}`}
              >
                <span className="workflow-log-time">
                  {new Date(line.timestamp).toLocaleTimeString()}
                </span>
                <span>{line.text}</span>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
}
