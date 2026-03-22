export interface EntryPoint {
  filePath: string;
  fileName: string;
  extension: string;
}

export interface RepositoryOpener {
  id: string;
  label: string;
  fileExtension: string;
  programPath: string;
  /** 'vscode' | 'visualstudio' | 'custom' */
  iconType: string;
  /** Path to an .exe/.ico file to extract the icon from (used when iconType is 'custom') */
  iconPath: string;
  sortOrder: number;
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
  scanIssueCode?: string | null;
  scanIssueMessage?: string | null;
  tags: string[];
}

export interface OpenRepositoryRequest {
  openerId?: string;
  entryPointPath?: string;
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

export interface TodoItem {
  id: string;
  title: string;
  linkUrl: string | null;
  completed: boolean;
  createdAt: string;
  completedAt: string | null;
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
  repositoryOpeners: RepositoryOpener[];
  customLinks: CustomLink[];
  workflows: WorkflowDefinition[];
  workflowDefinitionsPath: string;
  pullRequestProviders: PullRequestProvidersConfig;
  scanIntervalMinutes: number;
  repoScanDepth: number;
  entryPointScanDepth: number;
  hotkeyBinding: string;
  prRefreshIntervalSeconds: number;
}

export interface CustomLink {
  name: string;
  target: string;
  type: 'web' | 'explorer';
}

export interface WorkflowDefinition {
  id: string;
  name: string;
  description: string;
  requiresConfirmation: boolean;
  inputs: WorkflowInput[];
  steps: WorkflowStep[];
}

export interface WorkflowInput {
  name: string;
  label: string;
  type: "text";
  defaultValue: string;
}

export interface WorkflowStep {
  type: string;
  name: string;
  url: string;
  owner: string;
  repository: string;
  releaseTag: string;
  artifactName?: string;
  assetName?: string;
  organization: string;
  project: string;
  pipelineId: string;
  runId: string;
  buildId: string;
  pat: string;
  targetPath: string;
  overwrite: boolean;
  runElevated: boolean;
  archivePath: string;
  destinationPath: string;
  cleanDestination: boolean;
  sourcePath?: string;
  filePath: string;
  arguments: string[];
  waitForExit: boolean;
  successExitCodes: number[];
  operations: JsonPatchOperation[];
  serviceName: string;
  waitForRunning: boolean;
  timeoutSeconds: number;
}

export interface JsonPatchOperation {
  op: "set" | "remove" | "append";
  path: string;
  value?: unknown;
}

export interface RunWorkflowRequest {
  inputs: Record<string, string>;
  confirmed: boolean;
}

export interface WorkflowExecution {
  id: string;
  workflowId: string;
  workflowName: string;
  startedAt: string;
  finishedAt: string | null;
  status: string;
  exitCode: number | null;
  summary: string;
}

export interface WorkflowExecutionDetail extends WorkflowExecution {
  logLines: WorkflowLogLine[];
}

export interface WorkflowLogLine {
  text: string;
  stream: string;
  timestamp: string;
}

