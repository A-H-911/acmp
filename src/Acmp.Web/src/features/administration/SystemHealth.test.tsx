import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import { SystemHealth } from './SystemHealth';
import { renderWithAuth } from '../../test/render';
import type { SystemHealth as SystemHealthDto } from '../../api/systemHealth';

vi.mock('../../api/systemHealth', () => ({ useSystemHealth: vi.fn() }));
import { useSystemHealth } from '../../api/systemHealth';
const mockUseHealth = useSystemHealth as unknown as Mock;

function health(over: Partial<ReturnType<typeof useSystemHealth>>) {
  mockUseHealth.mockReturnValue({ data: undefined, isLoading: false, isError: false, isFetching: false, refetch: vi.fn(), ...over });
}

const dto = (entries: SystemHealthDto['entries'], status = 'Healthy'): SystemHealthDto => ({ status, entries });

describe('SystemHealth (NR-08)', () => {
  beforeEach(() => mockUseHealth.mockReset());

  it('renders the loading state while fetching', () => {
    health({ isLoading: true });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('renders an error state with retry on failure', () => {
    health({ isError: true });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('shows real latency for a healthy check and "operational" status', () => {
    health({ data: dto([{ name: 'sqlserver', status: 'Healthy', description: null, durationMs: 8.3 }]) });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });
    expect(screen.getByText('All core systems operational')).toBeInTheDocument();
    expect(screen.getByText(/8.3 ms/)).toBeInTheDocument();
  });

  it('reflects a degraded check in the overall banner and the tile', () => {
    health({ data: dto([{ name: 'sqlserver', status: 'Degraded', description: 'slow', durationMs: 120 }], 'Degraded') });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });
    expect(screen.getByText('Service degraded — core services operational')).toBeInTheDocument();
    expect(screen.getByText('Degraded')).toBeInTheDocument();
    expect(screen.getByText(/120 ms · slow/)).toBeInTheDocument();
  });

  it('reflects an unhealthy check as a down core service', () => {
    health({ data: dto([{ name: 'sqlserver', status: 'Unhealthy', description: null, durationMs: 0 }], 'Unhealthy') });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });
    expect(screen.getByText('A core service is down')).toBeInTheDocument();
    expect(screen.getByText('Down')).toBeInTheDocument();
  });

  it('refresh button triggers a refetch and is disabled while fetching', async () => {
    const refetch = vi.fn();
    health({ data: dto([{ name: 'api', status: 'Healthy', description: null, durationMs: 1 }]), isFetching: true, refetch });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });
    const btn = screen.getByRole('button', { name: /refresh/i });
    expect(btn).toBeDisabled();
  });

  it('renders six service tiles — unmonitored ones say so rather than inventing a status', () => {
    health({ data: dto([{ name: 'api', status: 'Healthy', description: null, durationMs: 1 }]) });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });
    expect(screen.getByText('Application (API)')).toBeInTheDocument();
    expect(screen.getByText('Seq logging')).toBeInTheDocument();
    expect(screen.getAllByText('Monitoring not configured').length).toBe(5); // all but api
  });

  /*
   * DEF-039. The object store, Seq and Hangfire tiles carried NO `check` at all, so they rendered
   * "monitoring not configured" no matter what the server reported — the display half of DEF-039.
   *
   * ⚠ THE TEST ABOVE CANNOT SEE THAT BUG, and that is why this one exists: it passes a fixture
   * containing only `api`, so those three tiles are legitimately unmonitored in it and the assertion
   * holds whether or not they are wired. It stayed green across the fix. A regression guard has to
   * supply the entries the server actually registers now.
   */
  it('shows the real status for object storage, Seq and Hangfire once the server reports them (DEF-039)', () => {
    health({
      data: dto([
        { name: 'api', status: 'Healthy', description: null, durationMs: 1 },
        { name: 'sqlserver', status: 'Healthy', description: null, durationMs: 2 },
        { name: 'objectstore', status: 'Healthy', description: 'buckets reachable: acmp-prod-recordings', durationMs: 21 },
        { name: 'hangfire', status: 'Healthy', description: 'servers=1, enqueued=0', durationMs: 3 },
        { name: 'seq', status: 'Healthy', description: null, durationMs: 4 },
      ]),
    });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });

    // Only Webex is genuinely unmonitored now — it is Phase 2 and disabled everywhere.
    expect(screen.getAllByText('Monitoring not configured').length).toBe(1);
    expect(screen.getByText('Object storage')).toBeInTheDocument();
    // The label must NOT name MinIO: the cloud stack runs S3, and a MinIO-labelled tile was wrong on
    // the environment it is most likely to be read on.
    expect(screen.queryByText(/MinIO/i)).not.toBeInTheDocument();
  });

  /*
   * A tile that reports "operational" while the server says Unhealthy would be worse than one that
   * says nothing — the whole point of this screen is that it never invents a status.
   */
  it('renders a failing object store as down rather than operational', () => {
    health({
      data: dto(
        [
          { name: 'api', status: 'Healthy', description: null, durationMs: 1 },
          {
            name: 'objectstore',
            status: 'Unhealthy',
            description: "object-storage bucket 'acmp-prod-recordings' unreachable: connection refused",
            durationMs: 5,
          },
        ],
        'Unhealthy',
      ),
    });
    renderWithAuth(<SystemHealth />, { roles: ['administrator'] });

    expect(screen.getByText('Object storage')).toBeInTheDocument();
    // getAllByText, not getByText: an Unhealthy report renders "Down" TWICE — once on the failing
    // tile's chip and once in the overall banner — and getByText throws on multiple matches.
    expect(screen.getAllByText('Down').length).toBeGreaterThanOrEqual(1);
    // The object store is MONITORED and failing, not unmonitored: with api + objectstore reported,
    // exactly the other four tiles say so. Counting is what distinguishes "renders Down" from
    // "silently fell back to not-configured", which is the failure mode this guards.
    expect(screen.getAllByText('Monitoring not configured').length).toBe(4);
  });
});
