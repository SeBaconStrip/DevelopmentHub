import { useState, Fragment, useEffect, useRef } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Responsive, useContainerWidth } from "react-grid-layout";
import * as signalR from "@microsoft/signalr";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import "./DashboardPage.css";

import { fetchRepositories, repositoriesApi } from "../../api/repositories";
import { configApi } from "../../api/config";
import vscodeIconUrl from "../../assets/icons/vscode.svg";
import visualStudioIconUrl from "../../assets/icons/visualstudio.svg";
import explorerIconUrl from "../../assets/icons/windows-explorer.svg";
import githubIconUrl from "../../assets/icons/github.svg";
import azureDevOpsIconUrl from "../../assets/icons/azure-devops.svg";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowUpRightFromSquare,
  faCheck,
  faPen,
  faTrashArrowUp,
  faTrashCan,
  faXmark,
} from "@fortawesome/free-solid-svg-icons";
import { fetchPullRequests } from "../../api/pullRequests";
import { launcherApi } from "../../api/launcher";
import { todosApi } from "../../api/todos";
import { workflowsApi } from "../../api/workflows";
import {
  useUiStore,
  type BreakpointLayouts,
  type WidgetId,
} from "../../store/uiStore";
import { DashboardSettingsModal } from "../../components/DashboardSettingsModal";
import { useLogHub } from "../../hooks/useLogHub";
import type {
  CustomLink,
  Repository,
  PullRequest,
  RunWorkflowRequest,
  TodoItem,
  WorkflowDefinition,
  WorkflowExecution,
  WorkflowExecutionDetail,
} from "../../types";

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
    mutationFn: ({
      id,
      title,
      linkUrl,
    }: {
      id: string;
      title: string;
      linkUrl?: string;
    }) => todosApi.update(id, title, linkUrl),
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
    mutationFn: ({
      workflowId,
      request,
    }: {
      workflowId: string;
      request: RunWorkflowRequest;
    }) => {
      setRunningWorkflowId(workflowId);
      return workflowsApi.run(workflowId, request);
    },
    onSuccess: (execution, variables) => {
      setWorkflowRunError(null);
      queryClient.invalidateQueries({ queryKey: ["workflow-executions"] });

      const workflow = workflows.find((item) => item.id === variables.workflowId);
      if (workflow) {
        setWorkflowModal({
          workflow,
          executionId: execution.id,
        });
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
        <>
          {openError && (
            <div className="panel-error-bar">
              <span>⚠ {openError}</span>
              <button onClick={() => setOpenError(null)}>✕</button>
            </div>
          )}
          <RepositoriesBody
            repos={repos}
            onOpen={(id, openWith) => {
              setOpenError(null);
              openRepo.mutate({ id, openWith });
            }}
            onToggleFav={(id) => toggleFav.mutate(id)}
          />
        </>
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
    pullRequests: { body: <PullRequestsBody prs={prs} />, badge: prs.length },
    quickLinks: {
      body: <QuickLinksBody links={customLinks} />,
      badge: customLinks.length,
    },
    todos: {
      body: (
        <TodosBody
          todos={todos}
          onCreate={(title, linkUrl) => createTodo.mutateAsync({ title, linkUrl })}
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
      body: (
        <>
          {workflowRunError && (
            <div className="panel-error-bar">
              <span>⚠ {workflowRunError}</span>
              <button onClick={() => setWorkflowRunError(null)}>✕</button>
            </div>
          )}
          <WorkflowsBody
            workflows={workflows}
            executions={workflowExecutions}
            runningWorkflowId={runningWorkflowId}
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

              setWorkflowModal({
                workflow,
                executionId: execution.id,
              });
            }}
          />
        </>
      ),
      badge: workflows.length,
    },
  };

  const enabled = dashboardWidgets.filter(
    (w) => w.enabled && w.id in widgetMap,
  );
  const disabled = dashboardWidgets.filter(
    (w) => !w.enabled && w.id in widgetMap,
  );

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

  function handleResetGridLayouts() {
    resetGridLayouts();
  }

  return (
    <div className="dash-root">
      {/* top header bar */}
      <header className="dash-header">
        <div>
          <h1 className="dash-title">Development Hub</h1>
          <p className="dash-subtitle">
            {enabled.length} panel{enabled.length !== 1 ? "s" : ""} active
          </p>
        </div>
        <div className="dash-header-actions">
          {isEditMode && (
            <button
              className="btn-ghost"
              onClick={handleResetGridLayouts}
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
      request: {
        inputs,
        confirmed,
      },
    });
  }
}

