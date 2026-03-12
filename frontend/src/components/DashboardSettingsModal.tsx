import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  useUiStore,
  type DashboardWidget,
  type ThemeId,
} from "../store/uiStore";
import { configApi } from "../api/config";
import { repositoriesApi } from "../api/repositories";
import type { AppConfig } from "../types";
import "./DashboardSettingsModal.css";

type NavPage = "general" | "repositories" | "pullRequests" | "appearance";

const NAV_ITEMS: { id: NavPage; icon: string; label: string }[] = [
  { id: "general", icon: "⚙", label: "General" },
  { id: "repositories", icon: "📁", label: "Repositories" },
  { id: "pullRequests", icon: "⎇", label: "Pull Requests" },
  { id: "appearance", icon: "🎨", label: "Appearance" },
];

interface Props {
  onClose: () => void;
}

export function DashboardSettingsModal({ onClose }: Props) {
  const [page, setPage] = useState<NavPage>("general");
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["config"],
    queryFn: configApi.get,
  });

  const scan = useMutation({
    mutationFn: repositoriesApi.scan,
    onSuccess: (data) => queryClient.setQueryData(["repositories"], data),
  });

  const save = useMutation({
    mutationFn: configApi.save,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["config"] });
      scan.mutate();
    },
  });

  const [form, setForm] = useState<AppConfig | null>(null);
  const [isCapturingHotkey, setIsCapturingHotkey] = useState(false);

  useEffect(() => {
    if (data) setForm(JSON.parse(JSON.stringify(data)));
  }, [data]);

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
    form && setField("repositoryRoots", [...form.repositoryRoots, ""]);

  const removeRoot = (i: number) =>
    form &&
    setField(
      "repositoryRoots",
      form.repositoryRoots.filter((_, idx) => idx !== i),
    );

  const updateRoot = (i: number, val: string) =>
    form &&
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

  const renderPage = () => {
    if (isLoading || !form) {
      return <p className="empty-msg">Loading configuration…</p>;
    }
    switch (page) {
      case "general":
        return (
          <GeneralPage
            form={form}
            isCapturingHotkey={isCapturingHotkey}
            setIsCapturingHotkey={setIsCapturingHotkey}
            handleHotkeyCapture={handleHotkeyCapture}
          />
        );
      case "repositories":
        return (
          <RepositoriesPage
            form={form}
            setField={setField}
            addRoot={addRoot}
            removeRoot={removeRoot}
            updateRoot={updateRoot}
            browseRoot={browseRoot}
          />
        );
      case "pullRequests":
        return (
          <PullRequestsPage form={form} setField={setField} setAzDO={setAzDO} />
        );
      case "appearance":
        return <AppearancePage />;
    }
  };

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
        </div>

        <div className="settings-modal-layout">
          <nav className="settings-sidebar">
            {NAV_ITEMS.map((item) => (
              <button
                key={item.id}
                className={`settings-nav-item${page === item.id ? " settings-nav-item--active" : ""}`}
                onClick={() => setPage(item.id)}
              >
                <span className="settings-nav-icon">{item.icon}</span>
                <span className="settings-nav-label">{item.label}</span>
              </button>
            ))}
          </nav>

          <div className="settings-modal-body">{renderPage()}</div>
        </div>

        <div className="settings-save-bar">
          <button
            className="btn-primary"
            onClick={() => form && save.mutate(form)}
            disabled={save.isPending || scan.isPending || !form}
          >
            {save.isPending
              ? "Saving…"
              : scan.isPending
                ? "Scanning…"
                : "💾 Save"}
          </button>
          {scan.isSuccess && (
            <span className="settings-save-ok">✓ Saved &amp; scanned</span>
          )}
          {(save.isError || scan.isError) && (
            <span className="settings-save-err">✗ Failed</span>
          )}
          <button className="btn-close" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────────── General page ── */

interface GeneralPageProps {
  form: AppConfig;
  isCapturingHotkey: boolean;
  setIsCapturingHotkey: (v: boolean | ((prev: boolean) => boolean)) => void;
  handleHotkeyCapture: (e: React.KeyboardEvent<HTMLInputElement>) => void;
}

