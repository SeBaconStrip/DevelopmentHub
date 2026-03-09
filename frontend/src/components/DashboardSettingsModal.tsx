import { useState, useEffect } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { useUiStore, type DashboardWidget } from "../store/uiStore";
import { configApi } from "../api/config";
import type { AppConfig } from "../types";
import "./DashboardSettingsModal.css";

type Tab = "dashboard" | "settings";

interface Props {
  onClose: () => void;
}

export function DashboardSettingsModal({ onClose }: Props) {
  const [tab, setTab] = useState<Tab>("dashboard");

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-card settings-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="settings-modal-header">
          <div className="settings-modal-title-row">
            <div>
              <h2 className="settings-modal-title">Settings</h2>
              <p className="settings-modal-sub">
                Manage panels and application configuration
              </p>
            </div>
            <button className="settings-modal-close" onClick={onClose}>
              ×
            </button>
          </div>
          <div className="settings-tabs">
            {(["dashboard", "settings"] as Tab[]).map((t) => (
              <button
                key={t}
                className={`settings-tab${tab === t ? " settings-tab--active" : ""}`}
                onClick={() => setTab(t)}
              >
                {t === "dashboard" ? "📦 Panels" : "⚙ App Settings"}
              </button>
            ))}
          </div>
        </div>
        <div className="settings-modal-body">
          {tab === "dashboard" ? (
            <DashboardTab onClose={onClose} />
          ) : (
            <SettingsTab onClose={onClose} />
          )}
        </div>
      </div>
    </div>
  );
}

/* ────────────────────────────────────────────── Dashboard / Panels tab ── */

function DashboardTab({ onClose }: { onClose: () => void }) {
  const { dashboardWidgets, toggleWidget } = useUiStore();
  return (
    <>
      <p className="dash-tab-hint">
        Toggle panels on or off. Use <strong>✎ Edit Layout</strong> on the
        dashboard to reposition them freely.
      </p>
      <div className="widget-list">
        {dashboardWidgets.map((widget) => (
          <WidgetRow
            key={widget.id}
            widget={widget}
            onToggle={() => toggleWidget(widget.id)}
          />
        ))}
      </div>
      <div className="dash-tab-footer">
        <button className="btn-primary" onClick={onClose}>
          Done
        </button>
      </div>
    </>
  );
}

interface RowProps {
  widget: DashboardWidget;
  onToggle: () => void;
}

function WidgetRow({ widget, onToggle }: RowProps) {
  return (
    <div
      className={`widget-row${widget.enabled ? " widget-row--enabled" : ""}`}
    >
      <span className="widget-row-icon">{widget.icon}</span>
      <span className="widget-row-label">{widget.label}</span>
      <button
        className={`toggle-btn${widget.enabled ? " toggle-btn--on" : ""}`}
        onClick={onToggle}
        title={widget.enabled ? "Disable" : "Enable"}
      >
        <span
          className={`toggle-knob${widget.enabled ? " toggle-knob--on" : ""}`}
        />
      </button>
    </div>
  );
}

/* ──────────────────────────────────────────────────── App Settings tab ── */

