import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { configApi } from "../../api/config";
import { QuickLinksWidget } from "../dashboard/widgets/QuickLinksWidget";
import { FilterToolbar } from "../../components/FilterToolbar";
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

  const linkFilters = [
    { value: "all", label: `All (${links.length})` },
    { value: "web", label: `🌐 Web${webCount > 0 ? ` (${webCount})` : ""}` },
    { value: "explorer", label: `📁 Explorer${explorerCount > 0 ? ` (${explorerCount})` : ""}` },
  ];

  return (
    <div className="quick-links-page">
      <div className="quick-links-page-card">
        <FilterToolbar
          search={search}
          onSearchChange={setSearch}
          filter={filter}
          onFilterChange={(f) => setFilter(f as Filter)}
          filters={linkFilters}
          searchPlaceholder="Search by name or URL…"
          shownCount={filtered.length}
          totalCount={links.length}
        />
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
