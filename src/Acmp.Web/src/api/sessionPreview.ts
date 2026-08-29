import { useQuery } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PresenterSession } from './session';

/*
 * FR-165 / DEC-086 — the Chairman/Secretary preview of a CHOSEN presenter's session view.
 *
 * ⚠ A SEPARATE MODULE FROM api/session.ts ON PURPOSE, and the separation is the security design rather
 * than filing tidiness. session.ts's own header records the property this endpoint deliberately does not
 * share: those calls take no meeting, topic or person, so there is no id a guest could change to see
 * somebody else's slot. This one is ALL targeting — so it is a different path, refused for guests at the
 * path itself (/api/session-preview is outside GuestSurfaceMiddleware's /api/session allowlist), and the
 * two never share a client, a query key or a cache entry.
 */

/** A chosen presenter's slot, or null when there is nothing to preview (the server sends 204). */
export function useSessionPreview(meetingId: string | undefined, topicId: string | undefined) {
  return useQuery({
    // The target is part of the key: previewing a second presenter must refetch rather than serve the
    // first one's slot out of cache, which would show a Secretary the wrong person's view and look right.
    queryKey: ['session', 'preview', meetingId, topicId],
    enabled: Boolean(meetingId && topicId),
    queryFn: async () =>
      (await api<PresenterSession | undefined>(
        `/session-preview?meetingId=${encodeURIComponent(meetingId!)}&topicId=${encodeURIComponent(topicId!)}`,
      )) ?? null,
  });
}
