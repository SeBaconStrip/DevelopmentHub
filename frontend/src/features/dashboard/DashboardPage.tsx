import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { Responsive, useContainerWidth } from "react-grid-layout";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import "./DashboardPage.css";

import { fetchRepositories, repositoriesApi } from "../../api/repositories";
import vscodeIconUrl from "../../assets/icons/vscode.svg";
import visualStudioIconUrl from "../../assets/icons/visualstudio.svg";
import { fetchPullRequests } from "../../api/pullRequests";
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

  const { data: repos = [], refetch: refetchRepos } = useQuery<Repository[]>({
    queryKey: ["repositories"],
    queryFn: fetchRepositories,
  });

  const openRepo = useMutation({
    mutationFn: ({
      id,
      openWith,
    }: {
      id: string;
      openWith: "VsCode" | "VisualStudio";
    }) => repositoriesApi.open(id, { openWith }),
  });

  const toggleFav = useMutation({
    mutationFn: (id: string) => repositoriesApi.toggleFavorite(id),
    onSuccess: () => refetchRepos(),
  });
  const { data: prs = [] } = useQuery<PullRequest[]>({
    queryKey: ["pullrequests"],
    queryFn: fetchPullRequests,
    refetchInterval: 120_000,
  });

  type WidgetConfig = {
    body: React.ReactNode;
    badge?: number;
    headerActions?: React.ReactNode;
  };
  const widgetMap: Record<WidgetId, WidgetConfig> = {
    repositories: {
      body: (
        <RepositoriesBody
          repos={repos}
          onOpen={(id, openWith) => openRepo.mutate({ id, openWith })}
          onToggleFav={(id) => toggleFav.mutate(id)}
        />
      ),
      badge: repos.length,
      headerActions: (
        <button
          className="panel-action-btn"
          onClick={() => refetchRepos()}
          title="Repositories aktualisieren"
        >
          ↻
        </button>
      ),
    },
    pullRequests: { body: <PullRequestsBody prs={prs} /> },
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

  return (
    <div className="dash-page">
      {/* page header */}
      <div className="dash-header">
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
        </div>
      </div>

      {/* edit mode: re-add hidden panels */}
      {isEditMode && disabled.length > 0 && (
        <div className="dash-hidden-panels">
          <span className="dash-hidden-label">Hidden panels:</span>
          {disabled.map((w) => (
            <button
              key={w.id}
              className="btn-add-panel"
              onClick={() => toggleWidget(w.id)}
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
                  onClose={() => toggleWidget(w.id)}
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
  onOpen: (id: string, openWith: "VsCode" | "VisualStudio") => void;
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
      <div className="repo-grid-header repo-col-fav" />

      {/* rows */}
      {repos.map((r) => (
        <>
          {/* name */}
          <div key={r.id + "-name"} className="repo-cell repo-col-name">
            <span className="item-name">{r.name}</span>
          </div>

          {/* branch + ahead/behind */}
          <div key={r.id + "-branch"} className="repo-cell repo-col-branch">
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
          <div key={r.id + "-vscode"} className="repo-cell repo-col-icon">
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
          <div key={r.id + "-vs"} className="repo-cell repo-col-icon">
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

          {/* Favourite */}
          <div key={r.id + "-fav"} className="repo-cell repo-col-fav">
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
        </>
      ))}
    </div>
  );
}

/* ───────────────────────────────────────────────── Pull Requests body ── */

const VOTE_CHIP: Record<number, { bg: string; fg: string; label: string }> = {
  10: { bg: "#dcfce7", fg: "#15803d", label: "Approved" },
  5: { bg: "#bbf7d0", fg: "#166534", label: "Approved+" },
  0: { bg: "#f3f4f6", fg: "#6b7280", label: "No vote" },
  [-5]: { bg: "#fef3c7", fg: "#d97706", label: "Waiting" },
  [-10]: { bg: "#fee2e2", fg: "#dc2626", label: "Rejected" },
};

function PullRequestsBody({ prs }: { prs: PullRequest[] }) {
  if (prs.length === 0) return <Empty text="No pull requests" />;
  return (
    <div>
      {prs.map((pr) => {
        const vote = VOTE_CHIP[pr.reviewerVote] ?? VOTE_CHIP[0];
        return (
          <div key={pr.prId} className="item-row">
            <div className="item-main">
              <div className="item-pr-top">
                <Chip
                  bg={pr.isDraft ? "#f3f4f6" : "#ede9fe"}
                  fg={pr.isDraft ? "#6b7280" : "#7c3aed"}
                >
                  {pr.isDraft ? "DRAFT" : "OPEN"}
                </Chip>
                <span className="item-pr-title">{pr.title}</span>
              </div>
              <span className="item-meta">{pr.repositoryName}</span>
            </div>
            {pr.isReviewer && (
              <Chip bg={vote.bg} fg={vote.fg}>
                {vote.label}
              </Chip>
            )}
          </div>
        );
      })}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────── shared ── */

function Chip({
  bg,
  fg,
  children,
}: {
  bg: string;
  fg: string;
  children: React.ReactNode;
}) {
  return (
    <span className="chip" style={{ background: bg, color: fg }}>
      {children}
    </span>
  );
}

function Empty({ text }: { text: string }) {
  return <p className="empty-msg">{text}</p>;
}
