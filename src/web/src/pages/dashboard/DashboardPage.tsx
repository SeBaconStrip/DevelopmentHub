import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { useHeaderActions } from "../../components/AppLayout";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Responsive, useContainerWidth } from "react-grid-layout";
import * as signalR from "@microsoft/signalr";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import "./DashboardPage.css";

import { fetchRepositories, repositoriesApi } from "../../api/repositories";
import { configApi } from "../../api/config";
import { fetchPullRequests } from "../../api/pullRequests";
import { todosApi } from "../../api/todos";
import { workflowsApi } from "../../api/workflows";
import { useTodos } from "../../hooks/useTodos";
import { useRepositoryScan } from "../../hooks/useRepositoryScan";
import {
  useUiStore,
  type BreakpointLayouts,
  BUILTIN_WIDGET_IDS,
  type BuiltinWidgetId,
} from "../../store/uiStore";
import { PluginWidget } from "../../plugins/PluginWidget";
import type {
  Repository,
  PullRequest,
  RunWorkflowRequest,
  TodoItem,
  WorkflowDefinition,
  WorkflowExecution,
} from "../../types";

import { Panel } from "./components/Panel";
import { RepositoriesWidget } from "./widgets/RepositoriesWidget";
import { PullRequestsWidget } from "./widgets/PullRequestsWidget";
import { QuickLinksWidget } from "./widgets/QuickLinksWidget";
import { TodosWidget } from "./widgets/TodosWidget";
import { WorkflowsWidget } from "./widgets/WorkflowsWidget";
import { WorkflowInputModal } from "./widgets/WorkflowInputModal";
import { WorkflowExecutionModal } from "./widgets/WorkflowExecutionModal";

/* ─────────────────────────────────────────────────────────────── layout ── */

