import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { JobMonitor } from './JobMonitor';
import { renderWithAuth } from '../../test/render';
import type { AdminJobs } from '../../api/jobs';

vi.mock('../../api/jobs', () => ({ useAdminJobs: vi.fn(), useRequeueJob: vi.fn() }));
import { useAdminJobs, useRequeueJob } from '../../api/jobs';
const mockUseJobs = useAdminJobs as unknown as Mock;
const mockUseRequeue = useRequeueJob as unknown as Mock;

function jobsState(over: Partial<ReturnType<typeof useAdminJobs>>) {
  mockUseJobs.mockReturnValue({ data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over });
}
function requeueState(over: Partial<ReturnType<typeof useRequeueJob>> = {}) {
  mockUseRequeue.mockReturnValue({ mutate: vi.fn(), isPending: false, isError: false, variables: undefined, ...over });
}

const configured = (jobs: AdminJobs['jobs']): AdminJobs => ({
  configured: true,
  counts: { succeeded: 1284, processing: 2, scheduled: 9, enqueued: 12, failed: 1 },
  jobs,
});

const FAILED = { id: 'f1', name: 'Send', queue: 'default', status: 'Failed' as const, timestamp: null, durationMs: null, canRetry: true };
const OK = { id: 's1', name: 'MinutesPdf', queue: 'render', status: 'Succeeded' as const, timestamp: '2026-07-02T11:58:00Z', durationMs: 1200, canRetry: false };

describe('JobMonitor (AC-056)', () => {
  beforeEach(() => {
    mockUseJobs.mockReset();
    mockUseRequeue.mockReset();
    requeueState();
  });

  it('renders the loading state while fetching', () => {
    jobsState({ isLoading: true });
    renderWithAuth(<JobMonitor />, { roles: ['administrator'] });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('renders an error state with retry on failure', () => {
    jobsState({ isError: true });
    renderWithAuth(<JobMonitor />, { roles: ['administrator'] });
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('says monitoring is not configured when Hangfire is off', () => {
    jobsState({ data: { configured: false, counts: { succeeded: 0, processing: 0, scheduled: 0, enqueued: 0, failed: 0 }, jobs: [] } });
    renderWithAuth(<JobMonitor />, { roles: ['administrator'] });
    expect(screen.getByText('Job monitoring not configured')).toBeInTheDocument();
  });

  it('shows the five stat tiles and an empty-table state when there are no jobs', () => {
    jobsState({ data: configured([]) });
    renderWithAuth(<JobMonitor />, { roles: ['administrator'] });
    expect(screen.getByText('1284')).toBeInTheDocument();
    expect(screen.getByText('No jobs yet')).toBeInTheDocument();
  });

  it('renders rows with status + duration, and a retry button only on failed rows', () => {
    jobsState({ data: configured([FAILED, OK]) });
    renderWithAuth(<JobMonitor />, { roles: ['administrator'] });
    expect(screen.getByText('Send')).toBeInTheDocument();
    // "Succeeded" appears twice: the stat tile label + the row's status chip.
    expect(screen.getAllByText('Succeeded')).toHaveLength(2);
    expect(screen.getByText('1.2s')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /retry/i })).toHaveLength(1);
  });

  it('re-queues a failed job on click', async () => {
    const mutate = vi.fn();
    requeueState({ mutate });
    jobsState({ data: configured([FAILED]) });
    const user = userEvent.setup();
    renderWithAuth(<JobMonitor />, { roles: ['administrator'] });
    await user.click(screen.getByRole('button', { name: /retry/i }));
    expect(mutate).toHaveBeenCalledWith('f1');
  });

  it('surfaces a re-queue error to the user (never silent)', () => {
    requeueState({ isError: true });
    jobsState({ data: configured([FAILED]) });
    renderWithAuth(<JobMonitor />, { roles: ['administrator'] });
    expect(screen.getByRole('alert')).toHaveTextContent(/couldn.t re-queue/i);
  });
});