function GeneralPage({
  form,
  isCapturingHotkey,
  setIsCapturingHotkey,
  handleHotkeyCapture,
}: GeneralPageProps) {
  return (
    <>
      <Section title="Hotkey">
        <Field label="Open window shortcut">
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <input
              readOnly
              className={`settings-input${isCapturingHotkey ? " settings-input--capturing" : ""}`}
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
    </>
  );
}

/* ───────────────────────────────────────────────── Repositories page ── */

interface RepositoriesPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
  addRoot: () => void;
  removeRoot: (i: number) => void;
  updateRoot: (i: number, val: string) => void;
  browseRoot: (i: number) => void;
}

function RepositoriesPage({
  form,
  setField,
  addRoot,
  removeRoot,
  updateRoot,
  browseRoot,
}: RepositoriesPageProps) {
  const { dashboardWidgets, toggleWidget } = useUiStore();
  const widget = dashboardWidgets.find((w) => w.id === "repositories");

  return (
    <>
      <Section title="Panel">
        {widget && (
          <WidgetRow
            widget={widget}
            onToggle={() => toggleWidget("repositories")}
          />
        )}
      </Section>

      <Section title="Root Directories">
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

      <Section title="Scan Settings">
        <div className="settings-scan-row">
          <Field label="Scan interval (min)">
            <input
              type="number"
              className="settings-input settings-input--narrow"
              value={form.scanIntervalMinutes}
              onChange={(e) =>
                setField("scanIntervalMinutes", Number(e.target.value))
              }
            />
          </Field>
          <Field label="Repo scan depth">
            <input
              type="number"
              className="settings-input settings-input--narrow"
              value={form.repoScanDepth}
              onChange={(e) =>
                setField("repoScanDepth", Number(e.target.value))
              }
            />
          </Field>
          <Field label="Entry point depth">
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
    </>
  );
}

/* ───────────────────────────────────────────────── Pull Requests page ── */

interface PullRequestsPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
  setAzDO: <K extends keyof AppConfig["azureDevOps"]>(
    key: K,
    value: string,
  ) => void;
}

function PullRequestsPage({ form, setField, setAzDO }: PullRequestsPageProps) {
  const { dashboardWidgets, toggleWidget } = useUiStore();
  const widget = dashboardWidgets.find((w) => w.id === "pullRequests");

  return (
    <>
      <Section title="Panel">
        {widget && (
          <WidgetRow
            widget={widget}
            onToggle={() => toggleWidget("pullRequests")}
          />
        )}
      </Section>

      <Section title="Refresh">
        <Field label="PR refresh interval (seconds)">
          <input
            type="number"
            className="settings-input settings-input--narrow"
            min={10}
            value={form.prRefreshIntervalSeconds}
            onChange={(e) =>
              setField("prRefreshIntervalSeconds", Number(e.target.value))
            }
          />
          <span className="settings-field-hint">
            How often to poll Azure DevOps for open pull requests.
          </span>
        </Field>
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
          <span className="settings-field-hint">
            Required scopes: Code (Read) · Profile (Read)
          </span>
        </Field>
      </Section>
    </>
  );
}

/* ───────────────────────────────────────────────────── Appearance page ── */

function AppearancePage() {
  const { theme, setTheme } = useUiStore();
  return (
    <>
      <Section title="Theme">
        <div className="theme-picker">
          {(
            [
              ["violet", "Violet"],
              ["dark", "Dark"],
              ["ocean", "Ocean"],
              ["orange", "Orange"],
              ["nature", "Nature"],
            ] as [ThemeId, string][]
          ).map(([id, label]) => (
            <button
              key={id}
              type="button"
              className={`theme-option${theme === id ? " theme-option--active" : ""}`}
              onClick={() => setTheme(id)}
            >
              <div className={`theme-swatch theme-swatch--${id}`} />
              {label}
            </button>
          ))}
        </div>
        <p className="settings-page-hint" style={{ marginTop: 12 }}>
          Theme changes are applied immediately.
        </p>
      </Section>
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
