import { useUiStore } from "../../store/uiStore";
import type { AppConfig, RepositoryOpener } from "../../types";
import { Section, Field, AddLink, WidgetRow } from "./SettingsHelpers";

interface RepositoriesPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
  addRoot: () => void;
  removeRoot: (i: number) => void;
  updateRoot: (i: number, val: string) => void;
  browseRoot: (i: number) => void;
  addOpener: () => void;
  removeOpener: (id: string) => void;
  updateOpener: (id: string, patch: Partial<RepositoryOpener>) => void;
  browseOpenerProgram: (id: string) => void;
  browseOpenerIconPath: (id: string) => void;
}

export function SettingsSectionRepositories({
  form,
  setField,
  addRoot,
  removeRoot,
  updateRoot,
  browseRoot,
  addOpener,
  removeOpener,
  updateOpener,
  browseOpenerProgram,
  browseOpenerIconPath,
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

      <Section title="Openers">
        <p className="settings-field-hint" style={{ marginBottom: 10 }}>
          Configure which file extensions to search for and how to open them.
          Leave the program path empty to open via Windows shell association.
        </p>
        {form.repositoryOpeners.map((opener) => (
          <div key={opener.id} className="opener-row">
            <select
              className="settings-input opener-input-icontype"
              value={opener.iconType}
              onChange={(e) => updateOpener(opener.id, { iconType: e.target.value })}
            >
              <option value="vscode">VS Code</option>
              <option value="visualstudio">Visual Studio</option>
              <option value="custom">Custom</option>
            </select>
            <input
              className="settings-input opener-input-label"
              placeholder="Label"
              value={opener.label}
              onChange={(e) => updateOpener(opener.id, { label: e.target.value })}
            />
            <input
              className="settings-input opener-input-ext"
              placeholder=".ext"
              value={opener.fileExtension}
              onChange={(e) => updateOpener(opener.id, { fileExtension: e.target.value })}
            />
            <input
              className="settings-input opener-input-program"
              placeholder="Program or command (empty = shell)"
              value={opener.programPath}
              onChange={(e) => updateOpener(opener.id, { programPath: e.target.value })}
            />
            <button
              type="button"
              className="btn-ghost"
              title="Browse executable…"
              onClick={() => browseOpenerProgram(opener.id)}
            >
              📁
            </button>
            {opener.iconType === "custom" && (
              <>
                <input
                  className="settings-input opener-input-iconpath"
                  placeholder="Icon (.exe/.ico, optional)"
                  value={opener.iconPath}
                  onChange={(e) => updateOpener(opener.id, { iconPath: e.target.value })}
                />
                <button
                  type="button"
                  className="btn-ghost"
                  title="Browse icon file…"
                  onClick={() => browseOpenerIconPath(opener.id)}
                >
                  🖼
                </button>
              </>
            )}
            <button className="btn-remove" onClick={() => removeOpener(opener.id)}>✕</button>
          </div>
        ))}
        <AddLink onClick={addOpener}>+ Add opener</AddLink>
      </Section>
    </>
  );
}
