import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { workflowsApi } from "../api/workflows";
import type { WorkflowDefinition, RunWorkflowRequest } from "../types";

interface UseWorkflowModalsOptions {
  workflows: WorkflowDefinition[];
  confirmMessage?: (workflow: WorkflowDefinition) => string;
}

export function useWorkflowModals({ workflows }: UseWorkflowModalsOptions) {
  const queryClient = useQueryClient();
  const [workflowRunError, setWorkflowRunError] = useState<string | null>(null);
  const [workflowInputModal, setWorkflowInputModal] = useState<WorkflowDefinition | null>(null);
  const [runningWorkflowId, setRunningWorkflowId] = useState<string | null>(null);
  const [lastExecutionIdByWorkflow, setLastExecutionIdByWorkflow] = useState<Record<string, string>>({});
  const [workflowModal, setWorkflowModal] = useState<{
    workflow: WorkflowDefinition;
    executionId: string | null;
  } | null>(null);

  const runWorkflow = useMutation({
    mutationFn: ({ workflowId, request }: { workflowId: string; request: RunWorkflowRequest }) => {
      setRunningWorkflowId(workflowId);
      return workflowsApi.run(workflowId, request);
    },
    onSuccess: (execution, variables) => {
      setWorkflowRunError(null);
      queryClient.invalidateQueries({ queryKey: ["workflow-executions"] });
      setLastExecutionIdByWorkflow((prev) => ({ ...prev, [variables.workflowId]: execution.id }));
      const workflow = workflows.find((w) => w.id === variables.workflowId);
      if (workflow) {
        setWorkflowModal({ workflow, executionId: execution.id });
      }
    },
    onError: (err: Error) => setWorkflowRunError(err.message),
    onSettled: () => setRunningWorkflowId(null),
  });

  function runWorkflowWithInputs(
    workflow: WorkflowDefinition,
    inputs: Record<string, string>,
    skippedSteps: string[] = [],
  ) {
    setWorkflowRunError(null);
    runWorkflow.mutate({
      workflowId: workflow.id,
      request: { inputs, skippedSteps },
    });
  }

  return {
    workflowRunError,
    setWorkflowRunError,
    workflowInputModal,
    setWorkflowInputModal,
    runningWorkflowId,
    lastExecutionIdByWorkflow,
    workflowModal,
    setWorkflowModal,
    runWorkflowWithInputs,
  };
}
