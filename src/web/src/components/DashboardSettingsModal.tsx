import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  useUiStore,
  type DashboardWidget,
  type ThemeId,
} from "../store/uiStore";
import { configApi } from "../api/config";
import { useRepositoryScan } from "../hooks/useRepositoryScan";
import type { AppConfig, CustomLink, PullRequestProvider, RepositoryOpener } from "../types";
import githubIcon from "../assets/icons/github.svg";
import azureDevOpsIcon from "../assets/icons/azure-devops.svg";
import "./DashboardSettingsModal.css";

type NavPage =
  | "general"
  | "repositories"
  | "pullRequests"
  | "quickLinks"
  | "workflows"
  | "todos"
  | "integrations"
  | "appearance";

const NAV_ITEMS: { id: NavPage; icon: string; label: string }[] = [
  { id: "general", icon: "⚙", label: "General" },
  { id: "repositories", icon: "📁", label: "Repositories" },
  { id: "pullRequests", icon: "⎇", label: "Pull Requests" },
  { id: "quickLinks", icon: "🔗", label: "Quick Links" },
  { id: "workflows", icon: "⚙", label: "Workflows" },
  { id: "todos", icon: "✅", label: "Todos" },
  { id: "integrations", icon: "🔌", label: "Integrations" },
  { id: "appearance", icon: "🎨", label: "Appearance" },
];

interface Props {
  onClose: () => void;
}

type ProviderField = {
  key: string;
  label: string;
  placeholder: string;
  type?: "text" | "password";
  hint?: string;
  tooltip?: React.ReactNode;
};

type ProviderOption = {
  id: PullRequestProvider;
  label: string;
  description: string;
  icon: string;
  sectionTitle: string;
  sectionHint?: string;
  fields: ProviderField[];
};

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

  const scan = useRepositoryScan();

  const save = useMutation({
    mutationFn: configApi.save,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["config"] });
      queryClient.invalidateQueries({ queryKey: ["workflows"] });
      scan.mutate();
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
            addOpener={addOpener}
            removeOpener={removeOpener}
            updateOpener={updateOpener}
            browseOpenerProgram={browseOpenerProgram}
            browseOpenerIconPath={browseOpenerIconPath}
          />
        );
      case "pullRequests":
        return <PullRequestsPage form={form} setField={setField} />;
      case "quickLinks":
        return <QuickLinksPage form={form} setField={setField} />;
      case "workflows":
        return <WorkflowsPage form={form} setField={setField} />;
      case "todos":
        return <TodosPage />;
      case "integrations":
        return (
          <IntegrationsPage
            form={form}
            setProviderField={setProviderField}
          />
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
  addOpener: () => void;
  removeOpener: (id: string) => void;
  updateOpener: (id: string, patch: Partial<RepositoryOpener>) => void;
  browseOpenerProgram: (id: string) => void;
  browseOpenerIconPath: (id: string) => void;
}

