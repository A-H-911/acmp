/*
 * Minutes tab (P7d) — the meeting's official record, governed by ACMP Agenda & Meeting.dc.html
 * (isMinutes) + the denied state. Rendered inside the meeting shell's <Outlet/> at
 * /meetings/:key/minutes; the shell owns loading/error + the header, so this starts at the MoM banner.
 *
 * Reconciliations (visual SoT = design; behavior SoT = package) — flagged in the progress log:
 *  - 5-state backend vs the design's 3-toggle (draft/review/published): a distinct Approved state sits
 *    between review and publish. The design's single "Approve & lock" button is honoured as one
 *    "Approve & publish" action that drives BOTH transitions (approve → publish) so notify fires on publish.
 *  - Version-preserving supersede (same MIN key, Version++) — the version history lists every version.
 *  - The body is a single bilingual markdown Summary (mirrored en===ar), rendered as text on read
 *    (no markdown→HTML dependency yet, per DV-04). The design's numbered Decision/Actions section cards
 *    are not modeled (Decisions link is P-later; Actions are P8) — a plain document body instead.
 *  - Hash-chain (SHA-256 footer) → P14; Export PDF is a disabled stub.
 *  - denied: meetings routes carry no extra role gate (single global auth gate), so non-managers get a
 *    read-only published record, or the "no edit access" gate when nothing is published yet.
 */
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMeetingDetail } from '../../api/meetings';
import {
  useMinutesForMeeting, useMinutes, useDraftMinutes, useReviseMinutes, useSubmitMinutes,
  useRequestMinutesChanges, useApproveMinutes, usePublishMinutes, useSupersedeMinutes,
  type MinutesDetail, type MinutesStatus, type LocalizedText,
} from '../../api/minutes';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { MarkdownEditor } from '../../components/ui/MarkdownEditor';
import { LoadingState, ErrorState } from '../../components/states';
import { MeetingGate } from './MeetingGate';
import { TemplatePicker } from '../templates/TemplatePicker';
import './minutes.css';

const STATUS_TONE: Record<MinutesStatus, StatusTone> = {
  Draft: 'neutral', InReview: 'warn', Approved: 'info', Published: 'success', Superseded: 'neutral',
};

export function MeetingMinutes() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const { data: meeting } = useMeetingDetail(key);
  const canManage = hasRole(auth, 'chairman') || hasRole(auth, 'secretary');

  const list = useMinutesForMeeting(meeting?.id);
  const head = list.data?.[0];
  const detail = useMinutes(head?.key, head?.version);

  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);

  if (!meeting) return null; // shell owns loading/error

  // Minutes exist only for a meeting that has started (docs/domain/entity-lifecycles.md §6 — InProgress/Held/Closed).
  const meetingRan = meeting.status === 'InProgress' || meeting.status === 'Held' || meeting.status === 'Closed';
  if (!meetingRan) {
    return <MeetingGate icon="doc" title={t('meetings.mom.notReady.title')} body={t('meetings.mom.notReady.body')} />;
  }

  if (list.isLoading || (head && detail.isLoading)) return <LoadingState />;
  if (list.isError) return <ErrorState onRetry={() => list.refetch()} />;

  // No minutes yet: managers get the create form; everyone else gets the "no access / not ready" gate.
  if (!head || !detail.data) {
    return canManage
      ? <CreateMinutes meetingId={meeting.id} />
      : <MeetingGate icon="lock" title={t('meetings.mom.denied.title')} body={t('meetings.mom.denied.body')} />;
  }

  const mom = detail.data;
  const editable = canManage && mom.status === 'Draft';

  return (
    <div className="mom">
      <MinutesBanner mom={mom} pick={pick} />
      <div className="mom-grid">
        <article className={`mom-doc${mom.status === 'Superseded' ? ' mom-doc-muted' : ''}`}>
          <div className="mom-doc-head">
            <div className="mom-committee">{t('meetings.mom.committee')}</div>
            <h2 className="mom-doc-title">{t('meetings.mom.docHeading')}</h2>
            <div className="mom-doc-sub">{mom.meetingKey}</div>
          </div>
          {editable ? (
            <DraftEditor key={mom.id} meetingId={meeting.id} mom={mom} initial={pick(mom.summary)} />
          ) : (
            <div className="mom-prose">{pick(mom.summary)}</div>
          )}
          {mom.status === 'Published' && (
            <div className="mom-approved" role="status">
              <div className="mom-approved-by">
                <span className="mom-approved-lead">{t('meetings.mom.approvedBy')}</span>
                <span className="mom-approved-name">{mom.approvedByName ?? '—'}</span>
              </div>
              <Button variant="secondary" size="sm" disabled title={t('meetings.mom.exportSoon')}>
                {t('meetings.mom.exportPdf')}
              </Button>
            </div>
          )}
        </article>

        <aside className="mom-side">
          {canManage && <ActionsCard meetingId={meeting.id} mom={mom} />}
          {list.data && list.data.length > 0 && <VersionHistory versions={list.data} currentVersion={mom.version} />}
        </aside>
      </div>
    </div>
  );
}

