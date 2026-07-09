/*
 * Webex runtime config (P13, point 5). Reads GET /api/webex/status so the schedule form knows whether an
 * online meeting will get an auto-generated Webex join URL — without baking Webex state into the build.
 * `canAutoCreate` implies `enabled` AND a stored OAuth token (the create job no-ops without one). Rarely
 * changes, so a long staleTime avoids refetching on every schedule-form mount.
 */
import { useQuery } from '@tanstack/react-query';
import { api } from './apiClient';

export interface WebexStatus {
  enabled: boolean;
  canAutoCreate: boolean;
}

export function useWebexStatus() {
  return useQuery({
    queryKey: ['webex', 'status'],
    queryFn: () => api<WebexStatus>('/webex/status'),
    staleTime: 5 * 60_000,
  });
}