function SettingsTab({ onClose }: { onClose: () => void }) {
  const { data, isLoading } = useQuery({
    queryKey: ["config"],
    queryFn: configApi.get,
  });
  const save = useMutation({ mutationFn: configApi.save });
  const [form, setForm] = useState<AppConfig | null>(null);
  const [isCapturingHotkey, setIsCapturingHotkey] = useState(false);

  useEffect(() => {
    if (data) setForm(JSON.parse(JSON.stringify(data)));
  }, [data]);

  if (isLoading || !form) {
    return <p className="empty-msg">Loading configuration…</p>;
  }

  const setField = <K extends keyof AppConfig>(key: K, value: AppConfig[K]) =>
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev));

  const setAzDO = <K extends keyof AppConfig["azureDevOps"]>(
    key: K,
    value: string,
  ) =>
    setForm((prev) =>
      prev
        ? { ...prev, azureDevOps: { ...prev.azureDevOps, [key]: value } }
        : prev,
    );

  const addRoot = () =>
    setField("repositoryRoots", [...form.repositoryRoots, ""]);
  const removeRoot = (i: number) =>
    setField(
      "repositoryRoots",
      form.repositoryRoots.filter((_, idx) => idx !== i),
    );
  const updateRoot = (i: number, val: string) =>
    setField(
      "repositoryRoots",
      form.repositoryRoots.map((r, idx) => (idx === i ? val : r)),
    );

  const browseRoot = async (i: number) => {
    const path = await configApi.pickFolder();
    if (path) updateRoot(i, path);
  };

  const handleHotkeyCapture = (e: React.KeyboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const ignored = [
      "Control",
      "Shift",
      "Alt",
      "Meta",
      "CapsLock",
      "Tab",
      "Escape",
    ];
    if (ignored.includes(e.key)) return;
    const mods: string[] = [];
    if (e.ctrlKey) mods.push("Ctrl");
    if (e.shiftKey) mods.push("Shift");
    if (e.altKey) mods.push("Alt");
    const key = e.key.length === 1 ? e.key.toUpperCase() : e.key;
    mods.push(key);
    setField("hotkeyBinding", mods.join("+"));
    setIsCapturingHotkey(false);
  };

  return (
    <>
      <Section title="Repository Root Directories">
        {form.repositoryRoots.map((root, i) => (
          <div key={i} className="settings-root-row">
            <input
              className="settings-input"
              value={root}
              onChange={(e) => updateRoot(i, e.target.value)}
            />
            <button
              type="button"
              className="btn-ghost"
              title="Browse…"
              onClick={() => browseRoot(i)}
            >
              📁
            </button>
            <button className="btn-remove" onClick={() => removeRoot(i)}>
              ✕
            </button>
          </div>
        ))}
        <AddLink onClick={addRoot}>+ Add directory</AddLink>
      </Section>

      <Section title="Azure DevOps">
        {(
          [
            ["Organization", "organization", "myorg"],
            ["Project", "project", "MyProject"],
            ["User Email", "userEmail", "you@example.com"],
          ] as const
        ).map(([label, key, placeholder]) => (
          <Field key={key} label={label}>
            <input
              className="settings-input"
              value={form.azureDevOps[key]}
              onChange={(e) => setAzDO(key, e.target.value)}
              placeholder={placeholder}
            />
          </Field>
        ))}
        <Field label="Personal Access Token">
          <input
            type="password"
            className="settings-input"
            value={form.azureDevOps.pat}
            onChange={(e) => setAzDO("pat", e.target.value)}
            placeholder="Leave blank to keep existing"
          />
          <span className="settings-field-hint">Required scope: vso.code</span>
        </Field>
      </Section>

      <Section title="Scan Settings">
        <div className="settings-scan-row">
          <Field label="Scan interval (minutes)">
            <input
              type="number"
              className="settings-input settings-input--narrow"
              value={form.scanIntervalMinutes}
              onChange={(e) =>
                setField("scanIntervalMinutes", Number(e.target.value))
              }
            />
          </Field>
          <Field label="Repository scan depth">
            <input
              type="number"
              className="settings-input settings-input--narrow"
              value={form.repoScanDepth}
              onChange={(e) =>
                setField("repoScanDepth", Number(e.target.value))
              }
            />
          </Field>
          <Field label="Entry point scan depth">
            <input
              type="number"
              className="settings-input settings-input--narrow"
              value={form.entryPointScanDepth}
              onChange={(e) =>
                setField("entryPointScanDepth", Number(e.target.value))
              }
            />
          </Field>
        </div>
      </Section>

      <Section title="Hotkey">
        <Field label="Open window shortcut">
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <input
              readOnly
              className={`settings-input${isCapturingHotkey ? " settings-input--focus" : ""}`}
              style={{ width: 160, cursor: "pointer" }}
              value={
                isCapturingHotkey
                  ? "Press keys…"
                  : form.hotkeyBinding || "Ctrl+Shift+D"
              }
              onKeyDown={isCapturingHotkey ? handleHotkeyCapture : undefined}
              onBlur={() => setIsCapturingHotkey(false)}
              onClick={() => setIsCapturingHotkey(true)}
            />
            <button
              type="button"
              className={isCapturingHotkey ? "btn-primary" : "btn-ghost"}
              onClick={() => setIsCapturingHotkey((v) => !v)}
            >
              {isCapturingHotkey ? "Cancel" : "Record"}
            </button>
          </div>
          <span className="settings-field-hint">
            Click the field or "Record", then press your combination.
          </span>
        </Field>
      </Section>

      <div className="settings-save-bar">
        <button
          className="btn-primary"
          onClick={() => save.mutate(form)}
          disabled={save.isPending}
        >
          {save.isPending ? "Saving…" : "💾 Save Configuration"}
        </button>
        {save.isSuccess && <span className="settings-save-ok">✓ Saved</span>}
        {save.isError && <span className="settings-save-err">✗ Failed</span>}
        <button className="btn-close" onClick={onClose}>
          Close
        </button>
      </div>
    </>
  );
}

/* ──────────────────────────────────────────────────────────── helpers ── */

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="settings-section">
      <h3 className="settings-section-title">{title}</h3>
      {children}
    </div>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label className="settings-field">
      <span className="settings-field-label">{label}</span>
      {children}
    </label>
  );
}

function AddLink({
  onClick,
  children,
}: {
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button className="btn-add-link" onClick={onClick}>
      {children}
    </button>
  );
}
