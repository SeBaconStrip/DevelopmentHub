import { useState, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { workflowsApi } from "../../../api/workflows";
import { useLogHub } from "../../../hooks/useLogHub";
import type { WorkflowDefinition, WorkflowExecutionDetail } from "../../../types";
import { Modal } from "../../../components/Modal";
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
    <Modal
      onClose={onClose}
      title={workflow.name}
      subtitle={
        <>
          Status:{" "}
          <span className={`workflow-status workflow-status--${status}`}>
            {status}
          </span>
        </>
      }
      className="workflow-modal"
    >
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
    </Modal>
  );
}
