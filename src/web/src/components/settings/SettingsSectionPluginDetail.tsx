import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { PluginManifest } from "../../plugins/PluginLoader";
import { Section, Field } from "./SettingsHelpers";

interface Props {
  manifest: PluginManifest;
}

export function SettingsSectionPluginDetail({ manifest }: Props) {
  const queryClient = useQueryClient();
  const queryKey = ["plugin-settings", manifest.id];

  const { data: settings = {}, isLoading } = useQuery<Record<string, string>>({
    queryKey,
    queryFn: () => apiFetch(`/api/plugins/${encodeURIComponent(manifest.id)}/settings`).then(r => r.json()),
  });

  const save = useMutation({
    mutationFn: (updated: Record<string, string>) =>
      apiFetch(`/api/plugins/${encodeURIComponent(manifest.id)}/settings`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(updated),
      }),
    onSuccess: (_data, updated) => {
      // Update our own local cache immediately
      queryClient.setQueryData(queryKey, updated);
      // Invalidate the plugin's own query key so its widgets/pages re-fetch
      queryClient.invalidateQueries({ queryKey: [manifest.id, "settings"] });
    },
  });

  const getValue = (key: string, defaultValue: string) =>
    settings[key] ?? defaultValue;

  const setValue = (key: string, value: string) => {
    const updated = { ...settings, [key]: value };
    save.mutate(updated);
  };

  if (isLoading) return <p className="empty-msg">Loading…</p>;

  return (
    <Section title={manifest.name || manifest.id}>
      {manifest.description && (
        <p className="settings-page-hint">{manifest.description}</p>
      )}
      <p className="settings-page-hint">v{manifest.version}</p>
      {(manifest.settings ?? []).map((setting) => (
        <Field key={setting.key} label={setting.label}>
          {setting.type === "bool" ? (
            <input
              type="checkbox"
              checked={getValue(setting.key, setting.defaultValue) === "true"}
              onChange={(e) => setValue(setting.key, e.target.checked ? "true" : "false")}
            />
          ) : setting.type === "select" ? (
            <select
              className="settings-input"
              value={getValue(setting.key, setting.defaultValue)}
              onChange={(e) => setValue(setting.key, e.target.value)}
            >
              {(setting.options ?? []).map((opt) => (
                <option key={opt} value={opt}>{opt}</option>
              ))}
            </select>
          ) : (
            <input
              className="settings-input"
              type="text"
              value={getValue(setting.key, setting.defaultValue)}
              onChange={(e) => setValue(setting.key, e.target.value)}
            />
          )}
        </Field>
      ))}
      {save.isError && <p className="settings-save-err">Failed to save</p>}
    </Section>
  );
}
