import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { scriptsApi } from "../../api/scripts";
import type { Script, Execution } from "../../types";

const statusColor: Record<string, string> = {
  Running: "#f9c74f",
  Success: "#43c59e",
  Failed: "#f05252",
  Cancelled: "#888",
};

export default function ScriptsPage() {
  const qc = useQueryClient();

  const { data: scripts = [], isLoading: loadingScripts } = useQuery<Script[]>({
    queryKey: ["scripts"],
    queryFn: scriptsApi.getAll,
  });

  const { data: executions = [] } = useQuery<Execution[]>({
    queryKey: ["executions"],
    queryFn: () => scriptsApi.getHistory(20),
    refetchInterval: 3_000,
  });

  const execute = useMutation({
    mutationFn: (scriptId: string) => scriptsApi.execute(scriptId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["executions"] }),
  });

  const cancel = useMutation({
    mutationFn: (executionId: string) => scriptsApi.cancel(executionId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["executions"] }),
  });

  if (loadingScripts) return <p style={{ padding: 32 }}>Loading scripts…</p>;

  const runningIds = new Set(
    executions
      .filter((e) => e.status === "Running")
      .map((e) => e.scriptDefinitionId),
  );

  return (
    <div style={{ padding: 32 }}>
      <h1 style={{ margin: "0 0 24px", fontSize: 24, fontWeight: 700 }}>
        Scripts
      </h1>

      {scripts.length === 0 ? (
        <p style={{ color: "#888" }}>
          No scripts configured. Add them in Settings.
        </p>
      ) : (
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: 12,
            marginBottom: 40,
          }}
        >
          {scripts.map((script) => {
            const isRunning = runningIds.has(script.id);
            const runningExec = executions.find(
              (e) =>
                e.scriptDefinitionId === script.id && e.status === "Running",
            );
            return (
              <div
                key={script.id}
                style={{
                  background: "rgba(255,255,255,0.06)",
                  borderRadius: 10,
                  padding: "14px 18px",
                  display: "flex",
                  alignItems: "center",
                  gap: 16,
                }}
              >
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 600, fontSize: 15 }}>
                    {script.name}
                  </div>
                  {script.description && (
                    <div style={{ fontSize: 12, color: "#888", marginTop: 2 }}>
                      {script.description}
                    </div>
                  )}
                  <div
                    style={{
                      fontSize: 11,
                      color: "#555",
                      marginTop: 3,
                      fontFamily: "monospace",
                    }}
                  >
                    {script.command} {script.arguments.join(" ")}
                  </div>
                </div>
                {isRunning && runningExec ? (
                  <button
                    onClick={() => cancel.mutate(runningExec.id)}
                    disabled={cancel.isPending}
                    style={{
                      padding: "5px 14px",
                      borderRadius: 6,
                      border: "none",
                      background: "#f05252",
                      color: "#fff",
                      cursor: "pointer",
                      fontWeight: 600,
                      fontSize: 13,
                    }}
                  >
                    Cancel
                  </button>
                ) : (
                  <button
                    onClick={() => execute.mutate(script.id)}
                    disabled={execute.isPending}
                    style={{
                      padding: "5px 14px",
                      borderRadius: 6,
                      border: "none",
                      background: "#43c59e",
                      color: "#fff",
                      cursor: "pointer",
                      fontWeight: 600,
                      fontSize: 13,
                    }}
                  >
                    Run
                  </button>
                )}
              </div>
            );
          })}
        </div>
      )}

      {executions.length > 0 && (
        <>
          <h2 style={{ margin: "0 0 14px", fontSize: 18, fontWeight: 600 }}>
            Recent Executions
          </h2>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {executions.map((exec) => (
              <div
                key={exec.id}
                style={{
                  background: "rgba(255,255,255,0.04)",
                  borderRadius: 8,
                  padding: "10px 16px",
                  display: "flex",
                  alignItems: "center",
                  gap: 14,
                }}
              >
                <span
                  style={{
                    width: 10,
                    height: 10,
                    borderRadius: "50%",
                    background: statusColor[exec.status] ?? "#888",
                    flexShrink: 0,
                    boxShadow:
                      exec.status === "Running"
                        ? `0 0 6px ${statusColor.Running}`
                        : "none",
                  }}
                />
                <span style={{ fontWeight: 500, flex: 1, fontSize: 14 }}>
                  {exec.scriptName}
                </span>
                <span style={{ fontSize: 12, color: "#888" }}>
                  {new Date(exec.startedAt).toLocaleString()}
                </span>
                {exec.exitCode !== null && (
                  <span
                    style={{
                      fontSize: 12,
                      color: exec.exitCode === 0 ? "#43c59e" : "#f05252",
                    }}
                  >
                    exit {exec.exitCode}
                  </span>
                )}
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
