import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { windowsServicesApi } from "../../api/windowsServices";
import { configApi } from "../../api/config";
import { ErrorBar } from "../../components/ErrorBar";
import { FilterToolbar } from "../../components/FilterToolbar";
import type { WindowsServiceInfo } from "../../types";
import "./WindowsServicesPage.css";

type StatusFilter = "all" | "running" | "stopped" | "other";

function StatusBadge({ status }: { status: string }) {
  const cls =
    status === "Running"
      ? "svc-badge--running"
      : status === "Stopped"
        ? "svc-badge--stopped"
        : "svc-badge--pending";
  return <span className={`svc-badge ${cls}`}>{status}</span>;
}

export default function WindowsServicesPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
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

  const runningCount = services.filter((s) => s.status === "Running").length;
  const stoppedCount = services.filter((s) => s.status === "Stopped").length;
  const otherCount  = services.filter((s) => s.status !== "Running" && s.status !== "Stopped").length;

  const statusFilters = [
    { value: "all",     label: `All (${services.length})` },
    { value: "running", label: `Running${runningCount > 0 ? ` (${runningCount})` : ""}` },
    { value: "stopped", label: `Stopped${stoppedCount > 0 ? ` (${stoppedCount})` : ""}` },
    ...(otherCount > 0 ? [{ value: "other", label: `Other (${otherCount})` }] : []),
  ];

  const filtered = services
    .filter((s) => {
      if (statusFilter === "running") return s.status === "Running";
      if (statusFilter === "stopped") return s.status === "Stopped";
      if (statusFilter === "other") return s.status !== "Running" && s.status !== "Stopped";
      return true;
    })
    .filter((s) =>
      !search ||
      s.displayName.toLowerCase().includes(search.toLowerCase()) ||
      s.name.toLowerCase().includes(search.toLowerCase()),
    );

  return (
    <div className="svc-page">
      <div className="svc-card">
        <FilterToolbar
          search={search}
          onSearchChange={setSearch}
          filter={statusFilter}
          onFilterChange={(f) => setStatusFilter(f as StatusFilter)}
          filters={statusFilters}
          searchPlaceholder="Search by name…"
          shownCount={filtered.length}
          totalCount={services.length}
        >
          <button
            className="btn-ghost"
            onClick={() => queryClient.invalidateQueries({ queryKey: ["windows-services"] })}
            disabled={isFetching}
            title="Refresh"
          >
            {isFetching ? <span className="svc-spinner" /> : "↻ Refresh"}
          </button>
        </FilterToolbar>

        <ErrorBar message={actionError} onDismiss={() => setActionError(null)} />
      </div>

      {patterns.length === 0 ? (
        <p className="svc-empty">
          No service patterns configured. Open <strong>⚙ Settings → Services</strong> to add service names or patterns.
        </p>
      ) : isLoading ? (
        <p className="svc-empty">Loading…</p>
      ) : filtered.length === 0 ? (
        <p className="svc-empty">No services match.</p>
      ) : (
        <div className="svc-table">
          <div className="svc-th svc-col-display">Display Name</div>
          <div className="svc-th svc-col-name">Service Name</div>
          <div className="svc-th svc-col-status">Status</div>
          <div className="svc-th svc-col-actions">Actions</div>

          {filtered.map((svc) => {
            const busy = pendingService === svc.name;
            return (
              <>
                <div key={`${svc.name}-display`} className="svc-td svc-col-display">
                  <span className="item-name">{svc.displayName}</span>
                </div>
                <div key={`${svc.name}-svcname`} className="svc-td svc-col-name">
                  <span className="svc-service-name">{svc.name}</span>
                </div>
                <div key={`${svc.name}-status`} className="svc-td svc-col-status">
                  <StatusBadge status={svc.status} />
                </div>
                <div key={`${svc.name}-actions`} className="svc-td svc-col-actions">
                  <button
                    className="btn-ghost svc-action-btn"
                    onClick={() => runAction(svc.name, "start")}
                    disabled={busy || !svc.canStart}
                    title="Start"
                  >▶</button>
                  <button
                    className="btn-ghost svc-action-btn"
                    onClick={() => runAction(svc.name, "stop")}
                    disabled={busy || !svc.canStop}
                    title="Stop"
                  >■</button>
                  <button
                    className="btn-ghost svc-action-btn"
                    onClick={() => runAction(svc.name, "restart")}
                    disabled={busy}
                    title="Restart"
                  >{busy ? <span className="svc-spinner" /> : "↺"}</button>
                </div>
              </>
            );
          })}
        </div>
      )}
    </div>
  );
}
