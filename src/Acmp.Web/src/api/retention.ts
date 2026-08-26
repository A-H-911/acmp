/*
 * Retention configuration server state (WBS-24.5 / DW-036; FR-155, NFR-059, NFR-060).
 * Reads GET /api/admin/retention and writes PUT /api/admin/retention/{key} — both admin-config gated
 * on the server (Policies.AdminConfig, Administrator only), because SEC-077 classifies a
 * retention/immutability config change as a privileged action.
 *
 * ⚠ `automaticPurgeEnabled` IS NOT A SETTING. The server reports it as a constant false: no purge path
 * exists in v1 (SEC-089 places enforcement in Phase 2), so there is nothing a toggle could switch on.
 * It is on the wire so the screen can STATE the v1 posture rather than leave a reader to infer it.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

export interface RetentionSetting {
  key: string;
  valueJson: string;
}

export interface RetentionPolicy {
  automaticPurgeEnabled: boolean;
  settings: RetentionSetting[];
}

const KEY = ['admin', 'retention'];

export function useRetentionPolicy() {
  return useQuery({ queryKey: KEY, queryFn: () => api<RetentionPolicy>('/admin/retention') });
}

export function useSetRetentionSetting() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ key, valueJson }: RetentionSetting) =>
      api<void>(`/admin/retention/${encodeURIComponent(key)}`, {
        method: 'PUT',
        // `api` sets Accept but not Content-Type, so a body needs it explicitly or the server
        // rejects the request before the endpoint is reached (the convention across this folder).
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ valueJson }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
  });
}
