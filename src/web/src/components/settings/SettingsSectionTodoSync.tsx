import { useState, useEffect, useCallback, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import type { AppConfig } from "../../types";
import { todoSyncApi, type MicrosoftTodoList } from "../../api/todoSync";
import { Section, Field } from "./SettingsHelpers";

interface Props {
  form: AppConfig;
  setTodoSyncField: (key: string, value: string) => void;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
}

type AuthPhase = "idle" | "polling" | "done" | "error";

export function SettingsSectionTodoSync({ form, setTodoSyncField, setField }: Props) {
  const queryClient = useQueryClient();
  const msSettings = form.todoSyncProviders?.microsoftTodo ?? {};
  const isConnected = msSettings.connected === "true";
  const clientId = msSettings.clientId ?? "";
  const listId = msSettings.listId ?? "";
  const listName = msSettings.listName ?? "";
  const enabled = msSettings.enabled === "true";

  const [authPhase, setAuthPhase] = useState<AuthPhase>("idle");
  const [userCode, setUserCode] = useState("");
  const [verificationUri, setVerificationUri] = useState("");
  const [authError, setAuthError] = useState("");
  const [lists, setLists] = useState<MicrosoftTodoList[]>([]);
  const [listsLoading, setListsLoading] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const pollIntervalRef = useRef<number | null>(null);

  const loadLists = useCallback(async () => {
    setListsLoading(true);
    try {
      const fetched = await todoSyncApi.getMicrosoftLists();
      setLists(fetched);
    } catch {
      // silently fail — user can retry
    } finally {
      setListsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (isConnected) {
      loadLists();
    }
  }, [isConnected, loadLists]);

  useEffect(() => {
    return () => {
      if (pollIntervalRef.current !== null) {
        clearInterval(pollIntervalRef.current);
      }
    };
  }, []);

  async function handleConnect() {
    if (!clientId.trim()) {
      setAuthError("Please enter a Client ID first.");
      return;
    }
    setAuthError("");
    setAuthPhase("polling");
    try {
      const resp = await todoSyncApi.startMicrosoftAuth(clientId.trim());
      setUserCode(resp.userCode);
      setVerificationUri(resp.verificationUri);

      const { sessionId, interval, expiresIn } = resp;

      const stopPolling = () => {
        if (pollIntervalRef.current !== null) {
          clearInterval(pollIntervalRef.current);
          pollIntervalRef.current = null;
        }
      };

      // Auto-cancel when the device code expires
      const expiryTimer = window.setTimeout(() => {
        stopPolling();
        setAuthPhase("error");
        setAuthError("Authentication expired. Please try again.");
      }, expiresIn * 1000);

      pollIntervalRef.current = window.setInterval(async () => {
        try {
          const poll = await todoSyncApi.pollMicrosoftAuth(sessionId);
          if (poll.status === "succeeded") {
            stopPolling();
            clearTimeout(expiryTimer);
            setAuthPhase("done");
            await queryClient.invalidateQueries({ queryKey: ["config"] });
          } else if (poll.status === "failed" || poll.status === "expired") {
            stopPolling();
            clearTimeout(expiryTimer);
            setAuthPhase("error");
            setAuthError(poll.error ?? `Authentication ${poll.status}.`);
          }
        } catch {
          stopPolling();
          clearTimeout(expiryTimer);
          setAuthPhase("error");
          setAuthError("Failed to poll authentication status.");
        }
      }, interval * 1000);
    } catch (err: unknown) {
      setAuthPhase("error");
      setAuthError(
        err instanceof Error ? err.message : "Failed to start authentication.",
      );
    }
  }

  async function handleDisconnect() {
    await todoSyncApi.disconnectMicrosoft();
    setAuthPhase("idle");
    setLists([]);
    await queryClient.invalidateQueries({ queryKey: ["config"] });
  }

  async function handleManualSync() {
    setSyncing(true);
    try {
      await todoSyncApi.triggerSync();
    } finally {
      setSyncing(false);
    }
  }

  return (
    <>
      <Section title="Microsoft To Do">
        <p className="settings-page-hint" style={{ marginBottom: 12 }}>
          Sync your todos bidirectionally with a Microsoft To Do list. Requires
          an Azure AD app registration with the{" "}
          <strong>Tasks.ReadWrite</strong> delegated permission.
        </p>

        <div className="todo-sync-status-row">
          <span
            className={`todo-sync-badge ${isConnected ? "todo-sync-badge--connected" : "todo-sync-badge--disconnected"}`}
          >
            {isConnected ? "● Connected" : "○ Not connected"}
          </span>
          {isConnected && (
            <label className="todo-sync-enable-label">
              <input
                type="checkbox"
                checked={enabled}
                onChange={(e) =>
                  setTodoSyncField("enabled", e.target.checked ? "true" : "false")
                }
              />
              <span>Auto-sync enabled</span>
            </label>
          )}
        </div>

        <Field
          label="Azure AD Client ID"
          tooltip={
            <>
              <p>
                The Application (client) ID from your Azure AD app registration.
              </p>
              <p>
                <strong>Required permission:</strong> Tasks.ReadWrite
                (Microsoft Graph, delegated).
              </p>
              <p>
                Add{" "}
                <a
                  href="https://login.microsoftonline.com/common/oauth2/nativeclient"
                  target="_blank"
                  rel="noreferrer"
                  className="todo-sync-link"
                  style={{ pointerEvents: "auto" }}
                >
                  https://login.microsoftonline.com/common/oauth2/nativeclient
                </a>{" "}
                as a redirect URI under Mobile and desktop applications.
              </p>
            </>
          }
        >
          <input
            type="text"
            className="settings-input"
            value={clientId}
            onChange={(e) => setTodoSyncField("clientId", e.target.value)}
            placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
            disabled={isConnected || authPhase === "polling"}
          />
        </Field>

        {!isConnected && authPhase !== "polling" && (
          <button
            className="btn-primary"
            onClick={handleConnect}
            disabled={!clientId.trim()}
          >
            Connect Microsoft To Do
          </button>
        )}

        {authPhase === "polling" && (
          <div className="todo-sync-auth-box">
            <p className="todo-sync-auth-instruction">
              Visit{" "}
              <a
                href={verificationUri}
                target="_blank"
                rel="noreferrer"
                className="todo-sync-link"
              >
                {verificationUri}
              </a>{" "}
              and enter this code:
            </p>
            <div className="todo-sync-user-code">{userCode}</div>
            <p className="todo-sync-auth-waiting">
              Waiting for authentication…
            </p>
          </div>
        )}

        {authPhase === "error" && (
          <p className="settings-save-err" style={{ marginTop: 8 }}>
            {authError}
          </p>
        )}

        {isConnected && (
          <>
            <Field label="Sync with list">
              {listsLoading ? (
                <span className="settings-page-hint">Loading lists…</span>
              ) : (
                <select
                  className="settings-input"
                  value={listId}
                  onChange={(e) => {
                    const selected = lists.find((l) => l.id === e.target.value);
                    setTodoSyncField("listId", e.target.value);
                    setTodoSyncField("listName", selected?.displayName ?? "");
                  }}
                >
                  <option value="">— select a list —</option>
                  {lists.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.displayName}
                    </option>
                  ))}
                </select>
              )}
              {listName && !listsLoading && (
                <span className="settings-field-hint">
                  Currently syncing: {listName}
                </span>
              )}
            </Field>

            <div className="todo-sync-actions">
              <button
                className="btn-secondary"
                onClick={handleManualSync}
                disabled={syncing}
              >
                {syncing ? "Syncing…" : "↻ Sync Now"}
              </button>
              <button
                className="btn-remove btn-remove--small"
                onClick={handleDisconnect}
              >
                Disconnect
              </button>
            </div>
          </>
        )}
      </Section>

      <Section title="Sync Interval">
        <Field label="Background sync interval (seconds)">
          <input
            type="number"
            className="settings-input settings-input--narrow"
            min={30}
            value={form.todoSyncIntervalSeconds ?? 300}
            onChange={(e) =>
              setField("todoSyncIntervalSeconds", Number(e.target.value))
            }
          />
          <span className="settings-field-hint">
            How often to sync todos automatically. Minimum 30 seconds.
          </span>
        </Field>
      </Section>
    </>
  );
}
