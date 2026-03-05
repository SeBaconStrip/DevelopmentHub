import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Responsive, useContainerWidth } from "react-grid-layout";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";

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

  const {
    dashboardWidgets,
    toggleWidget,
    gridLayouts,
    setGridLayouts,
    resetGridLayouts,
  } = useUiStore();

  const { data: repos = [] } = useQuery<Repository[]>({
    queryKey: ["repositories"],
    queryFn: fetchRepositories,
  });
  const { data: prs = [] } = useQuery<PullRequest[]>({
    queryKey: ["pullrequests"],
    queryFn: fetchPullRequests,
    refetchInterval: 120_000,
  });
  const { data: scripts = [] } = useQuery<Script[]>({
    queryKey: ["scripts"],
    queryFn: fetchScripts,
  });
  const { data: executions = [] } = useQuery<Execution[]>({
    queryKey: ["executions"],
    queryFn: () => fetchExecutions(),
    refetchInterval: 5_000,
  });

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
    <div style={{ minHeight: "100vh", padding: "28px 24px 48px" }}>
      {/* page header */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          marginBottom: 24,
          gap: 12,
        }}
      >
        <div>
          <h1
            style={{
              fontSize: 26,
              fontWeight: 700,
              color: "#fff",
              margin: 0,
              textShadow: "0 1px 4px rgba(0,0,0,0.2)",
            }}
          >
            Dashboard
          </h1>
          <p
            style={{
              fontSize: 13,
              color: "rgba(255,255,255,0.65)",
              margin: "3px 0 0",
            }}
          >
            {enabled.length} panel{enabled.length !== 1 ? "s" : ""} active
          </p>
        </div>

        <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
          {isEditMode && (
            <button
              onClick={resetGridLayouts}
              style={ghostBtn}
              title="Reset panel positions to defaults"
            >
              ↺ Reset
            </button>
          )}
          <button onClick={() => setShowSettings(true)} style={ghostBtn}>
            ⚙ Settings
          </button>
          <button
            onClick={() => setIsEditMode((v) => !v)}
            style={isEditMode ? editDoneBtn : editBtn}
          >
            {isEditMode ? "✓ Done" : "✎ Edit Layout"}
          </button>
        </div>
      </div>

      {/* edit mode: re-add hidden panels */}
      {isEditMode && disabled.length > 0 && (
        <div
          style={{
            display: "flex",
            flexWrap: "wrap",
            gap: 8,
            marginBottom: 16,
          }}
        >
          <span
            style={{
              fontSize: 12,
              color: "rgba(255,255,255,0.55)",
              alignSelf: "center",
              marginRight: 4,
            }}
          >
            Hidden panels:
          </span>
          {disabled.map((w) => (
            <button
              key={w.id}
              onClick={() => toggleWidget(w.id)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 6,
                background: "rgba(255,255,255,0.15)",
                border: "1.5px dashed rgba(255,255,255,0.4)",
                borderRadius: 8,
                padding: "5px 14px",
                color: "#fff",
                fontSize: 13,
                fontWeight: 500,
                cursor: "pointer",
              }}
            >
              {w.icon} + {w.label}
            </button>
          ))}
        </div>
      )}

      {isEditMode && (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 8,
            marginBottom: 16,
            background: "rgba(255,255,255,0.12)",
            border: "1px solid rgba(255,255,255,0.3)",
            borderRadius: 10,
            padding: "8px 16px",
            color: "rgba(255,255,255,0.9)",
            fontSize: 13,
          }}
        >
          <span>✋</span>
          Drag panels by their header · resize from any edge · click ✕ to hide
        </div>
      )}

      {enabled.length === 0 ? (
        <div
          className="card"
          style={{ padding: 48, textAlign: "center", color: "#6b7280" }}
        >
          <p style={{ fontSize: 18, marginBottom: 8 }}>No panels visible</p>
          <p style={{ fontSize: 14 }}>
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
              <div
                key={w.id}
                style={{ display: "flex", flexDirection: "column" }}
              >
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
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        height: "100%",
        background: "#fff",
        borderRadius: 16,
        overflow: "hidden",
        boxShadow: isEditMode
          ? "0 0 0 2px #7c6bb5, 0 8px 32px rgba(0,0,0,0.14)"
          : "0 4px 24px rgba(0,0,0,0.10)",
        transition: "box-shadow 0.2s",
      }}
    >
      <div
        className={isEditMode ? "drag-handle" : undefined}
        style={{
          display: "flex",
          alignItems: "center",
          gap: 10,
          flexShrink: 0,
          padding: "11px 14px",
          background: "linear-gradient(135deg, #6b7fd4 0%, #8b5ea8 100%)",
          userSelect: "none",
          cursor: isEditMode ? "grab" : "default",
        }}
      >
        {isEditMode && (
          <span style={{ color: "rgba(255,255,255,0.55)", fontSize: 14 }}>
            ⠿
          </span>
        )}
        <span style={{ fontSize: 17 }}>{icon}</span>
        <span style={{ flex: 1, fontWeight: 700, fontSize: 14, color: "#fff" }}>
          {title}
        </span>
        {isEditMode && (
          <button
            onPointerDown={(e) => e.stopPropagation()}
            onClick={onClose}
            style={{
              background: "rgba(255,255,255,0.22)",
              border: "none",
              borderRadius: 6,
              width: 24,
              height: 24,
              cursor: "pointer",
              color: "#fff",
              fontSize: 14,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            ✕
          </button>
        )}
      </div>
      <div
        style={{
          flex: 1,
          overflowY: "auto",
          overflowX: "hidden",
          minHeight: 0,
        }}
      >
        {children}
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────── Repositories body ── */

function RepositoriesBody({ repos }: { repos: Repository[] }) {
  if (repos.length === 0) return <Empty text="No repositories found" />;
  return (
    <div>
      {repos.map((r) => (
        <div key={r.id} style={rowStyle}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 5 }}>
              {r.isFavorite && <span style={{ fontSize: 11 }}>⭐</span>}
              <span
                style={{
                  fontWeight: 600,
                  fontSize: 13,
                  color: "#111827",
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                  whiteSpace: "nowrap",
                }}
              >
                {r.name}
              </span>
            </div>
            {r.currentBranch && (
              <span
                style={{
                  display: "inline-block",
                  marginTop: 2,
                  fontSize: 11,
                  color: "#6366f1",
                  background: "#eef2ff",
                  borderRadius: 4,
                  padding: "1px 6px",
                }}
              >
                {r.currentBranch}
              </span>
            )}
          </div>
          <div style={{ display: "flex", gap: 5, flexShrink: 0 }}>
            {(r.aheadBy ?? 0) > 0 && (
              <Chip bg="#dcfce7" fg="#15803d">
                +{r.aheadBy}
              </Chip>
            )}
            {(r.behindBy ?? 0) > 0 && (
              <Chip bg="#fef3c7" fg="#d97706">
                -{r.behindBy}
              </Chip>
            )}
          </div>
        </div>
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
          <div key={pr.prId} style={rowStyle}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 5,
                  marginBottom: 2,
                }}
              >
                <Chip
                  bg={pr.isDraft ? "#f3f4f6" : "#ede9fe"}
                  fg={pr.isDraft ? "#6b7280" : "#7c3aed"}
                >
                  {pr.isDraft ? "DRAFT" : "OPEN"}
                </Chip>
                <span
                  style={{
                    fontSize: 13,
                    color: "#1f2937",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                  }}
                >
                  {pr.title}
                </span>
              </div>
              <span style={{ fontSize: 11, color: "#9ca3af" }}>
                {pr.repositoryName}
              </span>
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

/* ──────────────────────────────────────────────────────── Scripts body ── */

const STATUS_CHIP: Record<string, { bg: string; fg: string }> = {
  Running: { bg: "#dbeafe", fg: "#1d4ed8" },
  Success: { bg: "#dcfce7", fg: "#16a34a" },
  Failed: { bg: "#fee2e2", fg: "#dc2626" },
  Cancelled: { bg: "#f3f4f6", fg: "#6b7280" },
};

function ScriptsBody({
  scripts,
  executions,
}: {
  scripts: Script[];
  executions: Execution[];
}) {
  if (scripts.length === 0) return <Empty text="No scripts configured" />;
  return (
    <div>
      {scripts.map((s) => {
        const last = [...executions]
          .reverse()
          .find((e) => e.scriptDefinitionId === s.id);
        const chip = last
          ? (STATUS_CHIP[last.status] ?? STATUS_CHIP.Cancelled)
          : null;
        return (
          <div key={s.id} style={rowStyle}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontWeight: 600, fontSize: 13, color: "#111827" }}>
                {s.name}
              </div>
              {s.description && (
                <div style={{ fontSize: 11, color: "#9ca3af", marginTop: 1 }}>
                  {s.description}
                </div>
              )}
            </div>
            {chip && last && (
              <Chip bg={chip.bg} fg={chip.fg}>
                {last.status}
              </Chip>
            )}
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
          <div key={e.id} style={rowStyle}>
            <span
              style={{
                flex: 1,
                fontSize: 13,
                color: "#374151",
                overflow: "hidden",
                textOverflow: "ellipsis",
                whiteSpace: "nowrap",
              }}
            >
              {e.scriptName}
            </span>
            <Chip bg={s.bg} fg={s.fg}>
              {e.status}
            </Chip>
          </div>
        );
      })}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────── shared ── */

const rowStyle: React.CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  gap: 8,
  padding: "8px 14px",
  borderBottom: "1px solid #f3f4f6",
};

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
    <span
      style={{
        fontSize: 11,
        fontWeight: 700,
        padding: "2px 7px",
        borderRadius: 5,
        background: bg,
        color: fg,
        whiteSpace: "nowrap",
        flexShrink: 0,
      }}
    >
      {children}
    </span>
  );
}

