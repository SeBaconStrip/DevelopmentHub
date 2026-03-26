import { useUiStore, type ThemeId } from "../../store/uiStore";
import { Section } from "./SettingsHelpers";

export function SettingsSectionAppearance() {
  const { theme, setTheme } = useUiStore();
  return (
    <>
      <Section title="Theme">
        <div className="theme-picker">
          {(
            [
              ["violet", "Violet"],
              ["dark", "Dark"],
              ["vscode", "VS Code"],
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
