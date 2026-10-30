import { useRef } from "react";
import { useUiStore } from "../../store/uiStore";
import type { AppConfig } from "../../types";
import { Section, WidgetRow } from "./SettingsHelpers";

interface Props {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
}

export function SettingsSectionServices({ form, setField }: Props) {
  const { dashboardWidgets, toggleWidget } = useUiStore();
  const widget = dashboardWidgets.find((w) => w.id === "windowsServices");
  const inputRef = useRef<HTMLInputElement>(null);

  const patterns = form.windowsServicePatterns ?? [];

  function addPattern() {
    const input = inputRef.current;
    if (!input) return;
    const trimmed = input.value.trim();
    if (!trimmed || patterns.includes(trimmed)) return;
    setField("windowsServicePatterns", [...patterns, trimmed]);
    input.value = "";
    input.focus();
  }

  function removePattern(pattern: string) {
    setField("windowsServicePatterns", patterns.filter((p) => p !== pattern));
  }

  return (
    <>
      <Section title="Panel">
        {widget && (
          <WidgetRow widget={widget} onToggle={() => toggleWidget("windowsServices")} />
        )}
      </Section>

      <Section title="Monitored Services">
        <p className="settings-page-hint">
          Add service names or wildcard patterns. Examples:{" "}
          <code>W3SVC</code>, <code>*SQL*</code>, <code>My Service*</code>.
          Matches against both the service name and display name.
        </p>

        {patterns.map((p) => (
          <div key={p} className="settings-root-row" style={{ marginBottom: 6 }}>
            <input
              className="settings-input"
              value={p}
              readOnly
              style={{ fontFamily: "var(--font-mono, monospace)", fontSize: 12 }}
            />
            <button className="btn-remove" onClick={() => removePattern(p)} title="Remove">
              ✕
            </button>
          </div>
        ))}

        <div className="settings-root-row" style={{ marginTop: 8 }}>
          <input
            ref={inputRef}
            className="settings-input"
            placeholder="Service name or pattern…"
            onKeyDown={(e) => e.key === "Enter" && addPattern()}
          />
          <button className="btn-ghost" onClick={addPattern}>
            + Add
          </button>
        </div>
      </Section>
    </>
  );
}
