import { apiFetch } from './client';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    let message = body;
    try {
      message = (JSON.parse(body) as { error?: string }).error ?? body;
    } catch {
      // Not JSON: show the raw body.
    }
    throw new Error(message || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export interface RepositoryRef {
  providerId: string;
  id: string;
  name: string;
  fullName: string;
}

export interface ContributorStat {
  login: string;
  displayName: string;
  avatarUrl: string;
  authoredCount: number;
  reviewedCount: number;
}

export interface ContributorStatsResult {
  providerId: string;
  repositories: RepositoryRef[];
  contributors: ContributorStat[];
  /** Data that was left out of the counts (limits reached, counts unavailable). */
  warnings: string[];
  fetchedAt: string;
}

export interface ContributorStatsRequest {
  provider: string;
  repositoryIds: string[];
  since?: string;
  until?: string;
}

export interface RepositoryContribution {
  repositoryId: string;
  repositoryName: string;
  authoredCount: number;
  reviewedCount: number;
  filesAdded: number;
  filesEdited: number;
  filesDeleted: number;
}

export interface ContributorCoverage {
  login: string;
  displayName: string;
  avatarUrl: string;
  totalAuthored: number;
  totalReviewed: number;
  totalFilesAdded: number;
  totalFilesEdited: number;
  totalFilesDeleted: number;
  repositories: RepositoryContribution[];
}

export interface RepositoryCoverageResult {
  providerId: string;
  repositories: RepositoryRef[];
  contributors: ContributorCoverage[];
  /** Data that was left out of the counts (limits reached, counts unavailable). */
  warnings: string[];
  fetchedAt: string;
}

export interface RepositoryCoverageRequest {
  provider: string;
  repositoryIds: string[];
  contributors: string[];
  since?: string;
  until?: string;
}

export const contributorsApi = {
  getRepositories: (provider: string): Promise<RepositoryRef[]> =>
    apiFetch(`/api/contributors/repositories?provider=${encodeURIComponent(provider)}`)
      .then((r) => handleResponse<RepositoryRef[]>(r)),

  getStats: (request: ContributorStatsRequest): Promise<ContributorStatsResult> =>
    apiFetch('/api/contributors/stats', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }).then((r) => handleResponse<ContributorStatsResult>(r)),

  getRepositoryCoverage: (request: RepositoryCoverageRequest): Promise<RepositoryCoverageResult> =>
    apiFetch('/api/contributors/repository-coverage', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }).then((r) => handleResponse<RepositoryCoverageResult>(r)),
};
