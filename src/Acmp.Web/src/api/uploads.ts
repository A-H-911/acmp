import { useQuery } from '@tanstack/react-query';
import { api } from './apiClient';

/** AC-169 / DEF-180: the upload limits the server enforces (GET /api/uploads/limits). */
export interface UploadLimits {
  attachmentMaxBytes: number;
  attachmentContentTypes: string[];
  recordingMaxBytes: number;
  recordingContentTypes: string[];
}

/** The shipped defaults (TopicAttachmentOptions / MeetingRecordingOptions), used only until the server answers,
 *  so a page never states a hard-coded limit once the real one is known. */
export const DEFAULT_UPLOAD_LIMITS: UploadLimits = {
  attachmentMaxBytes: 100 * 1024 * 1024,
  attachmentContentTypes: [
    'application/pdf', 'image/png', 'image/jpeg', 'image/svg+xml',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  ],
  recordingMaxBytes: 2 * 1024 * 1024 * 1024,
  recordingContentTypes: ['video/mp4', 'video/webm', 'video/quicktime'],
};

export function useUploadLimits(): UploadLimits {
  const q = useQuery({
    queryKey: ['uploads', 'limits'],
    queryFn: () => api<UploadLimits>('/uploads/limits'),
    staleTime: Infinity,
    retry: false,
  });
  return q.data ?? DEFAULT_UPLOAD_LIMITS;
}

/** Whole megabytes for a limit stated in the UI ("up to {{max}} MB"). */
export const toMb = (bytes: number): number => Math.round(bytes / (1024 * 1024));
