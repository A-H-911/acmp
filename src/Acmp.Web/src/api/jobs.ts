/*
 * Job Monitor server state (AC-056). Reads GET /api/admin/jobs — Hangfire's live counts + recent runs,
 * admin-config gated on the server. `configured` is false when Hangfire isn't wired on this instance
 * (test / minimal deploys), so the screen can say "monitoring not configured" instead of showing zeros
 * as if they were real. Retry (re-queue) posts to /api/admin/jobs/{id}/requeue and is audited server-side.
 * Polls every 30s like System Health.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

/** The five statuses the Job Monitor renders (a closed set — the server never sends others). */
export type JobStatus = 'Succeeded' | 'Processing' | 'Scheduled' | 'Enqueued' | 'Failed';

export interface JobCounts {
  succeeded: number;
  processing: number;
  scheduled: number;
  enqueued: number;
  failed: number;
}

export interface JobRow {
  id: string;
  name: string;
  queue: string;
  status: JobStatus;
  /** ISO-8601 UTC instant of the row's state change; null when Hangfire records none. */
  timestamp: string | null;
  /** Run duration in ms (succeeded jobs only); null otherwise. */
  durationMs: number | null;
  canRetry: boolean;
}

export interface AdminJobs {
  /** False when Hangfire isn't wired on this instance (test / minimal deploys). */
  configured: boolean;
  counts: JobCounts;
  jobs: JobRow[];
}

export function useAdminJobs() {
  return useQuery({
    queryKey: ['admin', 'jobs'],
    queryFn: () => api<AdminJobs>('/admin/jobs'),
    refetchInterval: 30_000,
  });
}

/** Retry (re-queue) a failed job. Invalidates the jobs query so the table reflects the new state. */
export function useRequeueJob() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      api<void>(`/admin/jobs/${encodeURIComponent(id)}/requeue`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'jobs'] }),
  });
}
