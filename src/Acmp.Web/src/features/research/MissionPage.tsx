/*
 * Research mission detail (P15b) — matches the "ACMP Research & Knowledge.dc.html" isResearch mission
 * drill-in (header + Findings + Recommendations sections). Read by key (GET /api/research/{key}); the
 * shell owns the breadcrumb, so this body starts at the header.
 *
 * Affordance gating is status-AND-role, not role alone (advisor): the P15a aggregate 409s any child
 * mutation unless the mission is Active, and Completed/Cancelled are terminal + immutable. Role-gating
 * alone would reproduce the always-403/409 button the guardrail exists to kill. Every mutating
 * affordance also requires Chairman/Secretary (the API re-checks Research.Manage). Reads stay open.
 *   Proposed  → Activate, Cancel.
 *   Active    → Add finding/rec, Verify (unverified findings), Accept/Reject (Proposed recs), Complete, Cancel.
 *   Completed / Cancelled → read-only; the cancellation reason is shown.
 *
 * Design↔behaviour reconciliations (guardrail #14 — the design shows more than P15a's backend has):
 *  - OMITTED (no backend): the Hypotheses and Acceptance-criteria sections, the Sources & artifacts aside,
 *    the "Convert to execution topic" action (→ P15c) and "Import from Keystone" (deferred, D-05).
 *  - titleAlt renders the SAME text as the title in the opposite direction — content is mirrored to both
 *    LocalizedString locales (en === ar), so the alt line is honest but not a distinct translation.
 *  - KeystonePackageRef is shown as a read fact if set; SourceTopicId as a plain "Linked topic" presence
 *    indicator (a navigable TOP- xref needs a key/title read → P15c).
 *  - Finding chip = derived from IsVerified (Verified/Unverified), Confidence on the item meta line;
 *    Recommendation chip = Status (Proposed/Accepted/Rejected).
 */
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  useMission, useActivateMission, useCompleteMission, useCancelMission,
  useAddFinding, useVerifyFinding, useAddRecommendation, useSetRecommendationStatus,
  type MissionDetail, type Finding, type Recommendation, type LocalizedText,
  type Confidence, type RecommendationPriority,
} from '../../api/research';
import { ApiError } from '../../api/apiClient';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { StatusChip } from '../../components/ui/StatusChip';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { Field, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, findingTone, recStatusTone, CONFIDENCES, PRIORITIES } from './researchMeta';
import './research.css';

type OpenDialog = null | 'cancel' | 'finding' | 'recommendation';

export function MissionPage() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const { data, isLoading, isError, error, refetch } = useMission(key);
  const [dialog, setDialog] = useState<OpenDialog>(null);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState title={t('research.notFoundTitle')} body={t('research.notFoundBody')} />
        ) : (
          <ErrorState onRetry={() => refetch()} />
        )}
      </section>
    );
  }

  const m = data;
  const pick = (l: LocalizedText | null) => (l ? (i18n.language === 'ar' ? l.ar : l.en) : '');
  const otherDir = i18n.language === 'ar' ? 'ltr' : 'rtl';
  const fmtDate = (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(iso));

  const canManage = hasRole(auth, 'chairman', 'secretary');
  const isProposed = m.status === 'Proposed';
  const isActive = m.status === 'Active';
  const cancelled = m.status === 'Cancelled';
  const canActivate = canManage && isProposed;
  const canComplete = canManage && isActive;
  const canCancel = canManage && (isProposed || isActive);
  const canAddItems = canManage && isActive;

  return (
    <section className="page rsc-detail">
      <MissionHeader
        m={m} pick={pick} otherDir={otherDir} fmtDate={fmtDate}
        canActivate={canActivate} canComplete={canComplete} canCancel={canCancel}
        onCancel={() => setDialog('cancel')}
      />

      {cancelled && m.cancellationReason && (
        <div className="rsc-cancel-banner" role="status">
          <Icon name="ban" size={18} aria-hidden />
          <div>
            <span className="rsc-cancel-lead">{t('research.cancelledLead')}</span>
            <span className="rsc-cancel-reason"> · {pick(m.cancellationReason)}</span>
          </div>
        </div>
      )}

      <section className="card rsc-question">
        <div className="rsc-fact-label">{t('research.question')}</div>
        <p className="rsc-question-body">{pick(m.question)}</p>
      </section>

      <FindingsSection m={m} pick={pick} canAdd={canAddItems} onAdd={() => setDialog('finding')} />
      <RecommendationsSection m={m} pick={pick} canAct={canAddItems} onAdd={() => setDialog('recommendation')} />

      <CancelMissionDialog open={dialog === 'cancel'} onClose={() => setDialog(null)} missionId={m.id} />
      <AddFindingDialog open={dialog === 'finding'} onClose={() => setDialog(null)} missionId={m.id} />
      <AddRecommendationDialog open={dialog === 'recommendation'} onClose={() => setDialog(null)} missionId={m.id} />
    </section>
  );
}

