import { useState, useCallback, useRef, useLayoutEffect, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useHeaderActions } from "../../components/AppLayout";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Responsive } from "react-grid-layout";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import "./DashboardPage.css";

import { repositoriesApi } from "../../api/repositories";
import { configApi } from "../../api/config";
import { fetchPullRequests } from "../../api/pullRequests";
import { todosApi } from "../../api/todos";
import { workflowsApi } from "../../api/workflows";
import { useTodos } from "../../hooks/useTodos";
import { useTodoSyncHub } from "../../hooks/useTodoSyncHub";
import { useRepositoryScan } from "../../hooks/useRepositoryScan";
import { useWorkflowModals } from "../../hooks/useWorkflowModals";
import { useRepositoryHub } from "../../hooks/useRepositoryHub";
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

// Persists the container width across unmount/remount so the grid never
// starts with the wrong width when navigating back to the dashboard.
let _cachedContainerWidth = window.innerWidth;

export default function DashboardPage() {
  const [isEditMode, setIsEditMode] = useState(false);

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
    queryFn: repositoriesApi.getAll,
    refetchInterval: (config?.scanIntervalMinutes ?? 30) * 60 * 1000,
  });

  // Invalidate immediately when the backend signals a scan has finished
  const handleRepositoriesUpdated = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ["repositories"] });
  }, [queryClient]);
  useRepositoryHub(handleRepositoriesUpdated);

  const handleTodosUpdated = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ["todos"] });
  }, [queryClient]);
  useTodoSyncHub(handleTodosUpdated);

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
    queryFn: () => {
      console.log("[Todos] Refetching todos from backend");
      return todosApi.getAll();
    },
    refetchInterval: (config?.todoSyncIntervalSeconds ?? 300) * 1000,
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

  const {
    createTodo,
    updateTodo,
    toggleTodo,
    deleteTodo,
    clearCompletedTodos,
  } = useTodos();

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
    confirmMessage: (workflow) => `Workflow "${workflow.name}" ausführen?`,
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
          onCreate={(title, linkUrl) =>
            createTodo.mutateAsync({ title, linkUrl })
          }
          onUpdate={(id, title, linkUrl) =>
            updateTodo.mutateAsync({ id, title, linkUrl })
          }
          onToggleCompleted={(id, completed) =>
            toggleTodo.mutateAsync({ id, completed })
          }
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
          lastExecutionIdByWorkflow={lastExecutionIdByWorkflow}
          workflowRunError={workflowRunError}
          onClearError={() => setWorkflowRunError(null)}
          onRun={(workflow) => setWorkflowInputModal(workflow)}
          onOpenExecution={(workflowId, executionId) => {
            const workflow = workflows.find((item) => item.id === workflowId);
            if (!workflow) return;
            setWorkflowModal({ workflow, executionId });
          }}
        />
      ),
      badge: workflows.length,
      headerActions: (
        <button
          className="panel-action-btn"
          onClick={() => {
            queryClient.invalidateQueries({ queryKey: ["workflows"] });
            queryClient.invalidateQueries({ queryKey: ["workflow-executions"] });
          }}
          title="Refresh workflows"
        >
          ↻
        </button>
      ),
    },
  };

  const isBuiltin = (id: string): id is BuiltinWidgetId =>
    (BUILTIN_WIDGET_IDS as readonly string[]).includes(id);

  const enabled = dashboardWidgets.filter(
    (w) => w.enabled && (isBuiltin(w.id) || true),
  );
  const disabled = dashboardWidgets.filter((w) => !w.enabled);

  const filteredLayouts: BreakpointLayouts = Object.fromEntries(
    Object.entries(gridLayouts).map(([bp, items]) => [
      bp,
      items.filter((item) => enabled.some((w) => w.id === item.i)),
    ]),
  );

  const containerRef = useRef<HTMLDivElement>(null);
  const [containerWidth, setContainerWidth] = useState(
    () => _cachedContainerWidth,
  );

  const [hasScrollbar, setHasScrollbar] = useState(false);
  useEffect(() => {
    const scrollEl = document.querySelector(".app-scroll") as HTMLElement | null;
    if (!scrollEl) return;
    const check = () =>
      setHasScrollbar(scrollEl.scrollHeight > scrollEl.clientHeight);
    check();
    const ro = new ResizeObserver(check);
    ro.observe(scrollEl);
    if (containerRef.current) ro.observe(containerRef.current);
    return () => ro.disconnect();
  }, [enabled.length]);

  useLayoutEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    setContainerWidth(el.offsetWidth);
    _cachedContainerWidth = el.offsetWidth;
    const ro = new ResizeObserver(([entry]) => {
      _cachedContainerWidth = entry.contentRect.width;
      setContainerWidth(entry.contentRect.width);
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  function handleLayoutChange(_: unknown, layouts: unknown) {
    const incoming = layouts as BreakpointLayouts;
    const merged = Object.fromEntries(
      Object.entries(gridLayouts).map(([bp, items]) => [
        bp,
        items.map(
          (item) => (incoming[bp] ?? []).find((l) => l.i === item.i) ?? item,
        ),
      ]),
    );
    setGridLayouts(merged);
  }

  function handleToggleWidget(id: string) {
    toggleWidget(id);
  }

  const navigate = useNavigate();

  const resetGridLayoutsCb = useCallback(resetGridLayouts, []);

  useHeaderActions(
    <>
      {isEditMode && (
        <button
          className="btn-ghost"
          onClick={resetGridLayoutsCb}
          title="Reset panel positions to defaults"
        >
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
    <div className={`dash-page${hasScrollbar ? " dash-page--scroll-pad" : ""}`}>
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
          Drag panels from anywhere · resize from the right, bottom or corner ·
          click ✕ to hide
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
