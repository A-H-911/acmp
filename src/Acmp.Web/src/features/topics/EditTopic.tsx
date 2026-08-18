/*
 * Edit a topic (AC-034) — the caller PUT /api/topics/{id} had never had. Before this the whole
 * UpdateTopicCommand was unreachable from the app: TopicDetail rendered a DISABLED "Edit" button
 * whose comment deferred this flow to "a follow-up slice" (DEC-045 / SC-010 chose to build it).
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = package):
 *  - THE DESIGN HAS NO EDIT SCREEN. "ACMP Backlog & Topic" specifies a submit form and a detail
 *    screen whose Edit button has no destination, so this is composed from the design's OWN submit
 *    form — same fieldsets, same order, same controls (Field/Input/MarkdownEditor/StreamPicker/
 *    TokenInput) — minus the parts that belong to submission alone: no type picker (type is
 *    reclassification, not an edit), no attachments dropzone (attachments have their own endpoint and
 *    tab), no draft autosave (this edits a real topic, so "save" is a server write).
 *  - ⚠ SCOPE IS A NO-REFERENCE COMPOSITION and is flagged as one (INV-014). No .dc.html shows a scope
 *    control anywhere. It is placed inside the design's own "Affected scope" fieldset, beside the
 *    streams it generalises, and rendered ONLY for Secretary/Chairman — elevating scope widens write
 *    access to every stream-bounded member (ADR-0043 clause 5), so it is a triage act.
 *  - AC-034's field-level locks are SHOWN, not just enforced: after Acceptance the content fields are
 *    disabled with a stated reason, and after a Decision the whole form is. Show-and-enforce — the
 *    aggregate refuses either way, and a disabled input is an explanation, not a control.
 */
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useTopicDetail, useUpdateTopic, useSetTopicConfidentiality, type TopicDetail } from '../../api/topics';
import { ApiError, localizedValidationMessage } from '../../api/apiClient';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { Field, Input, Textarea } from '../../components/ui/Field';
import { MarkdownEditor } from '../../components/ui/MarkdownEditor';
import { Button } from '../../components/ui/Button';
import { StreamPicker } from '../../components/ui/StreamPicker';
import { TokenInput } from '../../components/ui/TokenInput';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import './topics.css';

const URGENCIES = ['Normal', 'Urgent', 'Critical'];
const SCOPES = ['SingleStream', 'MultiStream', 'Platform', 'OrgWide'];
const MAX_TITLE = 120;

// entity-lifecycles.md §1 / AC-034: content freezes at Acceptance, metadata at a Decision.
const PRE_ACCEPT = ['Draft', 'Submitted', 'Triage', 'Reopened'];
const IMMUTABLE = ['Decided', 'Closed', 'Converted'];

interface FormState {
  title: string;
  description: string;
  justification: string;
  urgency: string;
  scope: string;
  streams: string[];
  systems: string[];
}

export function EditTopic() {
  const { key } = useParams();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data, isLoading, isError, error, refetch } = useTopicDetail(key);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound
          ? <EmptyState title={t('detail.notFoundTitle')} body={t('detail.notFoundBody')} />
          : <ErrorState onRetry={() => refetch()} />}
      </section>
    );
  }

  // Keyed on the topic id so the form state is rebuilt from scratch if the route changes topic —
  // deriving initial state in a child is what makes that a remount rather than a stale-props bug.
  return <EditForm key={data.id} topic={data} topicKey={key} onDone={() => navigate(`/topics/${key}`)} />;
}