interface HeaderProps {
  m: MissionDetail;
  pick: (l: LocalizedText | null) => string;
  otherDir: string;
  fmtDate: (iso: string) => string;
  canActivate: boolean;
  canComplete: boolean;
  canCancel: boolean;
  onCancel: () => void;
}

function MissionHeader({ m, pick, otherDir, fmtDate, canActivate, canComplete, canCancel, onCancel }: HeaderProps) {
  const { t } = useTranslation();
  const activate = useActivateMission();
  const complete = useCompleteMission();
  const altTitle = m.title.en === m.title.ar ? '' : (otherDir === 'rtl' ? m.title.ar : m.title.en);

  return (
    <header className="card rsc-head-card">
      <div className="rsc-head-row">
        <div className="rsc-head-main">
          <div className="rsc-head-chips">
            <span className="rsc-key-chip">{m.key}</span>
            <StatusChip tone="info" label={t('research.missionTag')} size="sm" />
            <StatusChip tone={statusTone(m.status)} label={t(`research.status.${m.status}`)} size="sm" />
          </div>
          <h1 className="rsc-detail-title">{pick(m.title)}</h1>
          {altTitle && <div className="rsc-title-alt" dir={otherDir}>{altTitle}</div>}
          <div className="rsc-head-meta">
            {m.sourceTopicId && (
              <span className="rsc-meta-linked"><Icon name="backlog" size={13} aria-hidden /> {t('research.linkedTopic')}</span>
            )}
            <span>{t('research.lead')}: <b>{m.ownerName || t('research.unassigned')}</b></span>
            <span className="rsc-meta-dot" aria-hidden="true" />
            <span>{t('research.opened')} {fmtDate(m.createdAt)}</span>
            {m.keystonePackageRef && (
              <>
                <span className="rsc-meta-dot" aria-hidden="true" />
                <span>{t('research.keystoneRef')}: <span className="rsc-key">{m.keystonePackageRef}</span></span>
              </>
            )}
          </div>
        </div>
        {(canActivate || canComplete || canCancel) && (
          <div className="rsc-head-actions">
            {canActivate && (
              <Button variant="primary" loading={activate.isPending} onClick={() => void activate.mutateAsync({ id: m.id })}>
                <Icon name="arrowRight" size={15} aria-hidden /> {t('research.activate')}
              </Button>
            )}
            {canComplete && (
              <Button variant="primary" loading={complete.isPending} onClick={() => void complete.mutateAsync({ id: m.id })}>
                <Icon name="check" size={15} aria-hidden /> {t('research.complete')}
              </Button>
            )}
            {canCancel && (
              <Button variant="secondary" onClick={onCancel}>
                <Icon name="ban" size={15} aria-hidden /> {t('research.cancel')}
              </Button>
            )}
          </div>
        )}
      </div>
    </header>
  );
}

function SectionShell({ icon, title, count, canAdd, onAdd, children }: {
  icon: 'search' | 'checklist'; title: string; count: number; canAdd: boolean; onAdd: () => void; children: React.ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <section className="card rsc-sec">
      <div className="rsc-sec-head">
        <div className="rsc-sec-title">
          <Icon name={icon} size={16} aria-hidden />
          <h2>{title}</h2>
          <span className="rsc-sec-count">{count}</span>
        </div>
        {canAdd && (
          <Button variant="ghost" size="sm" onClick={onAdd}>
            <Icon name="plus" size={14} aria-hidden /> {t('research.add')}
          </Button>
        )}
      </div>
      {children}
    </section>
  );
}

