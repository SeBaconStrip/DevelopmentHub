import { useQuery } from "@tanstack/react-query";
import { configApi } from "../../api/config";
import { apiFetch } from "../../api/client";
import type { AppConfig } from "../../types";
import type { PluginManifest } from "../../plugins/PluginLoader";
import { getPluginLoadErrors } from "../../plugins/PluginLoader";
import { Section, Field } from "./SettingsHelpers";

interface PluginsPageProps {
  form: AppConfig;
  savedConfig: AppConfig | null;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
}

export function SettingsSectionPlugins({
  form,
  savedConfig,
  setField,
}: PluginsPageProps) {
  const { data: manifests = [], isLoading } = useQuery<PluginManifest[]>({
    queryKey: ["plugins"],
    queryFn: () => apiFetch("/api/plugins").then((r) => r.json()),
  });

  const pluginLoadErrors = getPluginLoadErrors();

  const browsePluginsFolder = async () => {
    const path = await configApi.pickFolder();
    if (path) setField("pluginsFolderPath", path);
  };

  const getPluginSetting = (
    pluginId: string,
    key: string,
    defaultVal: string,
  ) => form.pluginSettings?.[pluginId]?.[key] ?? defaultVal;

  const setPluginSetting = (pluginId: string, key: string, value: string) =>
    setField("pluginSettings", {
      ...form.pluginSettings,
      [pluginId]: { ...(form.pluginSettings?.[pluginId] ?? {}), [key]: value },
    });

  const isPluginEnabled = (pluginId: string) =>
    getPluginSetting(pluginId, "enabled", "true") !== "false";

  const togglePlugin = (pluginId: string) =>
    setPluginSetting(
      pluginId,
      "enabled",
      isPluginEnabled(pluginId) ? "false" : "true",
    );

  const folderPathChanged =
    savedConfig !== null &&
    (form.pluginsFolderPath ?? "") !== (savedConfig.pluginsFolderPath ?? "");

  const enabledStateChanged =
    savedConfig !== null &&
    manifests.some((m) => {
      const formEnabled =
        (form.pluginSettings?.[m.id]?.["enabled"] ?? "true") !== "false";
      const savedEnabled =
        (savedConfig.pluginSettings?.[m.id]?.["enabled"] ?? "true") !== "false";
      return formEnabled !== savedEnabled;
    });

  const restartRequired = folderPathChanged || enabledStateChanged;

  return (
    <>
      {restartRequired && (
        <div className="settings-restart-warning">
          A restart is required for plugin changes to take effect.
        </div>
      )}
      <Section title="Plugins Folder">
        <p className="settings-page-hint">
          Point this to the folder where plugins are installed. Each
          subdirectory must contain a <code>manifest.json</code>.
        </p>
        <Field label="Plugins folder">
          <div className="settings-root-row">
            <input
              className="settings-input"
              value={form.pluginsFolderPath}
              onChange={(e) => setField("pluginsFolderPath", e.target.value)}
              placeholder="%LOCALAPPDATA%\DevelopmentHub\plugins"
            />
            <button
              type="button"
              className="btn-ghost"
              title="Browse…"
              onClick={browsePluginsFolder}
            >
              📁
            </button>
          </div>
        </Field>
      </Section>

      <Section title="Installed Plugins">
        {isLoading && <p className="empty-msg">Loading plugins…</p>}
        {!isLoading && manifests.length === 0 && (
          <p className="empty-msg">No plugins found in the plugins folder.</p>
        )}
        {manifests.map((manifest) => {
          const loadError = pluginLoadErrors.get(manifest.id);
          return (
            <div key={manifest.id} className="plugin-settings-entry">
              <div className="plugin-settings-header">
                <div className="plugin-settings-info">
                  <span className="plugin-settings-name">
                    {manifest.name || manifest.id}
                  </span>
                  {manifest.description && (
                    <span className="plugin-settings-desc">
                      {manifest.description}
                    </span>
                  )}
                  <span className="plugin-settings-version">
                    v{manifest.version}
                  </span>
                </div>
                <button
                  className={`toggle-btn${isPluginEnabled(manifest.id) ? " toggle-btn--on" : ""}`}
                  onClick={() => togglePlugin(manifest.id)}
                  title={
                    isPluginEnabled(manifest.id)
                      ? "Disable plugin"
                      : "Enable plugin"
                  }
                >
                  <span
                    className={`toggle-knob${isPluginEnabled(manifest.id) ? " toggle-knob--on" : ""}`}
                  />
                </button>
              </div>
              {loadError && (
                <div className="plugin-load-error" title={loadError}>
                  ⚠ Frontend konnte nicht geladen werden: {loadError}
                </div>
              )}
            </div>
          );
        })}
      </Section>
    </>
  );
}