function MinutesBanner({ mom, pick }: { mom: MinutesDetail; pick: (l: LocalizedText) => string }) {
  const { t } = useTranslation();
  const note =
    mom.status === 'Published' ? t('meetings.mom.banner.published')
      : mom.status === 'InReview' ? t('meetings.mom.banner.review')
        : mom.status === 'Superseded' ? t('meetings.mom.banner.superseded')
          : mom.status === 'Approved' ? t('meetings.mom.banner.approved')
            : t('meetings.mom.banner.draft');
  return (
    <div className="mom-banner">
      <div className="mom-banner-main">
        <h1 className="mom-banner-title">{t('meetings.mom.title')}</h1>
        <StatusChip tone={STATUS_TONE[mom.status]} label={t(`meetings.mom.status.${mom.status}`)} size="sm" />
      </div>
      <div className="mom-banner-note">
        {note}
        {mom.status === 'Superseded' && mom.supersessionReason && ` · ${pick(mom.supersessionReason)}`}
      </div>
    </div>
  );
}

/** Manager-only editor for a Draft: Save draft (revise) + Send for review (submit). */
function DraftEditor({ meetingId, mom, initial }: { meetingId: string; mom: MinutesDetail; initial: string }) {
  const { t } = useTranslation();
  const [body, setBody] = useState(initial);
  const revise = useReviseMinutes(meetingId);
  const submit = useSubmitMinutes(meetingId);
  const empty = !body.trim();

  return (
    <div className="mom-editor">
      <MarkdownEditor value={body} onChange={setBody} rows={14} ariaLabel={t('meetings.mom.bodyLabel')} />
      <div className="mom-editor-actions">
        <Button variant="secondary" disabled={empty || revise.isPending} onClick={() => revise.mutate({ id: mom.id, summary: body })}>
          {t('meetings.mom.saveDraft')}
        </Button>
        <Button
          disabled={empty || submit.isPending}
          onClick={async () => { if (!empty) { await revise.mutateAsync({ id: mom.id, summary: body }); submit.mutate(mom.id); } }}
        >
          {t('meetings.mom.sendForReview')}
        </Button>
      </div>
    </div>
  );
}

/** Status-driven action card (managers only): review actions, publish, or supersede. */
function ActionsCard({ meetingId, mom }: { meetingId: string; mom: MinutesDetail }) {
  const { t } = useTranslation();
  const approve = useApproveMinutes(meetingId);
  const publish = usePublishMinutes(meetingId);
  const requestChanges = useRequestMinutesChanges(meetingId);
  const [supersedeOpen, setSupersedeOpen] = useState(false);

  // The design's single "Approve & lock" → approve then publish (both 5-state transitions; notify on publish).
  const approveAndPublish = async () => { await approve.mutateAsync(mom.id); await publish.mutateAsync(mom.id); };
  const busy = approve.isPending || publish.isPending;

  if (mom.status === 'InReview') {
    return (
      <section className="mom-card">
        <div className="mom-card-h">{t('meetings.mom.reviewApprove')}</div>
        <div className="mom-card-body">
          <p className="mom-card-note">{t('meetings.mom.reviewNote')}</p>
          <Button disabled={busy} onClick={approveAndPublish}>{t('meetings.mom.approvePublish')}</Button>
          <Button variant="secondary" disabled={requestChanges.isPending} onClick={() => requestChanges.mutate(mom.id)}>
            {t('meetings.mom.requestChanges')}
          </Button>
        </div>
      </section>
    );
  }
  if (mom.status === 'Approved') {
    return (
      <section className="mom-card">
        <div className="mom-card-h">{t('meetings.mom.readyToPublish')}</div>
        <div className="mom-card-body">
          <Button disabled={publish.isPending} onClick={() => publish.mutate(mom.id)}>{t('meetings.mom.publishNotify')}</Button>
        </div>
      </section>
    );
  }
  if (mom.status === 'Published') {
    return (
      <section className="mom-card">
        <div className="mom-card-h">{t('meetings.mom.corrections')}</div>
        <div className="mom-card-body">
          <p className="mom-card-note">{t('meetings.mom.correctionsNote')}</p>
          <Button variant="secondary" onClick={() => setSupersedeOpen(true)}>{t('meetings.mom.supersede')}</Button>
        </div>
        <SupersedeMinutesDialog meetingId={meetingId} mom={mom} open={supersedeOpen} onClose={() => setSupersedeOpen(false)} />
      </section>
    );
  }
  return null; // Draft actions live in the editor; Superseded has none
}