export default function DashboardPage() {
  const [isEditMode, setIsEditMode] = useState(false);
  const [workflowRunError, setWorkflowRunError] = useState<string | null>(null);
  const [workflowInputModal, setWorkflowInputModal] = useState<WorkflowDefinition | null>(null);
  const [runningWorkflowId, setRunningWorkflowId] = useState<string | null>(null);
  const [workflowModal, setWorkflowModal] = useState<{
    workflow: WorkflowDefinition;
    executionId: string | null;
  } | null>(null);

  const {
    dashboardWidgets,
    toggleWidget,
    gridLayouts,
    setGridLayouts,
    resetGridLayouts,
  } = useUiStore();

  const queryClient = useQueryClient();

  const { data: config } = useQuery({
    queryKey: ["config"],
    queryFn: configApi.get,
  });
  const customLinks = config?.customLinks ?? [];

  const { data: repos = [], refetch: refetchRepos } = useQuery<Repository[]>({
    queryKey: ["repositories"],
    queryFn: fetchRepositories,
    refetchInterval: (config?.scanIntervalMinutes ?? 30) * 60 * 1000,
  });

  // Invalidate immediately when the backend signals a scan has finished
  const repoHubRef = useRef<signalR.HubConnection | null>(null);
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/log")
      .withAutomaticReconnect()
      .build();
    repoHubRef.current = connection;
    connection.on("RepositoriesUpdated", () => {
      queryClient.invalidateQueries({ queryKey: ["repositories"] });
    });
    connection
      .start()
      .catch((err) =>
        console.warn("SignalR (repo updates) connection failed:", err),
      );
    return () => {
      connection.stop();
    };
  }, [queryClient]);

  const scanRepos = useRepositoryScan();

  const [openError, setOpenError] = useState<string | null>(null);

  const openRepo = useMutation({
    mutationFn: ({ id, openerId }: { id: string; openerId?: string }) =>
      repositoriesApi.open(id, { openerId }),
    onError: (err) => setOpenError(err.message),
  });

  const toggleFav = useMutation({
    mutationFn: (id: string) => repositoriesApi.toggleFavorite(id),
    onSuccess: () => refetchRepos(),
  });

  const { data: prs = [] } = useQuery<PullRequest[]>({
    queryKey: ["pullrequests"],
    queryFn: fetchPullRequests,
    refetchInterval: (config?.prRefreshIntervalSeconds ?? 120) * 1000,
  });

  const { data: todos = [] } = useQuery<TodoItem[]>({
    queryKey: ["todos"],
    queryFn: todosApi.getAll,
  });

  const { data: workflowExecutions = [] } = useQuery<WorkflowExecution[]>({
    queryKey: ["workflow-executions"],
    queryFn: workflowsApi.listExecutions,
    refetchInterval: 5000,
  });

  const { data: workflows = [] } = useQuery<WorkflowDefinition[]>({
    queryKey: ["workflows"],
    queryFn: workflowsApi.list,
  });

  const { createTodo, updateTodo, toggleTodo, deleteTodo, clearCompletedTodos } = useTodos();

  const runWorkflow = useMutation({
    mutationFn: ({ workflowId, request }: { workflowId: string; request: RunWorkflowRequest }) => {
      setRunningWorkflowId(workflowId);
      return workflowsApi.run(workflowId, request);
    },
    onSuccess: (execution, variables) => {
      setWorkflowRunError(null);
      queryClient.invalidateQueries({ queryKey: ["workflow-executions"] });

      const workflow = workflows.find((item) => item.id === variables.workflowId);
      if (workflow) {
        setWorkflowModal({ workflow, executionId: execution.id });
      }
    },
    onError: (err) => setWorkflowRunError(err.message),
    onSettled: () => setRunningWorkflowId(null),
  });

  type WidgetConfig = {
    body: React.ReactNode;
    badge?: number;
    headerActions?: React.ReactNode;
    onTitleClick?: () => void;
  };

  const widgetMap: Record<BuiltinWidgetId, WidgetConfig> = {
    repositories: {
      onTitleClick: () => navigate("/repositories"),
      body: (
        <RepositoriesWidget
          repos={repos}
          openers={config?.repositoryOpeners ?? []}
          openError={openError}
          onClearOpenError={() => setOpenError(null)}
          onOpen={(id, openerId) => {
            setOpenError(null);
            openRepo.mutate({ id, openerId });
          }}
          onToggleFav={(id) => toggleFav.mutate(id)}
        />
      ),
      badge: repos.length,
      headerActions: (
        <button
          className="panel-action-btn"
          onClick={() => scanRepos.mutate()}
          disabled={scanRepos.isPending}
          title="Repositories neu scannen"
        >
          ↻
        </button>
      ),
    },
    pullRequests: {
      onTitleClick: () => navigate("/pull-requests"),
      body: <PullRequestsWidget prs={prs} />,
      badge: prs.length,
    },
    quickLinks: {
      onTitleClick: () => navigate("/quick-links"),
      body: <QuickLinksWidget links={customLinks} />,
      badge: customLinks.length,
    },
    todos: {
      onTitleClick: () => navigate("/todos"),
      body: (
        <TodosWidget
          todos={todos}
          onCreate={(title, linkUrl) => createTodo.mutateAsync({ title, linkUrl })}
          onUpdate={(id, title, linkUrl) => updateTodo.mutateAsync({ id, title, linkUrl })}
          onToggleCompleted={(id, completed) => toggleTodo.mutateAsync({ id, completed })}
          onDelete={(id) => deleteTodo.mutateAsync(id)}
          onClearCompleted={() => clearCompletedTodos.mutateAsync()}
          isBusy={
            createTodo.isPending ||
            updateTodo.isPending ||
            toggleTodo.isPending ||
            deleteTodo.isPending ||
            clearCompletedTodos.isPending
          }
        />
      ),
      badge: todos.filter((todo) => !todo.completed).length,
    },
    workflows: {
      onTitleClick: () => navigate("/workflows"),
      body: (
        <WorkflowsWidget
          workflows={workflows}
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
            const workflow = workflows.find((item) => item.id === workflowId);
            const execution = workflowExecutions.find((item) => item.workflowId === workflowId);
            if (!workflow || !execution) return;
            setWorkflowModal({ workflow, executionId: execution.id });
          }}
        />
      ),
      badge: workflows.length,
    },
  };

  const isBuiltin = (id: string): id is BuiltinWidgetId =>
    (BUILTIN_WIDGET_IDS as readonly string[]).includes(id);

  const enabled = dashboardWidgets.filter((w) => w.enabled && (isBuiltin(w.id) || true));
  const disabled = dashboardWidgets.filter((w) => !w.enabled);

  const filteredLayouts: BreakpointLayouts = Object.fromEntries(
    Object.entries(gridLayouts).map(([bp, items]) => [
      bp,
      items.filter((item) => enabled.some((w) => w.id === item.i)),
    ]),
  );

  const { containerRef, width: containerWidth } = useContainerWidth();

  function handleLayoutChange(_: unknown, layouts: unknown) {
    setGridLayouts(layouts as BreakpointLayouts);
  }

  function handleToggleWidget(id: string) {
    toggleWidget(id);
  }

  const navigate = useNavigate();

  const resetGridLayoutsCb = useCallback(resetGridLayouts, []);

  useHeaderActions(
    <>
      {isEditMode && (
        <button className="btn-ghost" onClick={resetGridLayoutsCb} title="Reset panel positions to defaults">
          ↺ Reset
        </button>
      )}
      <button
        className={isEditMode ? "btn-edit-done" : "btn-edit-layout"}
        onClick={() => setIsEditMode((v) => !v)}
      >
        {isEditMode ? "✓ Done" : "✎ Edit Layout"}
      </button>
    </>,
    [isEditMode],
  );

  return (
    <div className="dash-page">
        {/* edit mode: re-add hidden panels */}
        {isEditMode && disabled.length > 0 && (
          <div className="dash-hidden-panels">
            <span className="dash-hidden-label">Hidden panels:</span>
            {disabled.map((w) => (
              <button
                key={w.id}
                className="btn-add-panel"
                onClick={() => handleToggleWidget(w.id)}
              >
                {w.icon} + {w.label}
              </button>
            ))}
          </div>
        )}

        {isEditMode && (
          <div className="dash-edit-hint">
            <span>✋</span>
            Drag panels from anywhere · resize from the right, bottom or corner · click ✕ to hide
          </div>
        )}

        {enabled.length === 0 ? (
          <div className="card dash-empty-card">
            <p>No panels visible</p>
            <p>
              Open <strong>⚙ Settings</strong> or use{" "}
              <strong>✎ Edit Layout</strong> to add panels.
            </p>
          </div>
        ) : (
          <div ref={containerRef}>
            <Responsive
              width={containerWidth}
              layouts={filteredLayouts}
              breakpoints={{ lg: 1200, md: 900, sm: 600, xs: 0 }}
              cols={{ lg: 12, md: 10, sm: 6, xs: 4 }}
              rowHeight={54}
              dragConfig={
                { enabled: isEditMode, cancel: ".panel-close-btn" } as object
              }
              resizeConfig={
                { enabled: isEditMode, handles: ["se", "s", "e"] } as object
              }
              onLayoutChange={handleLayoutChange}
              margin={[16, 16]}
              containerPadding={[0, 0]}
            >
              {enabled.map((w) => {
                const cfg = isBuiltin(w.id) ? widgetMap[w.id] : null;
                return (
                  <div key={w.id} className="panel-wrapper">
                    <Panel
                      icon={w.icon}
                      title={w.label}
                      badge={cfg?.badge}
                      headerActions={cfg?.headerActions}
                      onTitleClick={cfg?.onTitleClick}
                      isEditMode={isEditMode}
                      onClose={() => handleToggleWidget(w.id)}
                    >
                      {cfg ? cfg.body : <PluginWidget widgetId={w.id} />}
                    </Panel>
                  </div>
                );
              })}
            </Responsive>
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

  function runWorkflowWithInputs(
    workflow: WorkflowDefinition,
    inputs: Record<string, string>,
  ) {
    const confirmed =
      !workflow.requiresConfirmation ||
      window.confirm(`Workflow "${workflow.name}" ausführen?`);
    if (!confirmed) return;

    setWorkflowRunError(null);
    runWorkflow.mutate({
      workflowId: workflow.id,
      request: { inputs, confirmed },
    });
  }
}
