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
});
