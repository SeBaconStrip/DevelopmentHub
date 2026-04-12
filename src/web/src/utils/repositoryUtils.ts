export function getScanIssueLabel(issueCode: string): string {
  switch (issueCode) {
    case "DubiousOwnership":
      return "Git ownership blocked";
    case "NotAGitRepository":
      return "Not a Git repository";
    case "PathNotFound":
      return "Path not found";
    case "RemoteNotFoundOrPermissionDenied":
      return "Remote missing or no access";
    case "FetchTimeout":
      return "Fetch timed out";
    case "HostUnreachable":
      return "Host unreachable";
    default:
      return "Scan warning";
  }
}
