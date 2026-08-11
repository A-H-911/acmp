/*
 * Member directory server state (AC-059). The directory is readable by any authenticated role; the
 * Administration screen that hosts it is itself admin-gated by route.
 *
 * NO LONGER READ-ONLY. Identities still live in Keycloak (ADR-0004), but ADR-0038 lets an
 * Administrator or Secretary create one from inside ACMP (FR-156), so the invite mutation below
 * writes THROUGH to Keycloak rather than making this a second identity store. The server enforces
 * who may call it; this module never decides that.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

export interface StreamRef {
  publicId: string;
  code: string;
  nameEn: string;
  nameAr: string;
}

export interface Member {
  publicId: string;
  /** The member's Keycloak subject — used to assign work (e.g. an action's OwnerUserId). */
  keycloakUserId: string;
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

/**
 * The invited account. `temporaryPassword` is returned by the server EXACTLY ONCE (AC-088).
 */
export interface InvitedUser {
  publicId: string;
  fullName: string;
  email: string;
  status: string;
  temporaryPassword: string;
}

/**
 * Invite a user (FR-156). Administrator or Secretary only — enforced server-side.
 *
 * ⚠ The temporary password exists ONLY in this response. "No email in v1" is a hard constraint, so
 * the inviter reading it out is the delivery channel. It must never be logged, persisted, or put
 * anywhere it can be re-read: the 26-password CSV that had to be deleted by hand is the hazard this
 * repeats if it leaks. React Query caches mutation results in memory for the component's lifetime
 * only, and this is deliberately NOT written into the ['members'] cache.
 */
export function useInviteUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { email: string; fullName: string }) =>
      api<InvitedUser>('/members/invite', { method: 'POST', body: JSON.stringify(body) }),
    // The new member arrives at status Invited, so the roster must refetch to show it.
    onSuccess: () => qc.invalidateQueries({ queryKey: ['members'] }),
  });
}
