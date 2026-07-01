import type { WindowsServiceInfo } from "../../../types";
import { Empty } from "./shared";
import { ErrorBar } from "../../../components/ErrorBar";
import "./WindowsServicesWidget.css";

function StatusBadge({ status }: { status: string }) {
  const cls =
    status === "Running"
      ? "wsw-badge--running"
      : status === "Stopped"
        ? "wsw-badge--stopped"
        : "wsw-badge--pending";
  return <span className={`wsw-badge ${cls}`}>{status}</span>;
}

export function WindowsServicesWidget({
  services,
  pendingService,
  error,
  onClearError,
  onStart,
  onStop,
  onRestart,
  onGrant,
}: {
  services: WindowsServiceInfo[];
  pendingService: string | null;
  error: string | null;
  onClearError: () => void;
  onStart: (name: string) => void;
  onStop: (name: string) => void;
  onRestart: (name: string) => void;
  onGrant: (name: string) => void;
}) {
  if (services.length === 0) {
    return <Empty text="No services configured. Open Windows Services page to add patterns." />;
  }

  return (
    <div className="wsw-root">
      <ErrorBar message={error} onDismiss={onClearError} />
      <div className="wsw-list">
        {services.map((svc) => {
          const busy = pendingService === svc.name;
          return (
            <div key={svc.name} className="workflow-card wsw-card">
              <div className="workflow-copy">
                <div className="workflow-title-row">
                  <span className="item-name">{svc.displayName}</span>
                  <StatusBadge status={svc.status} />
                </div>
                <span className="item-meta">{svc.name}</span>
              </div>
              <div className="wsw-btns">
                {svc.needsElevation && (
                  <button
                    className="btn-ghost wsw-btn wsw-btn--grant"
                    onClick={() => onGrant(svc.name)}
                    disabled={busy}
                    title="Grant start/stop permissions (one-time admin prompt)"
                  >{busy ? <span className="wsw-spinner" /> : "🔑"}</button>
                )}
                <button
                  className="btn-ghost wsw-btn"
                  onClick={() => onStart(svc.name)}
                  disabled={busy || !svc.canStart}
                  title="Start"
                >▶</button>
                <button
                  className="btn-ghost wsw-btn"
                  onClick={() => onStop(svc.name)}
                  disabled={busy || !svc.canStop}
                  title="Stop"
                >■</button>
                <button
                  className="btn-ghost wsw-btn"
                  onClick={() => onRestart(svc.name)}
                  disabled={busy}
                  title="Restart"
                >{busy ? <span className="wsw-spinner" /> : "↺"}</button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
