import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { windowsServicesApi } from "../../api/windowsServices";
import { configApi } from "../../api/config";
import type { WindowsServiceInfo } from "../../types";
import "./WindowsServicesPage.css";

function StatusBadge({ status }: { status: string }) {
  const cls =
    status === "Running"
      ? "svc-status--running"
      : status === "Stopped"
        ? "svc-status--stopped"
        : "svc-status--pending";
  return <span className={`svc-status ${cls}`}>{status}</span>;
}

export default function WindowsServicesPage() {
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState<string | null>(null);
  const [pendingService, setPendingService] = useState<string | null>(null);

  const { data: config } = useQuery({ queryKey: ["config"], queryFn: configApi.get });
  const patterns = config?.windowsServicePatterns ?? [];

  const { data: services = [], isLoading, isFetching } = useQuery<WindowsServiceInfo[]>({
    queryKey: ["windows-services"],
    queryFn: windowsServicesApi.getStatuses,
    refetchInterval: 10_000,
    enabled: patterns.length > 0,
  });

  async function runAction(name: string, action: "start" | "stop" | "restart") {
    setActionError(null);
    setPendingService(name);
    try {
      if (action === "start") await windowsServicesApi.start(name);
      else if (action === "stop") await windowsServicesApi.stop(name);
      else await windowsServicesApi.restart(name);
      queryClient.invalidateQueries({ queryKey: ["windows-services"] });
    } catch (err) {
      setActionError(err instanceof Error ? err.message : String(err));
    } finally {
      setPendingService(null);
    }
  }

  return (
    <div className="svc-page">
      <div className="svc-card">
        <div className="svc-services-header">
          <div>
            <h2 className="svc-section-title">Windows Services</h2>
            {patterns.length > 0 && (
              <p className="svc-section-desc">
                {patterns.length} pattern{patterns.length !== 1 ? "s" : ""} configured ·{" "}
                configure in <strong>⚙ Settings → Services</strong>
              </p>
            )}
          </div>
          <button
            className="btn-ghost svc-refresh-btn"
            onClick={() => queryClient.invalidateQueries({ queryKey: ["windows-services"] })}
            disabled={isFetching}
            title="Refresh"
          >
            {isFetching ? <span className="svc-spinner" /> : "↻"}
          </button>
        </div>

        {actionError && (
          <div className="svc-error">
            <span>{actionError}</span>
            <button className="svc-error-dismiss" onClick={() => setActionError(null)}>✕</button>
          </div>
        )}

        {patterns.length === 0 ? (
          <div className="svc-empty">
            No service patterns configured. Open <strong>⚙ Settings → Services</strong> to add service names or patterns.
          </div>
        ) : isLoading ? (
          <div className="svc-empty">Loading…</div>
        ) : services.length === 0 ? (
          <div className="svc-empty">No services matched the configured patterns.</div>
        ) : (
          <div className="svc-table">
            <div className="svc-table-head">
              <span>Display Name</span>
              <span>Name</span>
              <span>Status</span>
              <span>Actions</span>
            </div>
            {services.map((svc) => {
              const busy = pendingService === svc.name;
              return (
                <div key={svc.name} className="svc-table-row">
                  <span className="svc-display-name">{svc.displayName}</span>
                  <span className="svc-name">{svc.name}</span>
                  <StatusBadge status={svc.status} />
                  <div className="svc-actions">
                    <button
                      className="btn-ghost svc-action-btn"
                      onClick={() => runAction(svc.name, "start")}
                      disabled={busy || !svc.canStart}
                      title="Start"
                    >
                      ▶
                    </button>
                    <button
                      className="btn-ghost svc-action-btn"
                      onClick={() => runAction(svc.name, "stop")}
                      disabled={busy || !svc.canStop}
                      title="Stop"
                    >
                      ■
                    </button>
                    <button
                      className="btn-ghost svc-action-btn"
                      onClick={() => runAction(svc.name, "restart")}
                      disabled={busy}
                      title="Restart"
                    >
                      {busy ? <span className="svc-spinner" /> : "↺"}
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
