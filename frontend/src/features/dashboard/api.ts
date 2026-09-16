import { apiRequest } from '../../api/http';
import type { RunStatus } from '../runs/api';
export interface DashboardData {
  from: string; toExclusive: string; generatedAt: string; totalRuns: number;
  runCounts: Record<RunStatus, number>; completedRunsWithoutDetails: number;
  passed: number; failed: number; skipped: number; interrupted: number; successRate: number;
  averageTestDurationMs: number | null; flakyTests: number;
  daily: { date: string; runs: number; passed: number; failed: number; skipped: number }[];
  recentRuns: { id: string; projectName: string; suiteName: string; status: RunStatus; createdAt: string }[];
  recentFailures: { runId: string; caseId: string; caseName: string; browser: string; error: string; recordedAt: string }[];
  flakyCases: { caseId: string; caseName: string; browser: string; recoveredRuns: number }[];
}
export const dashboardApi = {
  get: (projectId: string, from: string, to: string, signal?: AbortSignal) => apiRequest<DashboardData>(`/dashboard?${new URLSearchParams({ ...(projectId ? { projectId } : {}), from, to })}`, { signal }),
};
