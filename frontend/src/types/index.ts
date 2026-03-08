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
  openWith: 'VisualStudio' | 'VsCode';
}

export interface Execution {
  id: string;
  scriptDefinitionId: string;
  scriptName: string;
  startedAt: string;
  finishedAt: string | null;
  exitCode: number | null;
  status: 'Running' | 'Success' | 'Failed' | 'Cancelled';
}

export interface ExecutionDetail extends Execution {
  outputLog: string;
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

export interface ScriptConfig {
  id: string;
  name: string;
  description: string;
  workingDirectory: string;
  command: string;
  arguments: string[];
  environmentVariables: Record<string, string>;
}

export interface AppConfig {
  repositoryRoots: string[];
  azureDevOps: AzureDevOpsConfig;
  scanIntervalMinutes: number;
  entryPointMaxDepth: number;
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

export interface DashboardConfig {
  widgets: { id: string; enabled: boolean }[];
  gridLayouts: Record<string, LayoutItem[]>;
}
