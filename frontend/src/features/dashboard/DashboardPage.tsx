import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Responsive, useContainerWidth } from "react-grid-layout";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import "./DashboardPage.css";

import { fetchRepositories } from "../../api/repositories";
import { fetchPullRequests } from "../../api/pullRequests";
import { fetchScripts, fetchExecutions } from "../../api/scripts";
import {
  useUiStore,
  type BreakpointLayouts,
  type WidgetId,
} from "../../store/uiStore";
import { DashboardSettingsModal } from "../../components/DashboardSettingsModal";
import type { Repository, PullRequest, Script, Execution } from "../../types";

/* ─────────────────────────────────────────────────────────────── layout ── */

export default function DashboardPage() {
  const [isEditMode, setIsEditMode] = useState(false);
  const [showSettings, setShowSettings] = useState(false);

  const { dashboardWidgets, toggleWidget, gridLayouts, setGridLayouts, resetGridLayouts } = useUiStore();

  const { data: repos = [] } = useQuery<Repository[]>({ queryKey: ["repositories"], queryFn: fetchRepositories });
  const { data: prs = [] } = useQuery<PullRequest[]>({ queryKey: ["pullrequests"], queryFn: fetchPullRequests, refetchInterval: 120_000 });
  const { data: scripts = [] } = useQuery<Script[]>({ queryKey: ["scripts"], queryFn: fetchScripts });
  const { data: executions = [] } = useQuery<Execution[]>({ queryKey: ["executions"], queryFn: () => fetchExecutions(), refetchInterval: 5_000 });

  const enabled = dashboardWidgets.filter((w) => w.enabled);
  const disabled = dashboardWidgets.filter((w) => !w.enabled);

  const filteredLayouts: BreakpointLayouts = Object.fromEntries(
    Object.entries(gridLayouts).map(([bp, items]) => [
      bp,
      items.filter((item) => enabled.some((w) => w.id === item.i)),
    ]),
  );

  const { containerRef, width: containerWidth } = useContainerWidth();

  const bodyMap: Record<WidgetId, React.ReactNode> = {
    repositories: <RepositoriesBody repos={repos} />,
    pullRequests: <PullRequestsBody prs={prs} />,
    scripts: <ScriptsBody scripts={scripts} executions={executions} />,
    executions: <ExecutionsBody executions={executions} />,
  };

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
            <button className="btn-ghost" onClick={resetGridLayouts} title="Reset panel positions to defaults">
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
            <button key={w.id} className="btn-add-panel" onClick={() => toggleWidget(w.id)}>
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
            dragConfig={{ enabled: isEditMode, handle: ".drag-handle" } as object}
            resizeConfig={{ enabled: isEditMode, handles: ["se", "s", "e"] } as object}
            onLayoutChange={handleLayoutChange}
            margin={[16, 16]}
            containerPadding={[0, 0]}
          >
            {enabled.map((w) => (
              <div key={w.id} className="panel-wrapper">
                <Panel
                  icon={w.icon}
                  title={w.label}
                  isEditMode={isEditMode}
                  onClose={() => toggleWidget(w.id)}
                >
                  {bodyMap[w.id]}
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
  isEditMode,
  onClose,
  children,
}: {
  icon: string;
  title: string;
  isEditMode: boolean;
  onClose: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className={`panel${isEditMode ? " panel--editing" : ""}`}>
      <div className={`panel-header${isEditMode ? " drag-handle panel-header--draggable" : ""}`}>
        {isEditMode && <span className="panel-grip">⠿</span>}
        <span className="panel-icon">{icon}</span>
        <span className="panel-title">{title}</span>
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

function RepositoriesBody({ repos }: { repos: Repository[] }) {
  if (repos.length === 0) return <Empty text="No repositories found" />;
  return (
    <div>
      {repos.map((r) => (
        <div key={r.id} className="item-row">
          <div className="item-main">
            <div className="item-name-row">
              {r.isFavorite && <span className="item-fav">⭐</span>}
              <span className="item-name">{r.name}</span>
            </div>
            {r.currentBranch && (
              <span className="item-branch">{r.currentBranch}</span>
            )}
          </div>
          <div className="item-chips">
            {(r.aheadBy ?? 0) > 0 && <Chip bg="#dcfce7" fg="#15803d">+{r.aheadBy}</Chip>}
            {(r.behindBy ?? 0) > 0 && <Chip bg="#fef3c7" fg="#d97706">-{r.behindBy}</Chip>}
          </div>
        </div>
      ))}
    </div>
  );
}

/* ───────────────────────────────────────────────── Pull Requests body ── */

const VOTE_CHIP: Record<number, { bg: string; fg: string; label: string }> = {
  10:   { bg: "#dcfce7", fg: "#15803d", label: "Approved" },
  5:    { bg: "#bbf7d0", fg: "#166534", label: "Approved+" },
  0:    { bg: "#f3f4f6", fg: "#6b7280", label: "No vote" },
  [-5]: { bg: "#fef3c7", fg: "#d97706", label: "Waiting" },
  [-10]:{ bg: "#fee2e2", fg: "#dc2626", label: "Rejected" },
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
                <Chip bg={pr.isDraft ? "#f3f4f6" : "#ede9fe"} fg={pr.isDraft ? "#6b7280" : "#7c3aed"}>
                  {pr.isDraft ? "DRAFT" : "OPEN"}
                </Chip>
                <span className="item-pr-title">{pr.title}</span>
              </div>
              <span className="item-meta">{pr.repositoryName}</span>
            </div>
            {pr.isReviewer && <Chip bg={vote.bg} fg={vote.fg}>{vote.label}</Chip>}
          </div>
        );
      })}
    </div>
  );
}

