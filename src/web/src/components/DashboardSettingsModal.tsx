import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import type { AppConfig, PullRequestProvider, RepositoryOpener } from "../types";
import { configApi } from "../api/config";
import { apiFetch } from "../api/client";
import { SettingsSectionGeneral } from "./settings/SettingsSectionGeneral";
import { SettingsSectionRepositories } from "./settings/SettingsSectionRepositories";
import { SettingsSectionPullRequests } from "./settings/SettingsSectionPullRequests";
import { SettingsSectionQuickLinks } from "./settings/SettingsSectionQuickLinks";
import { SettingsSectionWorkflows } from "./settings/SettingsSectionWorkflows";
import { SettingsSectionTodos } from "./settings/SettingsSectionTodos";
import { SettingsSectionIntegrations } from "./settings/SettingsSectionIntegrations";
import { SettingsSectionAppearance } from "./settings/SettingsSectionAppearance";
import { SettingsSectionPlugins } from "./settings/SettingsSectionPlugins";
import { SettingsSectionPluginDetail } from "./settings/SettingsSectionPluginDetail";
import type { PluginManifest } from "../plugins/PluginLoader";
import "./DashboardSettingsModal.css";

type NavPage =
  | "general"
  | "repositories"
  | "pullRequests"
  | "quickLinks"
  | "workflows"
  | "todos"
  | "integrations"
  | "appearance"
  | "plugins"
  | `plugin:${string}`;

const STATIC_NAV_ITEMS: { id: NavPage; icon: string; label: string }[] = [
  { id: "general", icon: "⚙", label: "General" },
  { id: "repositories", icon: "📁", label: "Repositories" },
  { id: "pullRequests", icon: "⎇", label: "Pull Requests" },
  { id: "quickLinks", icon: "🔗", label: "Quick Links" },
  { id: "workflows", icon: "⚙", label: "Workflows" },
  { id: "todos", icon: "✅", label: "Todos" },
  { id: "integrations", icon: "🔌", label: "Integrations" },
  { id: "appearance", icon: "🎨", label: "Appearance" },
  { id: "plugins", icon: "🔌", label: "Plugins" },
];

interface Props {
  onClose: () => void;
}

function normalizeConfig(config: AppConfig): AppConfig {
  const existingProviders = config.pullRequestProviders ?? {};
  return {
    ...config,
    repositoryRoots: config.repositoryRoots ?? [],
    customLinks: (config.customLinks ?? []).map((link) => ({
      name: link.name ?? "",
      target: link.target ?? "",
      type: link.type === "explorer" ? "explorer" : "web",
    })),
    workflows: (config.workflows ?? []).map((workflow) => ({
      ...workflow,
      inputs: workflow.inputs ?? [],
      steps: workflow.steps ?? [],
    })),
    workflowDefinitionsPath: config.workflowDefinitionsPath ?? "",
    pluginsFolderPath: config.pluginsFolderPath ?? "",
    pluginSettings: config.pluginSettings ?? {},
    pullRequestProviders: {
      ...existingProviders,
      azureDevOps: {
        organization: existingProviders.azureDevOps?.organization ?? "",
        project: existingProviders.azureDevOps?.project ?? "",
        userEmail: existingProviders.azureDevOps?.userEmail ?? "",
        pat: existingProviders.azureDevOps?.pat ?? "",
      },
      github: {
        userLogin: existingProviders.github?.userLogin ?? "",
        searchQuery: existingProviders.github?.searchQuery ?? "",
        pat: existingProviders.github?.pat ?? "",
      },
    },
    hotkeyBinding: config.hotkeyBinding ?? "Ctrl+Shift+D",
    repositoryOpeners: (config.repositoryOpeners ?? []).map((o) => ({
      id: o.id ?? crypto.randomUUID(),
      label: o.label ?? "",
      fileExtension: o.fileExtension ?? "",
      programPath: o.programPath ?? "",
      iconType: o.iconType ?? "custom",
      iconPath: o.iconPath ?? "",
      sortOrder: o.sortOrder ?? 0,
    })),
  };
}

