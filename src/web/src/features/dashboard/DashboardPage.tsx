import { useState, useEffect, useRef } from "react";
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
import {
  useUiStore,
  type BreakpointLayouts,
  type WidgetId,
} from "../../store/uiStore";
import { DashboardSettingsModal } from "../../components/DashboardSettingsModal";
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
  const [showSettings, setShowSettings] = useState(false);
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

  const scanRepos = useMutation({
    mutationFn: repositoriesApi.scan,
    onSuccess: (data) => queryClient.setQueryData(["repositories"], data),
  });

  const [openError, setOpenError] = useState<string | null>(null);

  const openRepo = useMutation({
    mutationFn: ({
      id,
      openWith,
    }: {
      id: string;
      openWith: "VsCode" | "VisualStudio" | "Explorer";
    }) => repositoriesApi.open(id, { openWith }),
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

  const createTodo = useMutation({
    mutationFn: ({ title, linkUrl }: { title: string; linkUrl?: string }) =>
      todosApi.create(title, linkUrl),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });
  const updateTodo = useMutation({
    mutationFn: ({ id, title, linkUrl }: { id: string; title: string; linkUrl?: string }) =>
      todosApi.update(id, title, linkUrl),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });
  const toggleTodo = useMutation({
    mutationFn: ({ id, completed }: { id: string; completed: boolean }) =>
      todosApi.setCompleted(id, completed),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });
  const deleteTodo = useMutation({
    mutationFn: (id: string) => todosApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });
  const clearCompletedTodos = useMutation({
    mutationFn: todosApi.clearCompleted,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });

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
  };

  const widgetMap: Record<WidgetId, WidgetConfig> = {
    repositories: {
      body: (
        <RepositoriesWidget
          repos={repos}
          openError={openError}
          onClearOpenError={() => setOpenError(null)}
          onOpen={(id, openWith) => {
            setOpenError(null);
            openRepo.mutate({ id, openWith });
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
    pullRequests: { body: <PullRequestsWidget prs={prs} />, badge: prs.length },
    quickLinks: {
      body: <QuickLinksWidget links={customLinks} />,
      badge: customLinks.length,
    },
    todos: {
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

  const enabled = dashboardWidgets.filter((w) => w.enabled && w.id in widgetMap);
  const disabled = dashboardWidgets.filter((w) => !w.enabled && w.id in widgetMap);

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

  function handleToggleWidget(id: WidgetId) {
    toggleWidget(id);
  }

  const [isMaximized, setIsMaximized] = useState(true);

  useEffect(() => {
    const handler = (e: Event) => setIsMaximized((e as CustomEvent).detail.maximized);
    window.addEventListener("windowstate", handler);
    return () => window.removeEventListener("windowstate", handler);
  }, []);

  function sendWindowMsg(msg: string) {
    (window as any).chrome?.webview?.postMessage(msg);
  }

  const dragStartPos = useRef<{ x: number; y: number } | null>(null);

  useEffect(() => {
    function onMouseMove(e: MouseEvent) {
      if (!dragStartPos.current) return;
      const { x, y } = dragStartPos.current;
      if (Math.abs(e.clientX - x) > 4 || Math.abs(e.clientY - y) > 4) {
        dragStartPos.current = null;
        (window as any).chrome?.webview?.postMessage("drag");
      }
    }
    function onMouseUp() {
      dragStartPos.current = null;
    }
    window.addEventListener("mousemove", onMouseMove);
    window.addEventListener("mouseup", onMouseUp);
    return () => {
      window.removeEventListener("mousemove", onMouseMove);
      window.removeEventListener("mouseup", onMouseUp);
    };
  }, []);

  function handleHeaderMouseDown(e: React.MouseEvent) {
    if (e.button !== 0) return;
    if ((e.target as HTMLElement).closest("button")) return;
    dragStartPos.current = { x: e.clientX, y: e.clientY };
  }

  function handleHeaderDblClick(e: React.MouseEvent) {
    if ((e.target as HTMLElement).closest("button")) return;
    sendWindowMsg("maximize");
  }

  return (
    <div className="dash-root">
      {/* top header bar */}
      <header className="dash-header" onMouseDown={handleHeaderMouseDown} onDoubleClick={handleHeaderDblClick}>
        <div className="dash-title-area">
          <h1 className="dash-title">Development Hub</h1>
          <p className="dash-subtitle">
            {enabled.length} panel{enabled.length !== 1 ? "s" : ""} active
          </p>
        </div>
        <div className="dash-header-actions">
          {isEditMode && (
            <button
              className="btn-ghost"
              onClick={resetGridLayouts}
              title="Reset panel positions to defaults"
            >
              ↺ Reset
            </button>
          )}
          <button className="btn-ghost" onClick={() => setShowSettings(true)}>
            ⚙ Settings
          </button>
          <button
            className={isEditMode ? "btn-edit-done" : "btn-edit-layout"}
            onClick={() => setIsEditMode((v) => !v)}
          >
            {isEditMode ? "✓ Done" : "✎ Edit Layout"}
          </button>
          <div className="wc-buttons">
            <button className="wc-btn wc-minimize" onClick={() => sendWindowMsg("minimize")} title="Minimize">
              <svg width="10" height="1" viewBox="0 0 10 1"><rect width="10" height="1" fill="currentColor"/></svg>
            </button>
            <button className="wc-btn wc-maximize" onClick={() => sendWindowMsg("maximize")} title={isMaximized ? "Restore" : "Maximize"}>
              {isMaximized
                ? <svg width="10" height="10" viewBox="0 0 10 10"><rect x="0" y="3" width="7" height="7" fill="none" stroke="currentColor" strokeWidth="1"/><polyline points="3,3 3,0 10,0 10,7 7,7" fill="none" stroke="currentColor" strokeWidth="1"/></svg>
                : <svg width="10" height="10" viewBox="0 0 10 10"><rect x="0" y="0" width="10" height="10" fill="none" stroke="currentColor" strokeWidth="1"/></svg>
              }
            </button>
            <button className="wc-btn wc-close" onClick={() => sendWindowMsg("close")} title="Close">
              <svg width="10" height="10" viewBox="0 0 10 10"><line x1="0" y1="0" x2="10" y2="10" stroke="currentColor" strokeWidth="1.5"/><line x1="10" y1="0" x2="0" y2="10" stroke="currentColor" strokeWidth="1.5"/></svg>
            </button>
          </div>
        </div>
      </header>

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
              {enabled.map((w) => (
                <div key={w.id} className="panel-wrapper">
                  <Panel
                    icon={w.icon}
                    title={w.label}
                    badge={widgetMap[w.id].badge}
                    headerActions={widgetMap[w.id].headerActions}
                    isEditMode={isEditMode}
                    onClose={() => handleToggleWidget(w.id)}
                  >
                    {widgetMap[w.id].body}
                  </Panel>
                </div>
              ))}
            </Responsive>
          </div>
        )}

        {showSettings && (
          <DashboardSettingsModal onClose={() => setShowSettings(false)} />
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
