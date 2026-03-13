export interface EntryPoint {
  filePath: string;
  fileName: string;
  type: 'Solution' | 'CodeWorkspace' | 'Folder';
}

export interface Repository {
  id: string;
  name: string;
  path: string;
  isFavorite: boolean;
  currentBranch: string | null;
  aheadBy: number;
  behindBy: number;
  entryPoints: EntryPoint[];
  openCount: number;
  lastOpenedAt: string | null;
  lastSyncedAt: string | null;
  usageScore: number;
}

export interface OpenRepositoryRequest {
  entryPointPath?: string;
  openWith: 'VisualStudio' | 'VsCode' | 'Explorer';
}


export interface PullRequest {
  providerId: PullRequestProvider;
  prId: number;
  title: string;
  repositoryName: string;
  status: string;
  url: string;
  createdByMe: boolean;
  isReviewer: boolean;
  reviewerVote: number;
  sourceBranch: string;
  targetBranch: string;
  createdAt: string;
  isDraft: boolean;
  authorDisplayName: string;
}

export type PullRequestProvider = 'azureDevOps' | 'github';
export type PullRequestProviderConfig = Record<string, string>;
export type PullRequestProvidersConfig = Record<string, PullRequestProviderConfig>;

export interface LayoutItem {
  i: string;
  x: number;
  y: number;
  w: number;
  h: number;
  minW?: number;
  minH?: number;
}

export interface AppConfig {
  repositoryRoots: string[];
  pullRequestProviders: PullRequestProvidersConfig;
  scanIntervalMinutes: number;
  repoScanDepth: number;
  entryPointScanDepth: number;
  hotkeyBinding: string;
  prRefreshIntervalSeconds: number;
}