function RepositoriesPage({
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
            {/* Icon type dropdown */}
            <select
              className="settings-input opener-input-icontype"
              value={opener.iconType}
              onChange={(e) => updateOpener(opener.id, { iconType: e.target.value })}
            >
              <option value="vscode">VS Code</option>
              <option value="visualstudio">Visual Studio</option>
              <option value="custom">Custom</option>
            </select>
            {/* Label */}
            <input
              className="settings-input opener-input-label"
              placeholder="Label"
              value={opener.label}
              onChange={(e) => updateOpener(opener.id, { label: e.target.value })}
            />
            {/* Extension */}
            <input
              className="settings-input opener-input-ext"
              placeholder=".ext"
              value={opener.fileExtension}
              onChange={(e) => updateOpener(opener.id, { fileExtension: e.target.value })}
            />
            {/* Program path */}
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
            {/* Icon path (only for custom) */}
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

/* ───────────────────────────────────────────────── Pull Requests page ── */

interface PullRequestsPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
  setProviderField: (
    providerId: PullRequestProvider,
    key: string,
    value: string,
  ) => void;
}

const PULL_REQUEST_PROVIDER_OPTIONS: {
  [K in PullRequestProvider]: ProviderOption;
} = {
  azureDevOps: {
    id: "azureDevOps",
    label: "Azure DevOps",
    description: "Use Azure DevOps repositories and pull requests.",
    icon: azureDevOpsIcon,
    sectionTitle: "Azure DevOps",
    fields: [
      {
        key: "organization",
        label: "Organization",
        placeholder: "myorg",
        tooltip: (
          <>
            <p>Your Azure DevOps organization name as it appears in the URL:</p>
            <code>dev.azure.com/<strong>organization</strong></code>
            <p className="tooltip-used-by">Used by: Pull Requests · Workflows</p>
          </>
        ),
      },
      {
        key: "project",
        label: "Project",
        placeholder: "MyProject",
        tooltip: (
          <>
            <p>The Azure DevOps project to fetch pull requests from.</p>
            <p className="tooltip-used-by">Used by: Pull Requests · Workflows</p>
          </>
        ),
      },
      {
        key: "userEmail",
        label: "User Email",
        placeholder: "you@example.com",
        tooltip: (
          <>
            <p>Your Azure DevOps account email. Used to identify pull requests you authored or are reviewing.</p>
            <p className="tooltip-used-by">Used by: Pull Requests</p>
          </>
        ),
      },
      {
        key: "pat",
        label: "Personal Access Token",
        placeholder: "Leave blank to keep existing",
        type: "password",
        tooltip: (
          <>
            <p>Personal Access Token for Azure DevOps authentication.</p>
            <p><strong>Required scopes:</strong></p>
            <ul>
              <li><strong>Code – Read</strong> · pull requests, repository list</li>
              <li><strong>Build – Read</strong> · workflow pipeline artifact downloads</li>
              <li><strong>Profile – Read</strong> · user identity</li>
            </ul>
            <p className="tooltip-used-by">Used by: Pull Requests · Workflows</p>
          </>
        ),
      },
    ],
  },
  github: {
    id: "github",
    label: "GitHub",
    description: "Use GitHub pull requests and repositories.",
    icon: githubIcon,
    sectionTitle: "GitHub",
    sectionHint:
      "GitHub pull requests are loaded via search for open pull requests that involve the configured user. You can optionally add extra search qualifiers to narrow the result set.",
    fields: [
      {
        key: "userLogin",
        label: "User Login",
        placeholder: "your-login",
        tooltip: (
          <>
            <p>Your GitHub username. Used to search for open PRs where you are author, reviewer, or mentioned.</p>
            <p className="tooltip-used-by">Used by: Pull Requests</p>
          </>
        ),
      },
      {
        key: "searchQuery",
        label: "Extra Search Query",
        placeholder: "org:my-org -label:wip",
        tooltip: (
          <>
            <p>Optional GitHub search qualifiers appended to the base PR query.</p>
            <p><strong>Examples:</strong></p>
            <ul>
              <li><code>org:my-org</code> · limit to an org</li>
              <li><code>repo:owner/name</code> · specific repo</li>
              <li><code>team-review-requested:org/team</code> · team reviews</li>
              <li><code>-label:wip</code> · exclude WIP PRs</li>
            </ul>
            <p className="tooltip-used-by">Used by: Pull Requests</p>
          </>
        ),
      },
      {
        key: "pat",
        label: "Personal Access Token",
        placeholder: "Leave blank to keep existing",
        type: "password",
        tooltip: (
          <>
            <p>Classic or fine-grained Personal Access Token for GitHub authentication.</p>
            <p><strong>Classic token scopes:</strong></p>
            <ul>
              <li><strong>repo</strong> · private repo access, PRs, releases</li>
            </ul>
            <p><strong>Fine-grained permissions:</strong></p>
            <ul>
              <li><strong>Pull requests – Read</strong> · PR list and details</li>
              <li><strong>Contents – Read</strong> · release asset downloads</li>
              <li><strong>Metadata – Read</strong> · required for all repos</li>
            </ul>
            <p className="tooltip-used-by">Used by: Pull Requests · Workflows</p>
          </>
        ),
      },
    ],
  },
};

const PULL_REQUEST_PROVIDER_LIST = [
  {
    ...PULL_REQUEST_PROVIDER_OPTIONS.azureDevOps,
  },
  {
    ...PULL_REQUEST_PROVIDER_OPTIONS.github,
  },
] satisfies ProviderOption[];

function PullRequestsPage({ form, setField }: Omit<PullRequestsPageProps, "setProviderField">) {
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
            How often to poll all configured pull request providers for open
            pull requests.
          </span>
        </Field>
      </Section>
    </>
  );
}

