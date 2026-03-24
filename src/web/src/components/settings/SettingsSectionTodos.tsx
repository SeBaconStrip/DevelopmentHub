import { useUiStore } from "../../store/uiStore";
import { Section, WidgetRow } from "./SettingsHelpers";

export function SettingsSectionTodos() {
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
