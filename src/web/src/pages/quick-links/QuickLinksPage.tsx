import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { configApi } from "../../api/config";
import { QuickLinksWidget } from "../dashboard/widgets/QuickLinksWidget";
import type { CustomLink } from "../../types";
import "./QuickLinksPage.css";

type Filter = "all" | "web" | "explorer";

export default function QuickLinksPage() {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");

  const { data: config, isLoading } = useQuery({
    queryKey: ["config"],
    queryFn: configApi.get,
  });

  const links: CustomLink[] = config?.customLinks ?? [];

  const webCount = links.filter((l) => l.type === "web").length;
  const explorerCount = links.filter((l) => l.type === "explorer").length;

  const filtered = links
    .filter((l) => {
      if (filter === "web") return l.type === "web";
      if (filter === "explorer") return l.type === "explorer";
      return true;
    })
    .filter(
      (l) =>
        !search ||
        l.name.toLowerCase().includes(search.toLowerCase()) ||
        l.target.toLowerCase().includes(search.toLowerCase()),
    );

  return (
    <div className="quick-links-page">
      <div className="quick-links-page-card">
        <div className="quick-links-page-toolbar">
          <div className="quick-links-page-toolbar-left">
            <input
              className="quick-links-page-search"
              type="search"
              placeholder="Search by name or URL…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <div className="quick-links-page-filters">
              {(["all", "web", "explorer"] as Filter[]).map((f) => (
                <button
                  key={f}
                  className={`quick-links-page-filter-btn${filter === f ? " quick-links-page-filter-btn--active" : ""}`}
                  onClick={() => setFilter(f)}
                >
                  {f === "all" && `All (${links.length})`}
                  {f === "web" && `🌐 Web${webCount > 0 ? ` (${webCount})` : ""}`}
                  {f === "explorer" && `📁 Explorer${explorerCount > 0 ? ` (${explorerCount})` : ""}`}
                </button>
              ))}
            </div>
          </div>
          {filtered.length !== links.length && (
            <span className="quick-links-page-count">{filtered.length} shown</span>
          )}
        </div>
      </div>

      {isLoading ? (
        <p className="quick-links-page-empty">Loading…</p>
      ) : (
        <div className="quick-links-page-content-card">
          <QuickLinksWidget links={filtered} />
        </div>
      )}
    </div>
  );
}
