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

export interface AzureDevOpsConfig {
  organization: string;
  project: string;
  userEmail: string;
  pat: string;
}

export interface LayoutItem {
  i: string;
  x: number;
  y: number;
  w: number;
  h: number;
  minW?: number;
  minH?: number;
}
  repositoryRoots: string[];
  azureDevOps: AzureDevOpsConfig;
  scanIntervalMinutes: number;
  repoScanDepth: number;
  entryPointScanDepth: number;
  hotkeyBinding: string;
}

