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
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  useMission, useActivateMission, useCompleteMission, useCancelMission,
  useAddFinding, useVerifyFinding, useAddRecommendation, useSetRecommendationStatus,
  type MissionDetail, type Finding, type Recommendation, type LocalizedText,
  type Confidence, type RecommendationPriority,
} from '../../api/research';
import { useArtifactRelationships } from '../../api/traceability';
import { ApiError } from '../../api/apiClient';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { StatusChip } from '../../components/ui/StatusChip';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { Field, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { TraceabilityPanel } from '../traceability/TraceabilityPanel';
import { hrefFor } from '../traceability/traceMeta';
import { ConvertToTopicDialog } from './ConvertToTopicDialog';
import { statusTone, findingTone, recStatusTone, CONFIDENCES, PRIORITIES } from './researchMeta';
import './research.css';

type OpenDialog = null | 'cancel' | 'finding' | 'recommendation';

/** A convert-dialog target: the whole mission (no recommendationId) or one recommendation. */
interface ConvertTarget {
  recommendationId?: string;
  title: string;
  description: string;
}

export function MissionPage() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const { data, isLoading, isError, error, refetch } = useMission(key);
  // The mission's typed edges (FR-114) — feeds both the header xref (FR-115) and the Linked-items panel;
  // one query, deduped with the panel's own read. Disabled until the mission id is known.
  const rels = useArtifactRelationships('ResearchMission', data?.id);
  const [dialog, setDialog] = useState<OpenDialog>(null);
  const [convertTarget, setConvertTarget] = useState<ConvertTarget | null>(null);

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
  const isCompleted = m.status === 'Completed';
  const cancelled = m.status === 'Cancelled';
  const canActivate = canManage && isProposed;
  const canComplete = canManage && isActive;
  const canCancel = canManage && (isProposed || isActive);
  const canAddItems = canManage && isActive;
  // Mission-level convert (W16) needs a COMPLETED mission — the backend 409s otherwise, so we only show it
  // then (design mockup shows it on an Active mission; behaviour wins — flagged, INV-014/guardrail #14).
  const canConvertMission = canManage && isCompleted;
  // FR-115: the source topic's navigable key comes from the incoming Topic→Mission edge (a create-time
  // snapshot). Missing for pre-P15c missions / a later source-topic (backfill deferred) — the header then
  // falls back to the plain "linked topic" indicator.
  const sourceTopicKey = rels.data?.incoming.find((e) => e.otherType === 'Topic')?.otherKey ?? null;

  return (
    <section className="page rsc-detail">
      <MissionHeader
        m={m} pick={pick} otherDir={otherDir} fmtDate={fmtDate}
        canActivate={canActivate} canComplete={canComplete} canCancel={canCancel}
        canConvert={canConvertMission} sourceTopicKey={sourceTopicKey}
        onCancel={() => setDialog('cancel')}
        onConvert={() => setConvertTarget({ title: pick(m.title), description: pick(m.question) })}
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

      <div className="rsc-detail-grid">
        <div className="rsc-detail-main">
          <section className="card rsc-question">
            <div className="rsc-fact-label">{t('research.question')}</div>
            <p className="rsc-question-body">{pick(m.question)}</p>
          </section>

          <FindingsSection m={m} pick={pick} canAdd={canAddItems} onAdd={() => setDialog('finding')} />
          <RecommendationsSection
            m={m} pick={pick} canAct={canAddItems} canManage={canManage} onAdd={() => setDialog('recommendation')}
            onConvertRec={(r) => setConvertTarget({ recommendationId: r.id, title: pick(r.statement), description: pick(r.rationale) })}
          />
        </div>

        <TraceabilityPanel traceType="ResearchMission" id={m.id} artifactKey={m.key} title={pick(m.title)} />
      </div>

      <CancelMissionDialog open={dialog === 'cancel'} onClose={() => setDialog(null)} missionId={m.id} />
      <AddFindingDialog open={dialog === 'finding'} onClose={() => setDialog(null)} missionId={m.id} />
      <AddRecommendationDialog open={dialog === 'recommendation'} onClose={() => setDialog(null)} missionId={m.id} />
      {convertTarget && (
        <ConvertToTopicDialog
          key={convertTarget.recommendationId ?? 'mission'}
          onClose={() => setConvertTarget(null)}
          missionId={m.id}
          recommendationId={convertTarget.recommendationId}
          seedTitle={convertTarget.title}
          seedDescription={convertTarget.description}
        />
      )}
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
  canConvert: boolean;
  /** The source topic's navigable key from the incoming edge, or null → plain indicator fallback. */
  sourceTopicKey: string | null;
  onCancel: () => void;
  onConvert: () => void;
}

function MissionHeader({ m, pick, otherDir, fmtDate, canActivate, canComplete, canCancel, canConvert, sourceTopicKey, onCancel, onConvert }: HeaderProps) {
  const { t } = useTranslation();
  const activate = useActivateMission();
  const complete = useCompleteMission();
  const altTitle = m.title.en === m.title.ar ? '' : (otherDir === 'rtl' ? m.title.ar : m.title.en);
  const topicHref = sourceTopicKey ? hrefFor('Topic', sourceTopicKey) : null;

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
            {sourceTopicKey && topicHref ? (
              <Link className="rsc-xref-link" to={topicHref}>
                <Icon name="backlog" size={13} aria-hidden /> {t('research.linkedTopic')}: <span className="rsc-xref">{sourceTopicKey}</span>
              </Link>
            ) : m.sourceTopicId ? (
              <span className="rsc-meta-linked"><Icon name="backlog" size={13} aria-hidden /> {t('research.linkedTopic')}</span>
            ) : null}
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
        {(canActivate || canComplete || canCancel || canConvert) && (
          <div className="rsc-head-actions">
            {canConvert && (
              <Button variant="primary" onClick={onConvert}>
                <Icon name="arrowRight" size={15} aria-hidden /> {t('research.convert.action')}
              </Button>
            )}
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

function RecommendationsSection({ m, pick, canAct, canManage, onAdd, onConvertRec }: {
  m: MissionDetail; pick: (l: LocalizedText | null) => string; canAct: boolean; canManage: boolean;
  onAdd: () => void; onConvertRec: (r: Recommendation) => void;
}) {
  const { t } = useTranslation();
  return (
    <SectionShell icon="checklist" title={t('research.recommendations')} count={m.recommendations.length} canAdd={canAct} onAdd={onAdd}>
      {m.recommendations.length === 0 ? (
        <p className="rsc-sec-empty">{t('research.noRecommendations')}</p>
      ) : (
        <ul className="rsc-items">
          {m.recommendations.map((r: Recommendation) => (
            <RecommendationCard key={r.id} r={r} missionId={m.id} pick={pick} canAct={canAct} canManage={canManage} onConvert={onConvertRec} />
          ))}
        </ul>
      )}
    </SectionShell>
  );
}

function RecommendationCard({ r, missionId, pick, canAct, canManage, onConvert }: {
  r: Recommendation; missionId: string; pick: (l: LocalizedText | null) => string;
  canAct: boolean; canManage: boolean; onConvert: (r: Recommendation) => void;
}) {
  const { t } = useTranslation();
  const setStatus = useSetRecommendationStatus();
  // Convert state is EDGE-driven (self-heals a failed best-effort status-mark): a recommendation with an
  // outgoing Informs→Topic edge IS converted, whatever the status flag says. Read only for accepted/converted
  // recs. ponytail: one small read per such rec — fine at ≤20 users; batch if a mission ever holds many.
  const needsEdge = r.status === 'Accepted' || r.status === 'Converted';
  const rels = useArtifactRelationships('Recommendation', needsEdge ? r.id : undefined);
  const topicEdge = rels.data?.outgoing.find((e) => e.otherType === 'Topic');
  const converted = !!topicEdge || r.status === 'Converted';
  const topicHref = topicEdge ? hrefFor('Topic', topicEdge.otherKey) : null;

  return (
    <li className="rsc-item">
      <span className="rsc-item-main">
        <span className="rsc-item-text">{pick(r.statement)}</span>
        <span className="rsc-item-meta">
          {t(`research.priority.${r.priority}`)}
          {r.rationale && <> · {pick(r.rationale)}</>}
        </span>
      </span>
      <span className="rsc-item-right">
        {converted ? (
          <>
            <StatusChip tone={recStatusTone('Converted')} label={t('research.recStatus.Converted')} size="sm" />
            {topicEdge && topicHref && (
              <Link className="rsc-xref-link" to={topicHref}>
                <span className="rsc-xref">{topicEdge.otherKey}</span>
                <Icon name="chevron" size={13} className="dir-flip" aria-hidden />
              </Link>
            )}
          </>
        ) : (
          <>
            <StatusChip tone={recStatusTone(r.status)} label={t(`research.recStatus.${r.status}`)} size="sm" />
            {canAct && r.status === 'Proposed' && (
              <span className="rsc-item-actions">
                <Button variant="ghost" size="sm" loading={setStatus.isPending} onClick={() => void setStatus.mutateAsync({ id: missionId, recommendationId: r.id, status: 'Accepted' })}>
                  <Icon name="check" size={13} aria-hidden /> {t('research.accept')}
                </Button>
                <Button variant="ghost" size="sm" loading={setStatus.isPending} onClick={() => void setStatus.mutateAsync({ id: missionId, recommendationId: r.id, status: 'Rejected' })}>
                  <Icon name="ban" size={13} aria-hidden /> {t('research.reject')}
                </Button>
              </span>
            )}
            {canManage && r.status === 'Accepted' && (
              <Button variant="ghost" size="sm" onClick={() => onConvert(r)}>
                <Icon name="arrowRight" size={13} aria-hidden /> {t('research.convert.recAction')}
              </Button>
            )}
          </>
        )}
      </span>
    </li>
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
