import type { AppConfig } from "../../types";
import { Section, Field } from "./SettingsHelpers";

interface GeneralPageProps {
  form: AppConfig;
  isCapturingHotkey: boolean;
  setIsCapturingHotkey: (v: boolean | ((prev: boolean) => boolean)) => void;
  handleHotkeyCapture: (e: React.KeyboardEvent<HTMLInputElement>) => void;
}

export function SettingsSectionGeneral({
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