export function DashboardSettingsModal({ onClose }: Props) {
  const [page, setPage] = useState<NavPage>("general");
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["config"],
    queryFn: configApi.get,
  });

  const { data: installedPlugins = [] } = useQuery<PluginManifest[]>({
    queryKey: ["plugins"],
    queryFn: () => apiFetch("/api/plugins").then(r => r.json()),
  });

  const save = useMutation({
    mutationFn: configApi.save,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["config"] });
      queryClient.invalidateQueries({ queryKey: ["workflows"] });
      // Immediately refresh the repo list from the DB (orphans already removed by the server).
      // The full scan result (newly discovered repos) will arrive via SignalR once complete.
      queryClient.invalidateQueries({ queryKey: ["repositories"] });
      onClose();
    },
  });

  const [form, setForm] = useState<AppConfig | null>(null);
  const [isCapturingHotkey, setIsCapturingHotkey] = useState(false);

  useEffect(() => {
    if (!data) return;
    const normalized = normalizeConfig(JSON.parse(JSON.stringify(data)));
    setForm(normalized);
  }, [data]);

  const setField = <K extends keyof AppConfig>(key: K, value: AppConfig[K]) =>
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev));

  const setProviderField = (
    providerId: PullRequestProvider,
    key: string,
    value: string,
  ) =>
    setForm((prev) =>
      prev
        ? {
            ...prev,
            pullRequestProviders: {
              ...prev.pullRequestProviders,
              [providerId]: {
                ...(prev.pullRequestProviders[providerId] ?? {}),
                [key]: value,
              },
            },
          }
        : prev,
    );

  const setTodoSyncField = (key: string, value: string) =>
    setForm((prev) =>
      prev
        ? {
            ...prev,
            todoSyncProviders: {
              ...(prev.todoSyncProviders ?? {}),
              microsoftTodo: {
                ...(prev.todoSyncProviders?.microsoftTodo ?? {}),
                [key]: value,
              },
            },
          }
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

  const addOpener = () =>
    form && setField("repositoryOpeners", [
      ...form.repositoryOpeners,
      { id: crypto.randomUUID(), label: "", fileExtension: "", programPath: "", iconType: "custom", iconPath: "", sortOrder: form.repositoryOpeners.length },
    ]);

  const removeOpener = (id: string) =>
    form && setField("repositoryOpeners", form.repositoryOpeners.filter((o) => o.id !== id));

  const updateOpener = (id: string, patch: Partial<RepositoryOpener>) =>
    form && setField("repositoryOpeners", form.repositoryOpeners.map((o) => o.id === id ? { ...o, ...patch } : o));

  const browseOpenerProgram = async (id: string) => {
    const path = await configApi.pickFile("Executable files (*.exe)|*.exe|All files (*.*)|*.*");
    if (path) updateOpener(id, { programPath: path });
  };

  const browseOpenerIconPath = async (id: string) => {
    const path = await configApi.pickFile("Icon sources (*.exe;*.ico;*.dll)|*.exe;*.ico;*.dll|All files (*.*)|*.*");
    if (path) updateOpener(id, { iconPath: path });
  };

  const handleHotkeyCapture = (e: React.KeyboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const ignored = ["Control", "Shift", "Alt", "Meta", "CapsLock", "Tab", "Escape"];
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
          <SettingsSectionGeneral
            form={form}
            isCapturingHotkey={isCapturingHotkey}
            setIsCapturingHotkey={setIsCapturingHotkey}
            handleHotkeyCapture={handleHotkeyCapture}
          />
        );
      case "repositories":
        return (
          <SettingsSectionRepositories
            form={form}
            setField={setField}
            addRoot={addRoot}
            removeRoot={removeRoot}
            updateRoot={updateRoot}
            browseRoot={browseRoot}
            addOpener={addOpener}
            removeOpener={removeOpener}
            updateOpener={updateOpener}
            browseOpenerProgram={browseOpenerProgram}
            browseOpenerIconPath={browseOpenerIconPath}
          />
        );
      case "pullRequests":
        return <SettingsSectionPullRequests form={form} setField={setField} />;
      case "quickLinks":
        return <SettingsSectionQuickLinks form={form} setField={setField} />;
      case "workflows":
        return <SettingsSectionWorkflows form={form} setField={setField} />;
      case "todos":
        return (
          <SettingsSectionTodos
            form={form}
            setTodoSyncField={setTodoSyncField}
            setField={setField}
          />
        );
      case "integrations":
        return (
          <SettingsSectionIntegrations
            form={form}
            setProviderField={setProviderField}
          />
        );
      case "appearance":
        return <SettingsSectionAppearance />;
      case "plugins":
        return <SettingsSectionPlugins form={form} savedConfig={data ? normalizeConfig(JSON.parse(JSON.stringify(data))) : null} setField={setField} />;
      default:
        if (page.startsWith("plugin:")) {
          const pluginId = page.slice("plugin:".length);
          const manifest = installedPlugins.find(p => p.id === pluginId);
          if (manifest) return <SettingsSectionPluginDetail manifest={manifest} />;
        }
        return null;
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
            {STATIC_NAV_ITEMS.map((item) => (
              <button
                key={item.id}
                className={`settings-nav-item${page === item.id ? " settings-nav-item--active" : ""}`}
                onClick={() => setPage(item.id)}
              >
                <span className="settings-nav-icon">{item.icon}</span>
                <span className="settings-nav-label">{item.label}</span>
              </button>
            ))}
            {installedPlugins.filter(p => (p.settings ?? []).length > 0).map((p) => (
              <button
                key={`plugin:${p.id}`}
                className={`settings-nav-item settings-nav-item--sub${page === `plugin:${p.id}` ? " settings-nav-item--active" : ""}`}
                onClick={() => setPage(`plugin:${p.id}`)}
              >
                <span className="settings-nav-icon">⚙</span>
                <span className="settings-nav-label">{p.name || p.id}</span>
              </button>
            ))}
          </nav>

          <div className="settings-modal-body">{renderPage()}</div>
        </div>

        <div className="settings-save-bar">
          <button
            className="btn-primary"
            onClick={() => form && save.mutate(form)}
            disabled={save.isPending || !form}
          >
            {save.isPending ? "Saving…" : "💾 Save"}
          </button>
          {save.isError && (
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