function EditForm({ topic, topicKey, onDone }: { topic: TopicDetail; topicKey?: string; onDone: () => void }) {
  const { t } = useTranslation();
  const auth = useAuth();
  const update = useUpdateTopic(topicKey);
  const setConfidentiality = useSetTopicConfidentiality(topicKey);

  const [form, setForm] = useState<FormState>({
    title: topic.title,
    description: topic.description,
    justification: topic.justification,
    urgency: topic.urgency,
    scope: topic.scope,
    streams: [...topic.streams],
    systems: [...topic.systems],
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saveError, setSaveError] = useState<string | null>(null);

  const set = (patch: Partial<FormState>) => setForm((f) => ({ ...f, ...patch }));

  const contentLocked = !PRE_ACCEPT.includes(topic.status);
  const allLocked = IMMUTABLE.includes(topic.status);
  // Show-and-enforce: the server is the gate (Policies.TopicTriage), and it 403s a non-triage role
  // that sends a changed scope. Hiding the control keeps a Member from being offered a refusal.
  const mayChangeScope = hasRole(auth, 'secretary', 'chairman');
  // FR-163 / C-AUTHZ-04 (DEC-063 d2). Same two roles as the scope gate, and gated for the same
  // reason: the server refuses anyone else, and offering the control to a Member would be offering
  // them a refusal. ⚠ The OWNER is deliberately not included, even though they often recognise
  // sensitivity first — letting them classify would let a plain Member hide a topic from the committee.
  const mayClassify = hasRole(auth, 'secretary', 'chairman');

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.title.trim()) e.title = t('topicEdit.err.title');
    // Mirrors the server (DEF-059): a live topic must affect at least one stream, or stream scope
    // has nothing to bound it by.
    if (form.streams.length === 0) e.streams = t('topicEdit.err.streams');
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const onSave = async () => {
    setSaveError(null);
    if (!validate()) return;
    try {
      await update.mutateAsync({
        topicId: topic.id,
        edit: {
          title: form.title.trim(),
          description: form.description,
          justification: form.justification,
          urgency: form.urgency,
          streams: form.streams,
          systems: form.systems,
          tags: topic.tags,
          // Sent ONLY when changed — an unchanged scope must not cost the caller a triage check.
          ...(form.scope !== topic.scope ? { scope: form.scope } : {}),
        },
      });
      onDone();
    } catch (err) {
      setSaveError(err instanceof ApiError
        ? (localizedValidationMessage(err.problem) ?? t('topicEdit.saveError'))
        : t('topicEdit.saveError'));
    }
  };

  return (
    <section className="page sub-page">
      <header className="sub-head">
        <h1>{t('topicEdit.title', { key: topic.key })}</h1>
        <p className="sub-sub">{topic.title}</p>
      </header>

      {allLocked && (
        <p className="sub-locked" role="status">{t('topicEdit.lockedAll', { status: t(`topics.status.${topic.status}`) })}</p>
      )}

      <div className="sub-form">
        {/* ⚠ THE LOCK IS THE NATIVE `fieldset disabled`, not a per-control prop. It disables every
            control inside, including ones added later and ones whose own props expose no `disabled`
            — which is the failure this codebase keeps finding: a guard applied at each caller misses
            the next one. AC-034: content freezes at Acceptance. */}
        <fieldset className="sub-fieldset" disabled={contentLocked}>
          <legend className="sub-legend">{t('submit.sec.type')}</legend>
          {contentLocked && !allLocked && (
            <p className="sub-sub" role="status">{t('topicEdit.lockedContent')}</p>
          )}
          <Field label={t('submit.fTitle')} required error={errors.title} help={t('submit.fTitleHelp')}>
            {(p) => (
              <Input {...p} value={form.title} maxLength={MAX_TITLE} onChange={(e) => set({ title: e.target.value })} />
            )}
          </Field>
          <Field label={t('submit.fDescription')}>
            {(p) => (
              <MarkdownEditor {...p} rows={4} value={form.description} onChange={(description) => set({ description })} />
            )}
          </Field>
          <Field label={t('submit.fJustification')}>
            {(p) => (
              <Textarea {...p} rows={3} value={form.justification} onChange={(e) => set({ justification: e.target.value })} />
            )}
          </Field>
        </fieldset>

        {/* Metadata stays editable until a Decision (AC-034). */}
        <fieldset className="sub-fieldset" disabled={allLocked}>
          <legend className="sub-legend">{t('submit.sec.scope')}</legend>
          <Field label={t('submit.fStreams')} required error={errors.streams}>
            {(p) => (
              <StreamPicker
                id={p.id}
                ariaInvalid={p['aria-invalid']}
                describedby={p['aria-describedby']}
                values={form.streams}
                onChange={(streams) => set({ streams })}
                ariaLabel={t('submit.fStreams')}
              />
            )}
          </Field>
          <Field label={t('submit.fSystems')}>
            {(p) => (
              <TokenInput
                id={p.id}
                values={form.systems}
                onChange={(systems) => set({ systems })}
                placeholder={t('submit.fSystemsPh')}
                ariaLabel={t('submit.fSystems')}
                removeLabel={(v) => t('topics.removeFilter', { label: v })}
              />
            )}
          </Field>
          {mayChangeScope && (
            <Field label={t('topicEdit.fScope')} help={t('topicEdit.fScopeHelp')}>
              {(p) => (
                <div className="sub-cards sub-cards-3" id={p.id} role="group" aria-label={t('topicEdit.fScope')}>
                  {SCOPES.map((s) => (
                    <button
                      key={s}
                      type="button"
                      className={`sub-card ${form.scope === s ? 'selected' : ''}`}
                      aria-pressed={form.scope === s}
                      onClick={() => set({ scope: s })}
                    >
                      {t(`invariants.scope.${s}`)}
                    </button>
                  ))}
                </div>
              )}
            </Field>
          )}
          {/* ⚠ OUTSIDE the `disabled={allLocked}` semantics of the other fields by intent: this
              posts through its OWN endpoint rather than the topic PUT, because classification must
              stay possible on a Decided/Closed/Converted topic. An archived sensitive topic that
              could never be declassified is the failure the domain exemption exists to prevent. */}
          {mayClassify && (
            <Field label={t('topicEdit.fRestricted')} help={t('topicEdit.fRestrictedHelp')}>
              {(p) => (
                // ⚠ A TWO-CARD GROUP, NOT A LONE .sub-card. The visual check caught the first
                // version: .sub-card is sized by its .sub-cards grid parent, so standalone it
                // collapsed to 43px in RTL and wrapped its own label — the DW-031 shape, a class
                // reused outside the context it was built for. This is the same segmented pattern
                // the scope and urgency pickers use, and it reads better besides: two explicit
                // options beat one toggle whose label changes under you.
                // ⚠ `.sub-cards` ALONE — it is already a 2-column grid. `.sub-cards-2` does not
                // exist, and an undefined class does nothing silently, which is the same failure
                // mode as the undefined custom property DW-031 records. Checked before writing it.
                <div className="sub-cards" id={p.id} role="group" aria-label={t('topicEdit.fRestricted')}>
                  {/* ⚠ COERCED once, here. With `restricted` absent, `restricted === v` is false for
                      BOTH cards and the control renders with no selection at all — a segmented
                      picker that shows nothing selected reads as broken, not as "unset". Same
                      failure family as the aria-pressed omission this replaced. */}
                  {[false, true].map((v) => (
                    <button
                      key={String(v)}
                      type="button"
                      className={`sub-card ${(topic.restricted ?? false) === v ? 'selected' : ''}`}
                      aria-pressed={(topic.restricted ?? false) === v}
                      disabled={setConfidentiality.isPending}
                      onClick={() => setConfidentiality.mutate({ topicId: topic.id, restricted: v })}
                    >
                      {/* ⚠ NO ICON. .sub-card is flex-direction: column, so a child icon pushes
                          the label onto a second line — which is exactly how it rendered before the
                          visual check. The scope and urgency pickers are text-only for the same
                          reason; the lock icon lives on the DETAIL badge, where it belongs. */}
                      {v ? t('topicEdit.restrictedOn') : t('topicEdit.restrictedOff')}
                    </button>
                  ))}
                </div>
              )}
            </Field>
          )}
        </fieldset>

        <fieldset className="sub-fieldset sub-fieldset-last" disabled={allLocked}>
          <legend className="sub-legend">{t('submit.sec.urgency')}</legend>
          <div className="sub-cards sub-cards-3" role="group" aria-label={t('topics.filter.urgency')}>
            {URGENCIES.map((u) => (
              <button
                key={u}
                type="button"
                className={`sub-card sub-urg urg-${u.toLowerCase()} ${form.urgency === u ? 'selected' : ''}`}
                aria-pressed={form.urgency === u}
                onClick={() => set({ urgency: u })}
              >
                {t(`topics.urgency.${u}`)}
              </button>
            ))}
          </div>
        </fieldset>

        <div className="sub-actions">
          {saveError && <span className="sub-error" role="alert">{saveError}</span>}
          <Button variant="secondary" onClick={onDone}>{t('topicEdit.cancel')}</Button>
          <Button onClick={onSave} loading={update.isPending} disabled={allLocked}>
            {t('topicEdit.save')}
          </Button>
        </div>
      </div>
    </section>
  );
}
