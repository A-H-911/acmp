/*
 * Member directory server state (AC-059). Read-only in P3/P4 UI: identities and roles come from
 * Keycloak (ADR-0004), so there is no create/edit-role mutation here. The directory is readable by
 * any authenticated role; the Administration screen that hosts it is itself admin-gated by route.
 */
import { useQuery } from '@tanstack/react-query';
import { api } from './apiClient';

export interface StreamRef {
  publicId: string;
  code: string;
  nameEn: string;
  nameAr: string;
}

export interface Member {
  publicId: string;
  fullName: string;
  email: string;
  role: string;
  status: string;
  isActive: boolean;
  isVotingEligible: boolean;
  streams: StreamRef[];
}

export function useMembers() {
  return useQuery({
    queryKey: ['members'],
    queryFn: () => api<Member[]>('/members'),
  });
}
