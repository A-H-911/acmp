/*
 * Recording tab — routed at /meetings/:key/recording (ACMP Meetings.dc.html isRecording). Shows an uploaded
 * recording (player + source chip + download + replace + delete) or a Webex recording (external link +
 * delete), or an upload dropzone for Secretary/Chairman. Uploaded playback streams from MinIO via a
 * short-lived presigned URL (ADR-0014, FR-056). Transcript is Phase-2/Webex (P19) — deferred, so the
 * design's 2-col transcript panel is omitted and the player card is the focus.
 */
import { useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMeetingDetail, useRecordingUrl, useUploadMeetingRecording, useDeleteMeetingRecording } from '../../api/meetings';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { Icon } from '../../components/icons';
import { Button } from '../../components/ui/Button';
import { StatusChip } from '../../components/ui/StatusChip';
import { Dialog } from '../../components/ui/Dialog';
import { MeetingGate } from './MeetingGate';

/** Only trust an https URL for the player/link (guards against a bad value producing a javascript:/data: src). */
function safeHttps(url: string | null | undefined): string | null {
  if (!url) return null;
  try {
    return new URL(url).protocol === 'https:' ? url : null;
  } catch {
    return null;
  }
}

function formatSize(bytes: number | null | undefined): string {
  if (!bytes) return '';
  const mb = bytes / (1024 * 1024);
  return mb >= 1 ? `${mb.toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

export function MeetingRecording() {
  const { key } = useParams();
  const { t } = useTranslation();
  const auth = useAuth();
  const { data: meeting } = useMeetingDetail(key);
  const rec = meeting?.recording ?? null;
  const canManage = hasRole(auth, 'chairman', 'secretary');
  const uploadHook = useUploadMeetingRecording(key);
  const deleteHook = useDeleteMeetingRecording(key);
  const urlQuery = useRecordingUrl(key, rec?.source === 'Uploaded');
  const inputRef = useRef<HTMLInputElement>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);

  if (!meeting) return null; // shell owns loading/error

  const pick = (list: FileList | null) => {
    const file = list?.[0];
    if (file) uploadHook.mutate(file);
  };

  const heading = <h1 className="mt-rec-h1">{t('meetings.recording.title')}</h1>;
  const hiddenInput = (
    <input ref={inputRef} type="file" accept="video/mp4,video/webm,video/quicktime" className="visually-hidden" aria-label={t('meetings.recording.upload')} onChange={(e) => pick(e.target.files)} />
  );

  // A recording exists → design player card (uploaded video or Webex link) + footer (source · meta · actions).
  if (rec) {
    const uploaded = rec.source === 'Uploaded';
    const src = uploaded ? safeHttps(urlQuery.data?.url) : null;
    const webexLink = !uploaded ? safeHttps(rec.playbackUrl) : null;
    return (
      <div className="mt-recording">
        {heading}
        <div className="mt-rec-card">
          <div className="mt-rec-frame">
            {uploaded && src && <video className="mt-rec-player" controls preload="metadata" src={src} />}
            {uploaded && !src && <span className="mt-rec-frame-note">{t('meetings.recording.loading.title')}</span>}
            {!uploaded &&
              (webexLink ? (
                <a className="mt-rec-webex" href={webexLink} target="_blank" rel="noopener noreferrer">
                  <Icon name="video" size={26} aria-hidden />
                  <span>{t('meetings.recording.openWebex')}</span>
                  <Icon name="arrowUpRight" size={15} aria-hidden />
                </a>
              ) : (
                <span className="mt-rec-frame-note">{t('meetings.recording.empty.title')}</span>
              ))}
          </div>
          <div className="mt-rec-foot">
            <div className="mt-rec-info">
              <StatusChip
                tone={uploaded ? 'neutral' : 'info'}
                label={uploaded ? t('meetings.recording.sourceUploaded') : t('meetings.recording.sourceWebex')}
              />
              {rec.fileName && <span className="mt-rec-name" dir="ltr">{rec.fileName}</span>}
              {rec.sizeBytes ? <span className="mt-rec-size">{formatSize(rec.sizeBytes)}</span> : null}
            </div>
            <div className="mt-rec-actions">
              {uploaded && src && (
                <a className="btn btn-ghost btn-sm mt-rec-dl" href={src} download={rec.fileName ?? undefined}>
                  <Icon name="download" size={15} aria-hidden /> {t('meetings.recording.download')}
                </a>
              )}
              {canManage && (
                <Button variant="secondary" size="sm" onClick={() => inputRef.current?.click()} loading={uploadHook.isPending}>
                  <Icon name="upload" size={15} aria-hidden /> {t('meetings.recording.replace')}
                </Button>
              )}
              {canManage && (
                <Button variant="danger" size="sm" onClick={() => setConfirmDelete(true)}>
                  <Icon name="trash" size={15} aria-hidden /> {t('meetings.recording.delete')}
                </Button>
              )}
            </div>
          </div>
        </div>
        {canManage && hiddenInput}
        {canManage && (
          <Dialog
            open={confirmDelete}
            onClose={() => setConfirmDelete(false)}
            tone="danger"
            icon={<Icon name="trash" size={20} aria-hidden />}
            title={t('meetings.recording.deleteConfirm.title')}
            description={t('meetings.recording.deleteConfirm.body')}
            footer={
              <>
                <Button variant="secondary" onClick={() => setConfirmDelete(false)}>{t('meetings.recording.cancel')}</Button>
                <Button variant="danger" loading={deleteHook.isPending} onClick={() => deleteHook.mutate(undefined, { onSuccess: () => setConfirmDelete(false) })}>
                  {t('meetings.recording.delete')}
                </Button>
              </>
            }
          />
        )}
      </div>
    );
  }

  // No recording → upload dropzone for Secretary/Chairman, else honest empty state.
  if (canManage) {
    return (
      <div className="mt-recording">
        {heading}
        <div className="mt-rec-drop">
          <div className="mt-rec-drop-ic" aria-hidden="true"><Icon name="upload" size={22} /></div>
          <Button variant="primary" onClick={() => inputRef.current?.click()} loading={uploadHook.isPending}>
            {t('meetings.recording.upload')}
          </Button>
          <p className="mt-rec-drop-hint">{t('meetings.recording.uploadHint')}</p>
          {hiddenInput}
          {uploadHook.isError && <p className="mt-rec-error" role="alert">{t('meetings.recording.error')}</p>}
        </div>
      </div>
    );
  }

  return (
    <div className="mt-recording">
      {heading}
      <MeetingGate icon="video" title={t('meetings.recording.empty.title')} body={t('meetings.recording.empty.body')} />
    </div>
  );
}