function Empty({ text }: { text: string }) {
  return (
    <p
      style={{
        color: "#9ca3af",
        fontSize: 13,
        textAlign: "center",
        padding: "24px 16px",
        margin: 0,
      }}
    >
      {text}
    </p>
  );
}

/* ───────────────────────────────────────────────────────── button styles ── */

const ghostBtn: React.CSSProperties = {
  display: "flex",
  alignItems: "center",
  gap: 6,
  background: "rgba(255,255,255,0.15)",
  backdropFilter: "blur(8px)",
  border: "1px solid rgba(255,255,255,0.3)",
  borderRadius: 10,
  padding: "8px 16px",
  color: "#fff",
  fontWeight: 500,
  fontSize: 13,
  cursor: "pointer",
};

const editBtn: React.CSSProperties = {
  display: "flex",
  alignItems: "center",
  gap: 6,
  background: "rgba(255,255,255,0.92)",
  border: "1px solid rgba(255,255,255,0.4)",
  borderRadius: 10,
  padding: "8px 18px",
  color: "#7c3aed",
  fontWeight: 700,
  fontSize: 13,
  cursor: "pointer",
};

const editDoneBtn: React.CSSProperties = {
  ...editBtn,
  background: "linear-gradient(135deg, #6b7fd4, #8b5ea8)",
  color: "#fff",
  border: "1px solid transparent",
};
