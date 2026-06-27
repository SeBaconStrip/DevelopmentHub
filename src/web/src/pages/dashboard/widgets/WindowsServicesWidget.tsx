import type { WindowsServiceInfo } from "../../../types";
import { Empty } from "./shared";
import "./WindowsServicesWidget.css";

function StatusDot({ status }: { status: string }) {
  const cls =
    status === "Running"
      ? "wsw-dot--running"
      : status === "Stopped"
        ? "wsw-dot--stopped"
        : "wsw-dot--pending";
  return <span className={`wsw-dot ${cls}`} title={status} />;
}

export function WindowsServicesWidget({
  services,
  pendingService,
  error,
  onClearError,
  onStart,
  onStop,
  onRestart,
}: {
  services: WindowsServiceInfo[];
  pendingService: string | null;
  error: string | null;
  onClearError: () => void;
  onStart: (name: string) => void;
  onStop: (name: string) => void;
  onRestart: (name: string) => void;
}) {
  if (services.length === 0) {
    return <Empty text="No services configured. Open Windows Services page to add patterns." />;
  }

  return (
    <div className="wsw-root">
      {error && (
        <div className="wsw-error">
          <span className="wsw-error-text">{error}</span>
          <button className="wsw-error-dismiss" onClick={onClearError}>✕</button>
        </div>
      )}
      <div className="wsw-list">
        {services.map((svc) => {
          const busy = pendingService === svc.name;
          return (
            <div key={svc.name} className="wsw-item">
              <StatusDot status={svc.status} />
              <div className="wsw-info">
                <span className="wsw-display-name">{svc.displayName}</span>
                <span className="wsw-service-name">{svc.name}</span>
              </div>
              <div className="wsw-btns">
                <button
                  className="wsw-btn"
                  onClick={() => onStart(svc.name)}
                  disabled={busy || !svc.canStart}
                  title="Start"
                >
                  ▶
                </button>
                <button
                  className="wsw-btn"
                  onClick={() => onStop(svc.name)}
                  disabled={busy || !svc.canStop}
                  title="Stop"
                >
                  ■
                </button>
                <button
                  className="wsw-btn"
                  onClick={() => onRestart(svc.name)}
                  disabled={busy}
                  title="Restart"
                >
                  {busy ? <span className="wsw-spinner" /> : "↺"}
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
