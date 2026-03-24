import { useState } from "react";
import { launcherApi } from "../../../api/launcher";
import type { CustomLink } from "../../../types";
import { Empty } from "./shared";
import { ErrorBar } from "../../../components/ErrorBar";
import "./QuickLinksWidget.css";

export function QuickLinksWidget({ links }: { links: CustomLink[] }) {
  const [error, setError] = useState<string | null>(null);

  if (links.length === 0) {
    return <Empty text="No quick links yet. Add some in Settings > Quick Links." />;
  }

  async function handleOpen(link: CustomLink) {
    try {
      setError(null);
      if (link.type === "explorer") {
        await launcherApi.openExplorer(link.target);
        return;
      }

      await launcherApi.openUrl(link.target);
    } catch {
      setError(`Could not open "${link.name}".`);
    }
  }

  return (
    <div className="quick-links-list">
      <ErrorBar message={error} onDismiss={() => setError(null)} />
      {links.map((link, index) => (
        <button
          key={`${link.type}-${link.target}-${index}`}
          className="quick-link-item"
          onClick={() => handleOpen(link)}
          title={link.target}
        >
          <span className="quick-link-icon">
            {link.type === "explorer" ? "📁" : "🌐"}
          </span>
          <span className="quick-link-copy">
            <span className="item-name">{link.name}</span>
            <span className="item-meta">{link.target}</span>
          </span>
        </button>
      ))}
    </div>
  );
}
