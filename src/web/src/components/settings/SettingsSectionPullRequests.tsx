import { useUiStore } from "../../store/uiStore";
import type { AppConfig } from "../../types";
import { Section, Field, WidgetRow } from "./SettingsHelpers";

interface PullRequestsPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
}

export function SettingsSectionPullRequests({ form, setField }: PullRequestsPageProps) {
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
