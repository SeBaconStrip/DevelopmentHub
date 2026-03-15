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
import { fetchPullRequests } from "../../api/pullRequests";
import { launcherApi } from "../../api/launcher";
import {
  useUiStore,
  type BreakpointLayouts,
  type WidgetId,
} from "../../store/uiStore";
import { DashboardSettingsModal } from "../../components/DashboardSettingsModal";
import type { Repository, PullRequest } from "../../types";

/* ─────────────────────────────────────────────────────────────── layout ── */

export default function DashboardPage() {
  const [isEditMode, setIsEditMode] = useState(false);
  const [showSettings, setShowSettings] = useState(false);

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
            Drag panels by their header · resize from any edge · click ✕ to hide
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
                { enabled: isEditMode, handle: ".drag-handle" } as object
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
      </div>
    </div>
  );
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
      <div
        className={`panel-header${isEditMode ? " drag-handle panel-header--draggable" : ""}`}
      >
        {isEditMode && <span className="panel-grip">⠿</span>}
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

/* ─────────────────────────────────────────────────────────────── shared ── */

function Empty({ text }: { text: string }) {
  return <p className="empty-msg">{text}</p>;
}
