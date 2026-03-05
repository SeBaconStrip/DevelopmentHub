import { useState, useEffect } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { useUiStore, type DashboardWidget } from "../store/uiStore";
import { configApi } from "../api/config";
import type { AppConfig, ScriptConfig } from "../types";

type Tab = "dashboard" | "settings";

interface Props {
  onClose: () => void;
}

export function DashboardSettingsModal({ onClose }: Props) {
  const [tab, setTab] = useState<Tab>("dashboard");

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-card"
        onClick={(e) => e.stopPropagation()}
        style={{ width: 740, maxWidth: "95vw", padding: 0 }}
      >
        {/* header */}
        <div style={{ padding: "24px 28px 0" }}>
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              marginBottom: 20,
            }}
          >
            <div>
              <h2
                style={{
                  fontSize: 20,
                  fontWeight: 700,
                  color: "#1e1b4b",
                  margin: 0,
                }}
              >
                Settings
              </h2>
              <p style={{ fontSize: 13, color: "#6b7280", margin: "3px 0 0" }}>
                Manage panels and application configuration
              </p>
            </div>
            <button
              onClick={onClose}
              style={{
                background: "#f3f4f6",
                border: "none",
                borderRadius: 8,
                width: 32,
                height: 32,
                cursor: "pointer",
                fontSize: 18,
                color: "#6b7280",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              ×
            </button>
          </div>

          {/* tab bar */}
          <div
            style={{
              display: "flex",
              gap: 4,
              borderBottom: "2px solid #f3f4f6",
            }}
          >
            {(["dashboard", "settings"] as Tab[]).map((t) => (
              <button
                key={t}
                onClick={() => setTab(t)}
                style={{
                  border: "none",
                  background: "none",
                  cursor: "pointer",
                  padding: "8px 18px",
                  fontSize: 14,
                  fontWeight: 600,
                  color: tab === t ? "#7c3aed" : "#6b7280",
                  borderBottom:
                    tab === t ? "2px solid #7c3aed" : "2px solid transparent",
                  marginBottom: -2,
                  borderRadius: 0,
                }}
              >
                {t === "dashboard" ? "📦 Panels" : "⚙ App Settings"}
              </button>
            ))}
          </div>
        </div>

        {/* body */}
        <div
          style={{
            padding: "20px 28px 28px",
            maxHeight: "70vh",
            overflowY: "auto",
          }}
        >
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
      <p style={{ fontSize: 13, color: "#6b7280", margin: "0 0 16px" }}>
        Toggle panels on or off. Use <strong>✎ Edit Layout</strong> on the
        dashboard to reposition them freely.
      </p>
      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {dashboardWidgets.map((widget) => (
          <WidgetRow
            key={widget.id}
            widget={widget}
            onToggle={() => toggleWidget(widget.id)}
          />
        ))}
      </div>
      <div
        style={{ marginTop: 24, display: "flex", justifyContent: "flex-end" }}
      >
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
      style={{
        display: "flex",
        alignItems: "center",
        gap: 14,
        padding: "12px 16px",
        borderRadius: 12,
        border: "2px solid #e5e7eb",
        background: widget.enabled ? "#fafbff" : "#fafafa",
      }}
    >
      <span style={{ fontSize: 20 }}>{widget.icon}</span>
      <span
        style={{ flex: 1, fontSize: 15, fontWeight: 500, color: "#1f2937" }}
      >
        {widget.label}
      </span>
      <button
        onClick={onToggle}
        title={widget.enabled ? "Disable" : "Enable"}
        style={{
          width: 44,
          height: 24,
          borderRadius: 12,
          border: "none",
          cursor: "pointer",
          background: widget.enabled
            ? "linear-gradient(135deg, #6b7fd4, #8b5ea8)"
            : "#d1d5db",
          position: "relative",
          transition: "background 0.2s",
          flexShrink: 0,
        }}
      >
        <span
          style={{
            position: "absolute",
            top: 2,
            left: widget.enabled ? 22 : 2,
            width: 20,
            height: 20,
            borderRadius: "50%",
            background: "#fff",
            transition: "left 0.2s",
            boxShadow: "0 1px 3px rgba(0,0,0,0.2)",
            display: "block",
          }}
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

  useEffect(() => {
    if (data) setForm(JSON.parse(JSON.stringify(data)));
  }, [data]);

  if (isLoading || !form) {
    return (
      <p
        style={{
          color: "#9ca3af",
          fontSize: 13,
          padding: "24px 0",
          textAlign: "center",
        }}
      >
        Loading configuration…
      </p>
    );
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

  const addScript = () =>
    setField("scripts", [
      ...form.scripts,
      {
        id: crypto.randomUUID(),
        name: "",
        description: "",
        workingDirectory: "",
        command: "",
        arguments: [],
        environmentVariables: {},
      } satisfies ScriptConfig,
    ]);
  const removeScript = (i: number) =>
    setField(
      "scripts",
      form.scripts.filter((_, idx) => idx !== i),
    );
  const updateScript = <K extends keyof ScriptConfig>(
    i: number,
    key: K,
    value: ScriptConfig[K],
  ) =>
    setField(
      "scripts",
      form.scripts.map((s, idx) => (idx === i ? { ...s, [key]: value } : s)),
    );

  return (
    <>
      {/* Repository Roots */}
      <Section title="Repository Root Directories">
        {form.repositoryRoots.map((root, i) => (
          <div key={i} style={{ display: "flex", gap: 8, marginBottom: 8 }}>
            <input
              value={root}
              onChange={(e) => updateRoot(i, e.target.value)}
              placeholder="C:\Projects"
              style={inputStyle}
            />
            <button onClick={() => removeRoot(i)} style={removeBtnStyle}>
              ✕
            </button>
          </div>
        ))}
        <AddLink onClick={addRoot}>+ Add directory</AddLink>
      </Section>

      {/* Azure DevOps */}
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
              value={form.azureDevOps[key]}
              onChange={(e) => setAzDO(key, e.target.value)}
              placeholder={placeholder}
              style={inputStyle}
            />
          </Field>
        ))}
        <Field label="Personal Access Token">
          <input
            type="password"
            value={form.azureDevOps.pat}
            onChange={(e) => setAzDO("pat", e.target.value)}
            placeholder="Leave blank to keep existing"
            style={inputStyle}
          />
          <span style={{ fontSize: 11, color: "#9ca3af", marginTop: 2 }}>
            Required scope: vso.code
          </span>
        </Field>
      </Section>

      {/* Scan Settings */}
      <Section title="Scan Settings">
        <div style={{ display: "flex", gap: 24 }}>
          <Field label="Scan interval (minutes)">
            <input
              type="number"
              value={form.scanIntervalMinutes}
              onChange={(e) =>
                setField("scanIntervalMinutes", Number(e.target.value))
              }
              style={{ ...inputStyle, width: 100 }}
            />
          </Field>
          <Field label="Entry point search depth">
            <input
              type="number"
              value={form.entryPointMaxDepth}
              onChange={(e) =>
                setField("entryPointMaxDepth", Number(e.target.value))
              }
              style={{ ...inputStyle, width: 100 }}
            />
          </Field>
        </div>
      </Section>

      {/* Scripts */}
      <Section title="Scripts">
        {form.scripts.map((script, i) => (
          <div
            key={script.id}
            style={{
              border: "1px solid #e5e7eb",
              borderRadius: 10,
              padding: 16,
              marginBottom: 12,
            }}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                marginBottom: 12,
              }}
            >
              <span style={{ fontSize: 13, fontWeight: 600, color: "#374151" }}>
                Script #{i + 1}
              </span>
              <button
                onClick={() => removeScript(i)}
                style={{
                  ...removeBtnStyle,
                  padding: "4px 10px",
                  fontSize: 12,
                  borderRadius: 6,
                }}
              >
                Remove
              </button>
            </div>
            {(
              [
                ["Name", "name", "Reset Database"],
                [
                  "Description",
                  "description",
                  "Drops and recreates the local DB",
                ],
                [
                  "Working Directory",
                  "workingDirectory",
                  "C:\\Projects\\MyApp",
                ],
                ["Command", "command", "dotnet"],
              ] as const
            ).map(([label, key, placeholder]) => (
              <Field key={key} label={label}>
                <input
                  value={script[key]}
                  onChange={(e) => updateScript(i, key, e.target.value)}
                  placeholder={placeholder}
                  style={inputStyle}
                />
              </Field>
            ))}
            <Field label="Arguments (one per line)">
              <textarea
                value={script.arguments.join("\n")}
                onChange={(e) =>
                  updateScript(
                    i,
                    "arguments",
                    e.target.value.split("\n").filter(Boolean),
                  )
                }
                placeholder={"ef\ndatabase\ndrop"}
                rows={3}
                style={{
                  ...inputStyle,
                  fontFamily: "monospace",
                  resize: "vertical",
                }}
              />
            </Field>
          </div>
        ))}
        <AddLink onClick={addScript}>+ Add script</AddLink>
      </Section>

      {/* Save bar */}
      <div
        style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 8 }}
      >
        <button
          onClick={() => save.mutate(form)}
          disabled={save.isPending}
          className="btn-primary"
          style={{ opacity: save.isPending ? 0.6 : 1 }}
        >
          {save.isPending ? "Saving…" : "💾 Save Configuration"}
        </button>
        {save.isSuccess && (
          <span style={{ fontSize: 13, color: "#16a34a" }}>✓ Saved</span>
        )}
        {save.isError && (
          <span style={{ fontSize: 13, color: "#dc2626" }}>✗ Failed</span>
        )}
        <button
          onClick={onClose}
          style={{
            marginLeft: "auto",
            background: "#f3f4f6",
            border: "none",
            borderRadius: 8,
            padding: "8px 18px",
            cursor: "pointer",
            fontSize: 14,
            color: "#374151",
          }}
        >
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
    <div style={{ marginBottom: 28 }}>
      <h3
        style={{
          fontSize: 14,
          fontWeight: 700,
          color: "#1e1b4b",
          margin: "0 0 12px",
          paddingBottom: 6,
          borderBottom: "1px solid #f3f4f6",
        }}
      >
        {title}
      </h3>
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
    <label
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 4,
        marginBottom: 10,
      }}
    >
      <span style={{ fontSize: 12, color: "#6b7280", fontWeight: 500 }}>
        {label}
      </span>
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
    <button
      onClick={onClick}
      style={{
        background: "none",
        border: "none",
        color: "#7c3aed",
        fontSize: 13,
        cursor: "pointer",
        padding: 0,
        fontWeight: 500,
      }}
    >
      {children}
    </button>
  );
}

const inputStyle: React.CSSProperties = {
  width: "100%",
  border: "1px solid #d1d5db",
  borderRadius: 8,
  padding: "7px 10px",
  fontSize: 13,
  fontFamily: "inherit",
  boxSizing: "border-box",
  color: "#111827",
  background: "#fff",
};

const removeBtnStyle: React.CSSProperties = {
  background: "none",
  border: "none",
  color: "#dc2626",
  cursor: "pointer",
  fontSize: 16,
  padding: "0 6px",
  borderRadius: 4,
};