function FindingsSection({ m, pick, canAdd, onAdd }: { m: MissionDetail; pick: (l: LocalizedText | null) => string; canAdd: boolean; onAdd: () => void }) {
  const { t } = useTranslation();
  const verify = useVerifyFinding();
  return (
    <SectionShell icon="search" title={t('research.findings')} count={m.findings.length} canAdd={canAdd} onAdd={onAdd}>
      {m.findings.length === 0 ? (
        <p className="rsc-sec-empty">{t('research.noFindings')}</p>
      ) : (
        <ul className="rsc-items">
          {m.findings.map((f: Finding) => (
            <li className="rsc-item" key={f.id}>
              <span className="rsc-item-main">
                <span className="rsc-item-text">{pick(f.summary)}</span>
                <span className="rsc-item-meta">
                  {t(`research.confidence.${f.confidence}`)}
                  {f.detail && <> · {pick(f.detail)}</>}
                </span>
              </span>
              <span className="rsc-item-right">
                <StatusChip tone={findingTone(f.isVerified)} label={t(`research.finding.${f.isVerified ? 'verified' : 'unverified'}`)} size="sm" />
                {canAdd && !f.isVerified && (
                  <Button variant="ghost" size="sm" loading={verify.isPending} onClick={() => void verify.mutateAsync({ id: m.id, findingId: f.id })}>
                    <Icon name="check" size={13} aria-hidden /> {t('research.verify')}
                  </Button>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
    </SectionShell>
  );
}

function RecommendationsSection({ m, pick, canAct, onAdd }: { m: MissionDetail; pick: (l: LocalizedText | null) => string; canAct: boolean; onAdd: () => void }) {
  const { t } = useTranslation();
  const setStatus = useSetRecommendationStatus();
  return (
    <SectionShell icon="checklist" title={t('research.recommendations')} count={m.recommendations.length} canAdd={canAct} onAdd={onAdd}>
      {m.recommendations.length === 0 ? (
        <p className="rsc-sec-empty">{t('research.noRecommendations')}</p>
      ) : (
        <ul className="rsc-items">
          {m.recommendations.map((r: Recommendation) => (
            <li className="rsc-item" key={r.id}>
              <span className="rsc-item-main">
                <span className="rsc-item-text">{pick(r.statement)}</span>
                <span className="rsc-item-meta">
                  {t(`research.priority.${r.priority}`)}
                  {r.rationale && <> · {pick(r.rationale)}</>}
                </span>
              </span>
              <span className="rsc-item-right">
                <StatusChip tone={recStatusTone(r.status)} label={t(`research.recStatus.${r.status}`)} size="sm" />
                {canAct && r.status === 'Proposed' && (
                  <span className="rsc-item-actions">
                    <Button variant="ghost" size="sm" loading={setStatus.isPending} onClick={() => void setStatus.mutateAsync({ id: m.id, recommendationId: r.id, status: 'Accepted' })}>
                      <Icon name="check" size={13} aria-hidden /> {t('research.accept')}
                    </Button>
                    <Button variant="ghost" size="sm" loading={setStatus.isPending} onClick={() => void setStatus.mutateAsync({ id: m.id, recommendationId: r.id, status: 'Rejected' })}>
                      <Icon name="ban" size={13} aria-hidden /> {t('research.reject')}
                    </Button>
                  </span>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
    </SectionShell>
  );
}

// ---- Dialogs (inline so one MissionPage.test covers them) ---------------------------------------

function useSubmitError() {
  const { t } = useTranslation();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const toMessage = (err: unknown) => setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('research.actionError') : t('research.actionError'));
  return { submitError, setSubmitError, toMessage };
}

function CancelMissionDialog({ open, onClose, missionId }: { open: boolean; onClose: () => void; missionId: string }) {
  const { t } = useTranslation();
  const cancel = useCancelMission();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const { submitError, setSubmitError, toMessage } = useSubmitError();

  async function onConfirm() {
    setSubmitError(null);
    if (!reason.trim()) { setError(t('research.cancelDialog.err')); return; }
    setError(null);
    try {
      await cancel.mutateAsync({ id: missionId, reason: { en: reason.trim(), ar: reason.trim() } });
      setReason('');
      onClose();
    } catch (err) {
      toMessage(err);
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="danger"
      icon={<Icon name="ban" size={20} aria-hidden />}
      title={t('research.cancelDialog.title')}
      description={t('research.cancelDialog.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="danger" loading={cancel.isPending} onClick={() => void onConfirm()}>{t('research.cancelDialog.confirm')}</Button>
        </>
      }
    >
      <div className="rsc-create-form">
        <Field label={t('research.cancelDialog.reason')} required error={error ?? undefined}>
          {(p) => (
            <Textarea {...p} rows={3} value={reason} placeholder={t('research.cancelDialog.reasonPh')} onChange={(e) => setReason(e.target.value)} />
          )}
        </Field>
        {submitError && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>}
      </div>
    </Dialog>
  );
}

function AddFindingDialog({ open, onClose, missionId }: { open: boolean; onClose: () => void; missionId: string }) {
  const { t } = useTranslation();
  const add = useAddFinding();
  const [summary, setSummary] = useState('');
  const [detail, setDetail] = useState('');
  const [confidence, setConfidence] = useState<Confidence>('Medium');
  const [error, setError] = useState<string | null>(null);
  const { submitError, setSubmitError, toMessage } = useSubmitError();
  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  async function onConfirm() {
    setSubmitError(null);
    if (!summary.trim()) { setError(t('research.findingDialog.err')); return; }
    setError(null);
    try {
      await add.mutateAsync({ id: missionId, summary: loc(summary), detail: detail.trim() ? loc(detail) : null, confidence });
      setSummary(''); setDetail('');
      onClose();
    } catch (err) {
      toMessage(err);
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="search" size={20} aria-hidden />}
      title={t('research.findingDialog.title')}
      description={t('research.findingDialog.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={add.isPending} onClick={() => void onConfirm()}>{t('research.findingDialog.confirm')}</Button>
        </>
      }
    >
      <div className="rsc-create-form">
        <Field label={t('research.findingDialog.summary')} required error={error ?? undefined}>
          {(p) => (
            <Textarea {...p} rows={2} value={summary} placeholder={t('research.findingDialog.summaryPh')} onChange={(e) => setSummary(e.target.value)} />
          )}
        </Field>
        <Field label={t('research.findingDialog.confidence')} required>
          {(p) => (
            <Select id={p.id} options={CONFIDENCES.map((v) => ({ value: v, label: t(`research.confidence.${v}`) }))} value={confidence} onChange={(v) => setConfidence(v as Confidence)} ariaLabel={t('research.findingDialog.confidence')} />
          )}
        </Field>
        <Field label={t('research.findingDialog.detail')}>
          {(p) => (
            <Textarea {...p} rows={2} value={detail} placeholder={t('research.findingDialog.detailPh')} onChange={(e) => setDetail(e.target.value)} />
          )}
        </Field>
        {submitError && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>}
      </div>
    </Dialog>
  );
}

function AddRecommendationDialog({ open, onClose, missionId }: { open: boolean; onClose: () => void; missionId: string }) {
  const { t } = useTranslation();
  const add = useAddRecommendation();
  const [statement, setStatement] = useState('');
  const [rationale, setRationale] = useState('');
  const [priority, setPriority] = useState<RecommendationPriority>('Medium');
  const [error, setError] = useState<string | null>(null);
  const { submitError, setSubmitError, toMessage } = useSubmitError();
  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  async function onConfirm() {
    setSubmitError(null);
    if (!statement.trim()) { setError(t('research.recDialog.err')); return; }
    setError(null);
    try {
      await add.mutateAsync({ id: missionId, statement: loc(statement), rationale: rationale.trim() ? loc(rationale) : null, priority });
      setStatement(''); setRationale('');
      onClose();
    } catch (err) {
      toMessage(err);
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="checklist" size={20} aria-hidden />}
      title={t('research.recDialog.title')}
      description={t('research.recDialog.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={add.isPending} onClick={() => void onConfirm()}>{t('research.recDialog.confirm')}</Button>
        </>
      }
    >
      <div className="rsc-create-form">
        <Field label={t('research.recDialog.statement')} required error={error ?? undefined}>
          {(p) => (
            <Textarea {...p} rows={2} value={statement} placeholder={t('research.recDialog.statementPh')} onChange={(e) => setStatement(e.target.value)} />
          )}
        </Field>
        <Field label={t('research.recDialog.priority')} required>
          {(p) => (
            <Select id={p.id} options={PRIORITIES.map((v) => ({ value: v, label: t(`research.priority.${v}`) }))} value={priority} onChange={(v) => setPriority(v as RecommendationPriority)} ariaLabel={t('research.recDialog.priority')} />
          )}
        </Field>
        <Field label={t('research.recDialog.rationale')}>
          {(p) => (
            <Textarea {...p} rows={2} value={rationale} placeholder={t('research.recDialog.rationalePh')} onChange={(e) => setRationale(e.target.value)} />
          )}
        </Field>
        {submitError && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>}
      </div>
    </Dialog>
  );
}