/* ─────────────────────────────────────────────────────────────── Panel ── */

function Panel({
  icon,
  title,
  badge,
  headerActions,
  isEditMode,
  onClose,
  children,
}: {
  icon: string;
  title: string;
  badge?: number;
  headerActions?: React.ReactNode;
  isEditMode: boolean;
  onClose: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className={`panel${isEditMode ? " panel--editing" : ""}`}>
      <div className="panel-header">
        <span className="panel-icon">{icon}</span>
        <span className="panel-title">{title}</span>
        {badge != null && <span className="panel-badge">{badge}</span>}
        {headerActions && !isEditMode && (
          <div
            className="panel-header-actions"
            onPointerDown={(e) => e.stopPropagation()}
          >
            {headerActions}
          </div>
        )}
        {isEditMode && (
          <button
            className="panel-close-btn"
            onPointerDown={(e) => e.stopPropagation()}
            onClick={onClose}
          >
            ✕
          </button>
        )}
      </div>
      <div className="panel-body">{children}</div>
    </div>
  );
}

/* ───────────────────────────────────────────────── Repositories body ── */

function RepositoriesBody({
  repos,
  onOpen,
  onToggleFav,
}: {
  repos: Repository[];
  onOpen: (
    id: string,
    openWith: "VsCode" | "VisualStudio" | "Explorer",
  ) => void;
  onToggleFav: (id: string) => void;
}) {
  if (repos.length === 0) return <Empty text="No repositories found" />;
  return (
    <div className="repo-grid">
      {/* header */}
      <div className="repo-grid-header repo-col-name">Repository</div>
      <div className="repo-grid-header repo-col-branch">Branch</div>
      <div className="repo-grid-header repo-col-icon" />
      <div className="repo-grid-header repo-col-icon" />
      <div className="repo-grid-header repo-col-icon" />
      <div className="repo-grid-header repo-col-fav" />

      {/* rows */}
      {repos.map((r) => (
        <Fragment key={r.id}>
          {/* name */}
          <div className="repo-cell repo-col-name">
            <span className="item-name">{r.name}</span>
          </div>

          {/* branch + ahead/behind */}
          <div className="repo-cell repo-col-branch">
            {r.currentBranch && (
              <div className="item-branch-row">
                <span className="item-branch">{r.currentBranch}</span>
                {(r.aheadBy ?? 0) > 0 && (
                  <span className="item-ahead">↑{r.aheadBy}</span>
                )}
                {(r.behindBy ?? 0) > 0 && (
                  <span className="item-behind">↓{r.behindBy}</span>
                )}
              </div>
            )}
          </div>

          {/* VS Code */}
          <div className="repo-cell repo-col-icon">
            {r.entryPoints.some(
              (ep) => ep.type === "CodeWorkspace" || ep.type === "Folder",
            ) && (
              <button
                className="item-open-icon"
                onClick={() => onOpen(r.id, "VsCode")}
                title="In VS Code öffnen"
              >
                <img
                  src={vscodeIconUrl}
                  width="24"
                  height="24"
                  alt="VS Code"
                  draggable={false}
                />
              </button>
            )}
          </div>

          {/* Visual Studio */}
          <div className="repo-cell repo-col-icon">
            {r.entryPoints.some((ep) => ep.type === "Solution") && (
              <button
                className="item-open-icon"
                onClick={() => onOpen(r.id, "VisualStudio")}
                title="In Visual Studio öffnen"
              >
                <img
                  src={visualStudioIconUrl}
                  width="24"
                  height="24"
                  alt="Visual Studio"
                  draggable={false}
                />
              </button>
            )}
          </div>

          {/* Explorer */}
          <div className="repo-cell repo-col-icon">
            <button
              className="item-open-icon"
              onClick={() => onOpen(r.id, "Explorer")}
              title="In Explorer öffnen"
            >
              <img
                src={explorerIconUrl}
                width="24"
                height="24"
                alt="Explorer"
                draggable={false}
              />
            </button>
          </div>

          {/* Favourite */}
          <div className="repo-cell repo-col-fav">
            <button
              className={`repo-fav-btn${r.isFavorite ? " repo-fav-btn--active" : ""}`}
              onClick={() => onToggleFav(r.id)}
              title={
                r.isFavorite ? "Favorit entfernen" : "Als Favorit markieren"
              }
            >
              ★
            </button>
          </div>
        </Fragment>
      ))}
    </div>
  );
}