/* ──────────────────────────────────────────────────────── Scripts body ── */

const STATUS_CHIP: Record<string, { bg: string; fg: string }> = {
  Running:   { bg: "#dbeafe", fg: "#1d4ed8" },
  Success:   { bg: "#dcfce7", fg: "#16a34a" },
  Failed:    { bg: "#fee2e2", fg: "#dc2626" },
  Cancelled: { bg: "#f3f4f6", fg: "#6b7280" },
};

function ScriptsBody({ scripts, executions }: { scripts: Script[]; executions: Execution[] }) {
  if (scripts.length === 0) return <Empty text="No scripts configured" />;
  return (
    <div>
      {scripts.map((s) => {
        const last = [...executions].reverse().find((e) => e.scriptDefinitionId === s.id);
        const chip = last ? (STATUS_CHIP[last.status] ?? STATUS_CHIP.Cancelled) : null;
        return (
          <div key={s.id} className="item-row">
            <div className="item-main">
              <div className="item-script-name">{s.name}</div>
              {s.description && <div className="item-desc">{s.description}</div>}
            </div>
            {chip && last && <Chip bg={chip.bg} fg={chip.fg}>{last.status}</Chip>}
          </div>
        );
      })}
    </div>
  );
}

/* ─────────────────────────────────────────────────────── Executions body ── */

function ExecutionsBody({ executions }: { executions: Execution[] }) {
  if (executions.length === 0) return <Empty text="No executions yet" />;
  return (
    <div>
      {executions.slice(0, 30).map((e) => {
        const s = STATUS_CHIP[e.status] ?? STATUS_CHIP.Cancelled;
        return (
          <div key={e.id} className="item-row">
            <span className="item-text">{e.scriptName}</span>
            <Chip bg={s.bg} fg={s.fg}>{e.status}</Chip>
          </div>
        );
      })}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────── shared ── */

function Chip({ bg, fg, children }: { bg: string; fg: string; children: React.ReactNode }) {
  return (
    <span className="chip" style={{ background: bg, color: fg }}>
      {children}
    </span>
  );
}

function Empty({ text }: { text: string }) {
  return <p className="empty-msg">{text}</p>;
}