/* ──────────────────────────────────────────────────── Integrations page ── */

interface IntegrationsPageProps {
  form: AppConfig;
  setProviderField: (
    providerId: PullRequestProvider,
    key: string,
    value: string,
  ) => void;
}

function IntegrationsPage({ form, setProviderField }: IntegrationsPageProps) {
  return (
    <>
      {PULL_REQUEST_PROVIDER_LIST.map((provider) => {
        const providerConfig = form.pullRequestProviders[provider.id] ?? {};

        return (
          <Section key={provider.id} title={provider.sectionTitle}>
            <div className="provider-section-heading">
              <img src={provider.icon} alt="" className="provider-radio-icon" />
              <span className="settings-page-hint">{provider.description}</span>
              {provider.sectionHint && (
                <InfoTooltip content={<p>{provider.sectionHint}</p>} />
              )}
            </div>
            {provider.fields.map((field) => (
              <Field
                key={field.key}
                label={field.label}
                tooltip={field.tooltip}
              >
                <input
                  type={field.type ?? "text"}
                  className="settings-input"
                  value={providerConfig[field.key] ?? ""}
                  onChange={(e) =>
                    setProviderField(provider.id, field.key, e.target.value)
                  }
                  placeholder={field.placeholder}
                />
                {field.hint && (
                  <span className="settings-field-hint">{field.hint}</span>
                )}
              </Field>
            ))}
          </Section>
        );
      })}
    </>
  );
}

/* ───────────────────────────────────────────────────── Quick links page ── */

interface QuickLinksPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
}

function QuickLinksPage({ form, setField }: QuickLinksPageProps) {
  const { dashboardWidgets, toggleWidget } = useUiStore();
  const widget = dashboardWidgets.find((w) => w.id === "quickLinks");

  const updateLink = (index: number, next: CustomLink) =>
    setField(
      "customLinks",
      form.customLinks.map((link, currentIndex) =>
        currentIndex === index ? next : link,
      ),
    );

  const addLink = () =>
    setField("customLinks", [
      ...form.customLinks,
      { name: "", target: "", type: "web" },
    ]);

  const removeLink = (index: number) =>
    setField(
      "customLinks",
      form.customLinks.filter((_, currentIndex) => currentIndex !== index),
    );

  const browseExplorerTarget = async (index: number) => {
    const path = await configApi.pickFolder();
    if (!path) return;

    updateLink(index, {
      ...form.customLinks[index],
      target: path,
      type: "explorer",
    });
  };

  return (
    <>
      <Section title="Panel">
        {widget && (
          <WidgetRow
            widget={widget}
            onToggle={() => toggleWidget("quickLinks")}
          />
        )}
      </Section>

      <Section title="Saved Links">
        {form.customLinks.length === 0 && (
          <p className="settings-page-hint">
            Add named shortcuts to websites or Explorer folders.
          </p>
        )}

        {form.customLinks.map((link, index) => (
          <div key={index} className="custom-link-card">
            <div className="custom-link-row">
              <Field label="Name">
                <input
                  className="settings-input"
                  value={link.name}
                  onChange={(e) =>
                    updateLink(index, { ...link, name: e.target.value })
                  }
                  placeholder="Docs, Sprint Board, Downloads..."
                />
              </Field>
              <Field label="Type">
                <select
                  className="settings-input"
                  value={link.type}
                  onChange={(e) =>
                    updateLink(index, {
                      ...link,
                      type: e.target.value as CustomLink["type"],
                    })
                  }
                >
                  <option value="web">Web Link</option>
                  <option value="explorer">Explorer Link</option>
                </select>
              </Field>
            </div>

            <Field label={link.type === "web" ? "URL" : "Folder Path"}>
              <div className="settings-root-row">
                <input
                  className="settings-input"
                  value={link.target}
                  onChange={(e) =>
                    updateLink(index, { ...link, target: e.target.value })
                  }
                  placeholder={
                    link.type === "web"
                      ? "https://example.com"
                      : "C:\\Projects\\SomeFolder"
                  }
                />
                {link.type === "explorer" && (
                  <button
                    type="button"
                    className="btn-ghost"
                    title="Browse…"
                    onClick={() => browseExplorerTarget(index)}
                  >
                    📁
                  </button>
                )}
                <button
                  className="btn-remove"
                  onClick={() => removeLink(index)}
                >
                  ✕
                </button>
              </div>
            </Field>
          </div>
        ))}

        <AddLink onClick={addLink}>+ Add link</AddLink>
      </Section>
    </>
  );
}