/* ───────────────────────────────────────────────── Pull Requests body ── */

const VOTE_LABEL: Record<number, string> = {
  10: "Approved",
  5: "Approved w/ suggestions",
  [-5]: "Waiting for author",
  [-10]: "Rejected",
};

const PROVIDER_ICONS: Record<PullRequest["providerId"], string> = {
  azureDevOps: azureDevOpsIconUrl,
  github: githubIconUrl,
};

function PullRequestsBody({ prs }: { prs: PullRequest[] }) {
  if (prs.length === 0) return <Empty text="No pull requests" />;
  return (
    <div className="pr-grid">
      {/* header cells — direct grid children, same as repo-grid pattern */}
      <div className="pr-grid-head pr-col-provider" />
      <div className="pr-grid-head pr-col-title">Title</div>
      <div className="pr-grid-head pr-col-repo">Repository</div>
      <div className="pr-grid-head pr-col-branch">Branch</div>
      <div className="pr-grid-head pr-col-author">Author</div>
      <div className="pr-grid-head pr-col-badges" />

      {/* rows — display:contents so cells share the parent grid tracks */}
      {prs.map((pr) => (
        <div
          key={`${pr.providerId}-${pr.prId}`}
          className="pr-grid-row"
          role="button"
          tabIndex={0}
          title={pr.title}
          onClick={() => launcherApi.openUrl(pr.url)}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              launcherApi.openUrl(pr.url);
            }
          }}
        >
          <div className="pr-cell pr-col-provider">
            <img
              src={PROVIDER_ICONS[pr.providerId]}
              alt={pr.providerId}
              className="pr-provider-icon"
              draggable={false}
            />
          </div>
          <div className="pr-cell pr-col-title">
            <span className="item-name">{pr.title}</span>
          </div>
          <div className="pr-cell pr-col-repo">
            <span className="item-meta">{pr.repositoryName}</span>
          </div>
          <div className="pr-cell pr-col-branch">
            <div className="item-branch-row">
              <span className="item-branch">{pr.sourceBranch}</span>
              <span style={{ color: "var(--text-muted)", opacity: 0.4 }}>
                →
              </span>
              <span className="item-branch">{pr.targetBranch}</span>
            </div>
          </div>
          <div className="pr-cell pr-col-author">
            <span className="item-meta">{pr.authorDisplayName}</span>
          </div>
          <div className="pr-cell pr-col-badges">
            {pr.isDraft && (
              <span className="pr-chip pr-chip--draft">Draft</span>
            )}
            {!pr.isDraft && pr.createdByMe && (
              <span className="pr-chip pr-chip--mine">Mine</span>
            )}
            {pr.isReviewer && pr.reviewerVote !== 0 && (
              <span
                className={`pr-chip pr-chip--vote pr-chip--vote-${pr.reviewerVote > 0 ? "pos" : pr.reviewerVote === -5 ? "wait" : "neg"}`}
              >
                {VOTE_LABEL[pr.reviewerVote] ?? "Reviewed"}
              </span>
            )}
            {pr.isReviewer && pr.reviewerVote === 0 && (
              <span className="pr-chip pr-chip--reviewer">Reviewer</span>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

function QuickLinksBody({ links }: { links: CustomLink[] }) {
  const [error, setError] = useState<string | null>(null);

  if (links.length === 0) {
    return <Empty text="No quick links yet. Add some in Settings > Quick Links." />;
  }

  async function handleOpen(link: CustomLink) {
    try {
      setError(null);
      if (link.type === "explorer") {
        await launcherApi.openExplorer(link.target);
        return;
      }

      await launcherApi.openUrl(link.target);
    } catch {
      setError(`Could not open "${link.name}".`);
    }
  }

  return (
    <div className="quick-links-list">
      {error && (
        <div className="panel-error-bar">
          <span>⚠ {error}</span>
          <button onClick={() => setError(null)}>✕</button>
        </div>
      )}
      {links.map((link, index) => (
        <button
          key={`${link.type}-${link.target}-${index}`}
          className="quick-link-item"
          onClick={() => handleOpen(link)}
          title={link.target}
        >
          <span className="quick-link-icon">
            {link.type === "explorer" ? "📁" : "🌐"}
          </span>
          <span className="quick-link-copy">
            <span className="item-name">{link.name}</span>
            <span className="item-meta">{link.target}</span>
          </span>
        </button>
      ))}
    </div>
  );
}

function WorkflowsBody({
  workflows,
  executions,
  runningWorkflowId,
  onRun,
  onOpenExecution,
}: {
  workflows: WorkflowDefinition[];
  executions: WorkflowExecution[];
  runningWorkflowId: string | null;
  onRun: (workflow: WorkflowDefinition) => void;
  onOpenExecution: (workflowId: string) => void;
}) {
  if (workflows.length === 0) {
    return <Empty text="No workflows yet. Add them in Settings > Workflows." />;
  }

  return (
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
  );
}

function WorkflowInputModal({
  workflow,
  onClose,
  onSubmit,
}: {
  workflow: WorkflowDefinition;
  onClose: () => void;
  onSubmit: (inputs: Record<string, string>) => void;
}) {
  const [values, setValues] = useState<Record<string, string>>(
    Object.fromEntries(
      workflow.inputs.map((input) => [input.name, input.defaultValue ?? ""]),
    ),
  );

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-card workflow-input-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="workflow-modal-header">
          <div>
            <h2 className="settings-modal-title">{workflow.name}</h2>
            <p className="settings-modal-sub">Enter the workflow inputs.</p>
          </div>
          <button className="settings-modal-close" onClick={onClose}>
            ×
          </button>
        </div>
        <div className="workflow-input-fields">
          {workflow.inputs.map((input) => (
            <label key={input.name} className="settings-field">
              <span className="settings-field-label">
                {input.label || input.name}
              </span>
              <input
                className="settings-input"
                value={values[input.name] ?? ""}
                onChange={(e) =>
                  setValues((prev) => ({
                    ...prev,
                    [input.name]: e.target.value,
                  }))
                }
              />
            </label>
          ))}
        </div>
        <div className="settings-save-bar">
          <button className="btn-primary" onClick={() => onSubmit(values)}>
            Run workflow
          </button>
          <button className="btn-close" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}

function TodosBody({
  todos,
  onCreate,
  onUpdate,
  onToggleCompleted,
  onDelete,
  onClearCompleted,
  isBusy,
}: {
  todos: TodoItem[];
  onCreate: (title: string, linkUrl?: string) => Promise<unknown>;
  onUpdate: (id: string, title: string, linkUrl?: string) => Promise<unknown>;
  onToggleCompleted: (id: string, completed: boolean) => Promise<unknown>;
  onDelete: (id: string) => Promise<unknown>;
  onClearCompleted: () => Promise<unknown>;
  isBusy: boolean;
}) {
  const [newTitle, setNewTitle] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingTitle, setEditingTitle] = useState("");
  const [showCompleted, setShowCompleted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const activeTodos = todos.filter((todo) => !todo.completed);
  const completedTodos = todos.filter((todo) => todo.completed);

  async function handleCreate(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const title = newTitle.trim();
    if (!title) {
      return;
    }

    try {
      setError(null);
      await onCreate(title);
      setNewTitle("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create todo.");
    }
  }

  async function handleSaveEdit(id: string) {
    const title = editingTitle.trim();
    if (!title) {
      setError("Todo title is required.");
      return;
    }

    try {
      setError(null);
      await onUpdate(id, title);
      setEditingId(null);
      setEditingTitle("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not update todo.");
    }
  }

  async function handleAction(action: () => Promise<unknown>, fallback: string) {
    try {
      setError(null);
      await action();
    } catch (err) {
      setError(err instanceof Error ? err.message : fallback);
    }
  }

  async function handleOpenLink(linkUrl: string) {
    try {
      setError(null);
      await launcherApi.openUrl(linkUrl);
    } catch {
      setError("Could not open todo link.");
    }
  }

  return (
    <div className="todo-widget">
      {error && (
        <div className="panel-error-bar">
          <span>⚠ {error}</span>
          <button onClick={() => setError(null)}>✕</button>
        </div>
      )}

      <form className="todo-create-row" onSubmit={handleCreate}>
        <input
          className="todo-input"
          value={newTitle}
          onChange={(e) => setNewTitle(e.target.value)}
          placeholder="Add a todo, optionally with a link..."
          disabled={isBusy}
        />
        <button className="btn-ghost todo-add-btn" type="submit" disabled={isBusy || !newTitle.trim()}>
          Add
        </button>
      </form>

      <div className="todo-list">
        {activeTodos.length === 0 ? (
          <Empty text="No open todos. Add one above to get started." />
        ) : (
          activeTodos.map((todo) => {
            const isEditing = editingId === todo.id;
            return (
              <div key={todo.id} className="todo-item">
                <label className="todo-check">
                  <input
                    type="checkbox"
                    checked={todo.completed}
                    onChange={() =>
                      handleAction(
                        () => onToggleCompleted(todo.id, true),
                        "Could not complete todo.",
                      )
                    }
                    disabled={isBusy}
                  />
                </label>
                <div className="todo-copy">
                  {isEditing ? (
                    <input
                      className="todo-input todo-input--inline"
                      value={editingTitle}
                      onChange={(e) => setEditingTitle(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") {
                          e.preventDefault();
                          void handleSaveEdit(todo.id);
                        }
                        if (e.key === "Escape") {
                          setEditingId(null);
                          setEditingTitle("");
                        }
                      }}
                      placeholder="Todo text, optionally with a link..."
                      autoFocus
                    />
                  ) : (
                    <>
                      <span className="todo-title">{todo.title}</span>
                      {todo.linkUrl && (
                        <span className="item-meta todo-link-text">{todo.linkUrl}</span>
                      )}
                    </>
                  )}
                </div>
                <div className="todo-actions">
                  {isEditing ? (
                    <>
                      <button
                        className="todo-icon-btn"
                        onClick={() => void handleSaveEdit(todo.id)}
                        disabled={isBusy}
                        title="Save todo"
                        type="button"
                      >
                        <FontAwesomeIcon icon={faCheck} className="todo-action-icon" />
                      </button>
                      <button
                        className="todo-icon-btn"
                        onClick={() => {
                          setEditingId(null);
                          setEditingTitle("");
                        }}
                        disabled={isBusy}
                        title="Cancel editing"
                        type="button"
                      >
                        <FontAwesomeIcon icon={faXmark} className="todo-action-icon" />
                      </button>
                    </>
                  ) : (
                    <>
                      {todo.linkUrl && (
                        <button
                          className="todo-icon-btn"
                          onClick={() => void handleOpenLink(todo.linkUrl!)}
                          disabled={isBusy}
                          title="Open todo link"
                          type="button"
                        >
                          <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="todo-action-icon" />
                        </button>
                      )}
                      <button
                        className="todo-icon-btn"
                        onClick={() => {
                          setEditingId(todo.id);
                          setEditingTitle(
                            todo.linkUrl ? `${todo.title} ${todo.linkUrl}` : todo.title,
                          );
                        }}
                        disabled={isBusy}
                        title="Edit todo"
                        type="button"
                      >
                        <FontAwesomeIcon icon={faPen} className="todo-action-icon" />
                      </button>
                      <button
                        className="todo-icon-btn todo-icon-btn--danger"
                        onClick={() =>
                          void handleAction(
                            () => onDelete(todo.id),
                            "Could not delete todo.",
                          )
                        }
                        disabled={isBusy}
                        title="Delete todo"
                        type="button"
                      >
                        <FontAwesomeIcon icon={faTrashCan} className="todo-action-icon" />
                      </button>
                    </>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      <div className="todo-completed">
        <button
          className="todo-completed-toggle"
          onClick={() => setShowCompleted((value) => !value)}
          disabled={completedTodos.length === 0}
        >
          <span>{showCompleted ? "▾" : "▸"} Done</span>
          <span className="panel-badge">{completedTodos.length}</span>
        </button>

        {showCompleted && completedTodos.length > 0 && (
          <div className="todo-list todo-list--completed">
            <div className="todo-completed-actions">
              <button
                className="todo-icon-btn todo-icon-btn--danger"
                onClick={() =>
                  void handleAction(
                    onClearCompleted,
                    "Could not clear completed todos.",
                  )
                }
                disabled={isBusy}
                title="Clear completed todos"
                type="button"
              >
                <FontAwesomeIcon icon={faTrashCan} className="todo-action-icon" />
              </button>
            </div>
            {completedTodos.map((todo) => (
              <div key={todo.id} className="todo-item todo-item--completed">
                <label className="todo-check">
                  <input
                    type="checkbox"
                    checked={todo.completed}
                    onChange={() =>
                      handleAction(
                        () => onToggleCompleted(todo.id, false),
                        "Could not restore todo.",
                      )
                    }
                    disabled={isBusy}
                  />
                </label>
                <div className="todo-copy">
                  <span className="todo-title">{todo.title}</span>
                  {todo.linkUrl && (
                    <span className="item-meta todo-link-text">{todo.linkUrl}</span>
                  )}
                  {todo.completedAt && (
                    <span className="item-meta">
                      Done {new Date(todo.completedAt).toLocaleDateString()}
                    </span>
                  )}
                </div>
                <div className="todo-actions">
                  {todo.linkUrl && (
                    <button
                      className="todo-icon-btn"
                      onClick={() => void handleOpenLink(todo.linkUrl!)}
                      disabled={isBusy}
                      title="Open todo link"
                      type="button"
                    >
                      <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="todo-action-icon" />
                    </button>
                  )}
                  <button
                    className="todo-icon-btn"
                    onClick={() =>
                      void handleAction(
                        () => onToggleCompleted(todo.id, false),
                        "Could not restore todo.",
                      )
                    }
                    disabled={isBusy}
                    title="Restore todo"
                    type="button"
                  >
                    <FontAwesomeIcon icon={faTrashArrowUp} className="todo-action-icon" />
                  </button>
                  <button
                    className="todo-icon-btn todo-icon-btn--danger"
                    onClick={() =>
                      void handleAction(
                        () => onDelete(todo.id),
                        "Could not delete todo.",
                      )
                    }
                    disabled={isBusy}
                    title="Delete todo"
                    type="button"
                  >
                    <FontAwesomeIcon icon={faTrashCan} className="todo-action-icon" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function WorkflowExecutionModal({
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

/* ─────────────────────────────────────────────────────────────── shared ── */

function Empty({ text }: { text: string }) {
  return <p className="empty-msg">{text}</p>;
}
