import { describe, it, expect } from 'vitest';
import { getScanIssueLabel } from '../../utils/repositoryUtils';

describe('getScanIssueLabel', () => {
  it('returns correct label for DubiousOwnership', () => {
    expect(getScanIssueLabel('DubiousOwnership')).toBe('Git ownership blocked');
  });

  it('returns correct label for NotAGitRepository', () => {
    expect(getScanIssueLabel('NotAGitRepository')).toBe('Not a Git repository');
  });

  it('returns correct label for PathNotFound', () => {
    expect(getScanIssueLabel('PathNotFound')).toBe('Path not found');
  });

  it('returns correct label for RemoteNotFoundOrPermissionDenied', () => {
    expect(getScanIssueLabel('RemoteNotFoundOrPermissionDenied')).toBe('Remote missing or no access');
  });

  it('returns correct label for FetchTimeout', () => {
    expect(getScanIssueLabel('FetchTimeout')).toBe('Fetch timed out');
  });

  it('returns fallback label for unknown issue code', () => {
    expect(getScanIssueLabel('SomeUnknownCode')).toBe('Scan warning');
  });

  it('returns fallback label for empty string', () => {
    expect(getScanIssueLabel('')).toBe('Scan warning');
  });
});
