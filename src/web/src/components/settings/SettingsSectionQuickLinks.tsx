import { useUiStore } from "../../store/uiStore";
import { configApi } from "../../api/config";
import type { AppConfig, CustomLink } from "../../types";
import { Section, Field, AddLink, WidgetRow } from "./SettingsHelpers";

interface QuickLinksPageProps {
  form: AppConfig;
  setField: <K extends keyof AppConfig>(key: K, value: AppConfig[K]) => void;
}

export function SettingsSectionQuickLinks({ form, setField }: QuickLinksPageProps) {
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
