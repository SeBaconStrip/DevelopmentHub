import { useUiStore } from "../../store/uiStore";
import { configApi } from "../../api/config";
import type { AppConfig } from "../../types";
import { Section, Field, WidgetRow } from "./SettingsHelpers";

interface WorkflowsPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
}

export function SettingsSectionWorkflows({ form, setField }: WorkflowsPageProps) {
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