/** Corrected-version dialog (AC-036): a new body + a reason → a published successor under the same
 *  key. Built on the shared Dialog so it gets focus-trap, Esc-to-close, backdrop-click close and
 *  focus restore (the hand-rolled backdrop had none). Same fields + i18n keys as before. */
function SupersedeMinutesDialog({ meetingId, mom, open, onClose }: { meetingId: string; mom: MinutesDetail; open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const supersede = useSupersedeMinutes(meetingId);
  const [body, setBody] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [touched, setTouched] = useState(false);

  const invalid = !body.trim() || !reason.trim();
  const onSubmit = async () => {
    setTouched(true);
    if (invalid) return;
    setError(null);
    try {
      await supersede.mutateAsync({ id: mom.id, summary: body, reason });
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : t('meetings.mom.supersedeError'));
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="warn"
      title={t('meetings.mom.supersedeTitle')}
      description={t('meetings.mom.supersedeNote')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button loading={supersede.isPending} onClick={() => void onSubmit()}>{t('meetings.mom.supersedeConfirm')}</Button>
        </>
      }
    >
      <div className="mom-supersede-body">
        <label className="mom-label" htmlFor="mom-sup-body">{t('meetings.mom.bodyLabel')}</label>
        <MarkdownEditor id="mom-sup-body" value={body} onChange={setBody} rows={8} ariaLabel={t('meetings.mom.bodyLabel')} />
        {touched && !body.trim() && <p className="mom-field-error">{t('meetings.mom.bodyRequired')}</p>}
        <label className="mom-label" htmlFor="mom-sup-reason">{t('meetings.mom.reasonLabel')}</label>
        <input id="mom-sup-reason" className="mom-input" value={reason} onChange={(e) => setReason(e.target.value)} />
        {touched && !reason.trim() && <p className="mom-field-error">{t('meetings.mom.reasonRequired')}</p>}
        {error && <p className="mom-field-error" role="alert">{error}</p>}
      </div>
    </Dialog>
  );
}

/** Manager-only initial-draft form for a meeting with no minutes yet. */
function CreateMinutes({ meetingId }: { meetingId: string }) {
  const { t } = useTranslation();
  const draft = useDraftMinutes(meetingId);
  const [body, setBody] = useState('');
  const empty = !body.trim();
  return (
    <div className="mom">
      <div className="mom-banner">
        <div className="mom-banner-main"><h1 className="mom-banner-title">{t('meetings.mom.title')}</h1></div>
        <div className="mom-banner-note">{t('meetings.mom.startNote')}</div>
      </div>
      <div className="mom-editor">
        {/* P15h/FR-120: a template pre-fills the minutes body at creation time (mirrored en===ar on save). */}
        <TemplatePicker targetType="MinutesOfMeeting" onApply={setBody} hasContent={!!body.trim()} />
        <MarkdownEditor value={body} onChange={setBody} rows={14} ariaLabel={t('meetings.mom.bodyLabel')} />
        <div className="mom-editor-actions">
          <Button disabled={empty || draft.isPending} onClick={() => draft.mutate(body)}>{t('meetings.mom.startDraft')}</Button>
        </div>
      </div>
    </div>
  );
}

function VersionHistory({ versions, currentVersion }: { versions: { version: number; status: MinutesStatus }[]; currentVersion: number }) {
  const { t } = useTranslation();
  return (
    <section className="mom-card">
      <div className="mom-card-h">{t('meetings.mom.versionHistory')}</div>
      <ul className="mom-versions">
        {versions.map((v) => (
          <li key={v.version} className="mom-version">
            <span className="mom-version-tag">v{v.version}</span>
            <span className="mom-version-status">{t(`meetings.mom.status.${v.status}`)}</span>
            {v.version === currentVersion && <span className="mom-version-current">{t('meetings.mom.current')}</span>}
          </li>
        ))}
      </ul>
    </section>
  );
}
