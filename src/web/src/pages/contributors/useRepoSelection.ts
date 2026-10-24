import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { contributorsApi, type RepositoryRef } from "../../api/contributors";

export type Provider = "azureDevOps" | "github";

export const PROVIDERS: { id: Provider; label: string }[] = [
  { id: "azureDevOps", label: "Azure DevOps" },
  { id: "github", label: "GitHub" },
];

export const providerLabel = (provider: Provider) =>
  PROVIDERS.find((p) => p.id === provider)?.label ?? provider;

/**
 * Converts the date inputs ("YYYY-MM-DD") into an inclusive range in local time.
 * `new Date("YYYY-MM-DD")` would parse as UTC midnight, which drops the first
 * hours of the start day east of UTC.
 */
function toIsoRange(since: string, until: string) {
  return {
    since: since ? new Date(since + "T00:00:00").toISOString() : undefined,
    until: until ? new Date(until + "T23:59:59.999").toISOString() : undefined,
  };
}

/** Provider, repository and date-range state shared by the contributor reports. */
export function useRepoSelection(onReset: () => void) {
  const [provider, setProviderState] = useState<Provider>("azureDevOps");
  const [repoSearch, setRepoSearch] = useState("");
  const [selectedRepoIds, setSelectedRepoIds] = useState<string[]>([]);
  const [since, setSince] = useState("");
  const [until, setUntil] = useState("");

  const reposQuery = useQuery<RepositoryRef[]>({
    queryKey: ["contributor-repos", provider],
    queryFn: () => contributorsApi.getRepositories(provider),
  });
  const repositories = useMemo(() => reposQuery.data ?? [], [reposQuery.data]);

  const filteredRepos = useMemo(
    () =>
      repositories.filter(
        (r) =>
          !repoSearch ||
          r.name.toLowerCase().includes(repoSearch.toLowerCase()),
      ),
    [repositories, repoSearch],
  );

  const allFilteredSelected =
    filteredRepos.length > 0 &&
    filteredRepos.every((r) => selectedRepoIds.includes(r.id));

  const setProvider = (next: Provider) => {
    setProviderState(next);
    setSelectedRepoIds([]);
    onReset();
  };

  const toggleRepo = (id: string) =>
    setSelectedRepoIds((prev) =>
      prev.includes(id) ? prev.filter((r) => r !== id) : [...prev, id],
    );

  const toggleSelectAll = () => {
    if (allFilteredSelected) {
      const filteredIds = new Set(filteredRepos.map((r) => r.id));
      setSelectedRepoIds((prev) => prev.filter((id) => !filteredIds.has(id)));
    } else {
      setSelectedRepoIds((prev) => [
        ...new Set([...prev, ...filteredRepos.map((r) => r.id)]),
      ]);
    }
  };

  return {
    provider,
    setProvider,
    repoSearch,
    setRepoSearch,
    repositories,
    reposLoading: reposQuery.isLoading,
    reposError: reposQuery.error,
    filteredRepos,
    selectedRepoIds,
    toggleRepo,
    allFilteredSelected,
    toggleSelectAll,
    since,
    setSince,
    until,
    setUntil,
    dateRange: () => toIsoRange(since, until),
    isComplete: selectedRepoIds.length > 0 && !!since && !!until,
  };
}

export type RepoSelection = ReturnType<typeof useRepoSelection>;
