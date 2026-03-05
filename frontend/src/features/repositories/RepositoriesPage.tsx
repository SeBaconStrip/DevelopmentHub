import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { repositoriesApi } from "../../api/repositories";
import type { Repository } from "../../types";

export default function RepositoriesPage() {
  const qc = useQueryClient();
  const { data: repos = [], isLoading } = useQuery<Repository[]>({
    queryKey: ["repositories"],
    queryFn: repositoriesApi.getAll,
  });

  const scan = useMutation({
    mutationFn: repositoriesApi.scan,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["repositories"] }),
  });

  const toggleFav = useMutation({
    mutationFn: (id: string) => repositoriesApi.toggleFavorite(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["repositories"] }),
  });

  const openRepo = useMutation({
    mutationFn: ({
      id,
      openWith,
    }: {
      id: string;
      openWith: "VisualStudio" | "VsCode";
    }) => repositoriesApi.open(id, { openWith }),
  });

  if (isLoading) return <p style={{ padding: 32 }}>Loading repositories…</p>;

  return (
    <div style={{ padding: 32 }}>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          marginBottom: 24,
        }}
      >
        <h1 style={{ margin: 0, fontSize: 24, fontWeight: 700 }}>
          Repositories
        </h1>
        <button
          onClick={() => scan.mutate()}
          disabled={scan.isPending}
          style={{
            padding: "8px 18px",
            borderRadius: 8,
            border: "none",
            background: "#6c63ff",
            color: "#fff",
            cursor: "pointer",
            fontWeight: 600,
          }}
        >
          {scan.isPending ? "Scanning…" : "Scan"}
        </button>
      </div>

      {repos.length === 0 ? (
        <p style={{ color: "#888" }}>No repositories found. Try scanning.</p>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {repos.map((repo) => (
            <div
              key={repo.id}
              style={{
                background: "rgba(255,255,255,0.06)",
                borderRadius: 10,
                padding: "14px 18px",
                display: "flex",
                alignItems: "center",
                gap: 16,
              }}
            >
              <button
                onClick={() => toggleFav.mutate(repo.id)}
                style={{
                  background: "none",
                  border: "none",
                  cursor: "pointer",
                  fontSize: 18,
                  color: repo.isFavorite ? "#f9c74f" : "#888",
                }}
                title="Toggle favourite"
              >
                ★
              </button>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div
                  style={{
                    fontWeight: 600,
                    fontSize: 15,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                  }}
                >
                  {repo.name}
                </div>
                <div
                  style={{
                    fontSize: 12,
                    color: "#888",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                  }}
                >
                  {repo.path}
                </div>
                {repo.currentBranch && (
                  <div style={{ fontSize: 12, color: "#6c63ff", marginTop: 2 }}>
                    {repo.currentBranch}
                    {(repo.aheadBy > 0 || repo.behindBy > 0) && (
                      <span style={{ color: "#f9c74f", marginLeft: 8 }}>
                        ↑{repo.aheadBy} ↓{repo.behindBy}
                      </span>
                    )}
                  </div>
                )}
              </div>
              <div style={{ display: "flex", gap: 8 }}>
                <button
                  onClick={() =>
                    openRepo.mutate({ id: repo.id, openWith: "VsCode" })
                  }
                  style={{
                    padding: "5px 12px",
                    borderRadius: 6,
                    border: "none",
                    background: "#0098ff",
                    color: "#fff",
                    cursor: "pointer",
                    fontSize: 12,
                  }}
                >
                  VS Code
                </button>
                <button
                  onClick={() =>
                    openRepo.mutate({ id: repo.id, openWith: "VisualStudio" })
                  }
                  style={{
                    padding: "5px 12px",
                    borderRadius: 6,
                    border: "none",
                    background: "#7d3cba",
                    color: "#fff",
                    cursor: "pointer",
                    fontSize: 12,
                  }}
                >
                  VS
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