function TodosPage() {
  const { dashboardWidgets, toggleWidget } = useUiStore();
  const widget = dashboardWidgets.find((w) => w.id === "todos");

  return (
    <>
      <Section title="Panel">
        {widget && (
          <WidgetRow widget={widget} onToggle={() => toggleWidget("todos")} />
        )}
      </Section>

      <Section title="Completed Todos">
        <p className="settings-page-hint">
          Completed todos stay in a collapsed Done section instead of
          disappearing immediately. That makes it easy to restore something you
          checked off too early.
        </p>
        <p className="settings-page-hint">
          You can restore individual done items, delete them one by one, or
          clear all completed todos from the widget.
        </p>
      </Section>
    </>
  );
}

interface WorkflowsPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
}

function WorkflowsPage({ form, setField }: WorkflowsPageProps) {
  const { dashboardWidgets, toggleWidget } = useUiStore();
  const widget = dashboardWidgets.find((w) => w.id === "workflows");

  const browseWorkflowFolder = async () => {
    const path = await configApi.pickFolder();
    if (path) setField("workflowDefinitionsPath", path);
  };

  return (
    <>
      <Section title="Panel">
        {widget && (
          <WidgetRow
            widget={widget}
            onToggle={() => toggleWidget("workflows")}
          />
        )}
      </Section>

      <Section title="Workflow Definitions">
        <p className="settings-page-hint">
          Point this to a folder containing workflow `*.json` files. Each file
          can contain either a single workflow object or an array of workflows.
        </p>
        <p className="settings-page-hint">
          Supported V1 step types are <code>downloadFile</code>,{" "}
          <code>extractArchive</code>,<code>runExecutable</code>,{" "}
          <code>patchJson</code>, <code>restartWindowsService</code>,
          <code>downloadGithubReleaseAsset</code> and{" "}
          <code>downloadAzureDevopsPipelineArtifactAsset</code>.
        </p>
        <Field label="Workflow folder">
          <div className="settings-root-row">
            <input
              className="settings-input"
              value={form.workflowDefinitionsPath}
              onChange={(e) =>
                setField("workflowDefinitionsPath", e.target.value)
              }
              placeholder="C:\\Workflows"
            />
            <button
              type="button"
              className="btn-ghost"
              title="Browse…"
              onClick={browseWorkflowFolder}
            >
              📁
            </button>
          </div>
        </Field>
        <p className="settings-page-hint">
          Example placeholders inside files: <code>{"{{version}}"}</code>,{" "}
          <code>{"{{serviceName}}"}</code>.
        </p>
        <p className="settings-page-hint">
          The folder is loaded by the backend, so after saving you can just drop
          new JSON files there.
        </p>
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

/* ──────────────────────────────────────────────────── InfoTooltip ── */

function InfoTooltip({ content }: { content: React.ReactNode }) {
  const [visible, setVisible] = useState(false);
  return (
    <span
      className="info-tooltip-wrapper"
      onMouseEnter={() => setVisible(true)}
      onMouseLeave={() => setVisible(false)}
    >
      <span className="info-tooltip-icon">ℹ</span>
      {visible && <div className="info-tooltip-box">{content}</div>}
    </span>
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
  tooltip,
  children,
}: {
  label: string;
  tooltip?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <label className="settings-field">
      <span className="settings-field-label">
        {label}
        {tooltip && <InfoTooltip content={tooltip} />}
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
