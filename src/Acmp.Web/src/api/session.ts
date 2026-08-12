import { useQuery } from '@tanstack/react-query';
import { api, ApiError } from './apiClient';

/*
 * FR-159 / AC-092 — /session, the guest presenter's surface (DEC-037).
 *
 * Caller-scoped by construction: neither call takes a meeting, a topic or a person. The server
 * answers for whoever is asking, so there is no id a guest could change to see somebody else's slot.
 */

export interface SessionMaterial {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface PresenterSession {
  /** The instant the server itself starts refusing — the banner renders this exact stored value. */
  accessExpiresAt: string | null;
  meetingKey: string;
  meetingTitle: string;
  slotStart: string;
  slotEnd: string;
  itemNumber: number;
  itemCount: number;
  timeboxMinutes: number;
  topicKey: string;
  topicTitle: string;
  topicSummary: string;
  materials: SessionMaterial[];
}

/** The caller's own presenter slot, or null when they are not presenting (the server sends 204). */
export function useMySession() {
  return useQuery({
    queryKey: ['session', 'me'],
    queryFn: async () => (await api<PresenterSession | undefined>('/session/me')) ?? null,
    // A refusal is the answer, not a hiccup: retrying an ended access window cannot succeed, and the
    // page needs the ApiError to say so rather than a spinner that never resolves (AC-092).
    retry: (count, error) => !(error instanceof ApiError) || (!error.isAccessEnded && count < 2),
  });
}

/**
 * Opens one of the caller's own materials via a short-lived pre-signed URL (NFR-027).
 *
 * The URL is fetched ON CLICK rather than embedded in the list: it expires in minutes, so a link
 * rendered when the page loaded would be dead by the time a presenter came back to it.
 */
export async function openSessionMaterial(attachmentId: string): Promise<void> {
  const { url } = await api<{ url: string }>(`/session/materials/${attachmentId}`);
  window.open(url, '_blank', 'noopener,noreferrer');
}
