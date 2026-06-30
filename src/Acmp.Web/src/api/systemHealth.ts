/*
 * System Health server state (NR-08). Reads GET /api/admin/health — the live status of the
 * registered ASP.NET health checks (admin-config gated on the server). The endpoint returns only
 * what is actually monitored; the SystemHealth screen overlays these onto its fixed service catalog
 * and renders anything unmonitored as "monitoring not configured". Polls every 30s to match the
 * design's "last refreshed 30s ago".
 */
import { useQuery } from '@tanstack/react-query';
import { api } from './apiClient';

/** ASP.NET HealthStatus on the wire: 'Healthy' | 'Degraded' | 'Unhealthy'. */
export interface HealthEntry {
  name: string;
  status: string;
  description?: string | null;
  durationMs: number;
}

export interface SystemHealth {
  status: string;
  entries: HealthEntry[];
}

export function useSystemHealth() {
  return useQuery({
    queryKey: ['admin', 'health'],
    queryFn: () => api<SystemHealth>('/admin/health'),
    refetchInterval: 30_000,
  });
}
