/*
 * Submit a topic (P5b, W1) — matches the "ACMP Backlog & Topic" design (submit screen).
 * Composes the shared library (Breadcrumb, Field/Input/Textarea, Button, Dialog, Icon).
 * Wired to POST /api/topics (+ per-file POST /{id}/attachments on success).
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = package):
 *  - No Scope/Source picker — Scope is derived server-side and Source defaults to CommitteeMember
 *    (P5a decision); the form sends `source` and omits scope.
 *  - 4 topic types (canonical taxonomy) and Urgency Normal/Urgent/Critical — not the design's 3 + "low".
 *  - Description is a plain textarea — the design's rich-text toolbar is mock chrome; we store plain text.
 *  - Streams & systems are free-text token inputs (no committed stream registry in the web yet) rather than
 *    the design's fixed stream toggle-chips; revisit when a streams endpoint exists.
 *  - Autosave persists the draft to localStorage (there is no server draft endpoint in P5); the indicator
 *    and "Save draft" reflect that. The unsaved-work guard (AC-047 useBlocker + AC-048 beforeunload) warns
 *    before leaving an unsubmitted topic — the draft is kept on the device either way.
 *  - The left section nav scrolls to each fieldset (single scrollable form), not a multi-step wizard.
 */
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useBlocker } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useSubmitTopic, uploadTopicAttachment } from '../../api/topics';
import { ApiError } from '../../api/apiClient';
import { AREAS } from '../../nav/navModel';
import { Breadcrumb } from '../../components/ui/Breadcrumb';
import { Field, Input, Textarea } from '../../components/ui/Field';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { Icon, type IconName } from '../../components/icons';
import './topics.css';

const TYPES: { v: string; icon: IconName }[] = [
  { v: 'ResearchDiscovery', icon: 'research' },
  { v: 'ArchitectureDecision', icon: 'decision' },
  { v: 'EnhancementInnovation', icon: 'plus' },
  { v: 'GovernanceStandardization', icon: 'audit' },
];
const URGENCIES = ['Normal', 'Urgent', 'Critical'];
const STEPS = ['type', 'justification', 'scope', 'attachments', 'urgency'];
const MAX_TITLE = 120;
const MAX_FILE_BYTES = 50 * 1024 * 1024;
const DRAFT_KEY = 'acmp-topic-draft-v1';
const SOURCE_DEFAULT = 'CommitteeMember';

interface FormState {
  type: string;
  title: string;
  description: string;
  justification: string;
  streams: string[];
  systems: string[];
  urgency: string;
}
const EMPTY: FormState = { type: '', title: '', description: '', justification: '', streams: [], systems: [], urgency: 'Normal' };

function loadDraft(): FormState | null {
  try {
    const raw = localStorage.getItem(DRAFT_KEY);
    return raw ? { ...EMPTY, ...(JSON.parse(raw) as Partial<FormState>) } : null;
  } catch {
    return null;
  }
}

function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(0)} KB`;
  return `${(n / 1024 / 1024).toFixed(1)} MB`;
}

export function SubmitTopic() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const submit = useSubmitTopic();

  const [form, setForm] = useState<FormState>(() => loadDraft() ?? EMPTY);
  const [files, setFiles] = useState<File[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [fileError, setFileError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [saveState, setSaveState] = useState<'idle' | 'saving' | 'saved'>(loadDraft() ? 'saved' : 'idle');
  const [activeStep, setActiveStep] = useState('type');

  const update = (patch: Partial<FormState>) => setForm((f) => ({ ...f, ...patch }));

  const hasContent =
    !!(form.type || form.title || form.description || form.justification || form.streams.length || form.systems.length || files.length);

  // `leaving` (a ref read by the blocker, plus state for re-render) suppresses the guard for our own
  // programmatic navigation after submit / save-draft.
  const [leaving, setLeaving] = useState(false);
  const dirtyRef = useRef(false);
  dirtyRef.current = hasContent && !leaving;
  const bypassRef = useRef(false);

  // Debounced autosave to localStorage (no server draft endpoint in P5).
  useEffect(() => {
    if (leaving) return;
    setSaveState('saving');
    const id = setTimeout(() => {
      try {
        localStorage.setItem(DRAFT_KEY, JSON.stringify(form));
        setSaveState('saved');
      } catch {
        /* storage full / unavailable — keep editing, just don't show "saved" */
      }
    }, 600);
    return () => clearTimeout(id);
  }, [form, leaving]);

  // AC-048: native guard on reload / tab-close / hard navigation.
  useEffect(() => {
    if (!dirtyRef.current) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = '';
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [hasContent, leaving]);

  // Scroll-spy: keep the section nav's active item synced to the fieldset in view as the user scrolls or
  // fills the form (the design's nav shows active + "done" progress; the static mock can't drive it). Guarded
  // so jsdom (no IntersectionObserver) is a no-op. IntersectionObserver, not a scroll handler (web perf rule).
  useEffect(() => {
    if (typeof IntersectionObserver === 'undefined') return;
    const els = STEPS.map((s) => document.getElementById(`sec-${s}`)).filter((el): el is HTMLElement => !!el);
    if (els.length === 0) return;
    // Track which sections are in the active band across callbacks (IO entries are deltas, not the full
    // set), so scrolling up re-adds upper sections and scrolling down removes them — symmetric both ways.
    const visible = new Set<string>();
    const obs = new IntersectionObserver(
      (entries) => {
        for (const e of entries) {
          const id = e.target.id.replace('sec-', '');
          if (e.isIntersecting) visible.add(id);
          else visible.delete(id);
        }
        const current = STEPS.find((s) => visible.has(s)); // topmost in document order
        if (current) setActiveStep(current);
      },
      { rootMargin: '-90px 0px -55% 0px' },
    );
    els.forEach((el) => obs.observe(el));
    return () => obs.disconnect();
  }, []);

  // AC-047: in-app route-change guard (data router). Reads refs so the condition is always current.
  const blocker = useBlocker(
    useCallback(
      ({ currentLocation, nextLocation }) =>
        !bypassRef.current && dirtyRef.current && currentLocation.pathname !== nextLocation.pathname,
      [],
    ),
  );

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!form.type) e.type = t('submit.err.type');
    if (!form.title.trim()) e.title = t('submit.err.title');
    else if (form.title.length > MAX_TITLE) e.title = t('submit.err.titleLong');
    if (!form.description.trim()) e.description = t('submit.err.description');
    if (!form.justification.trim()) e.justification = t('submit.err.justification');
    if (form.streams.length === 0) e.streams = t('submit.err.streams');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  function leaveTo(path: string) {
    bypassRef.current = true;
    setLeaving(true);
    navigate(path);
  }

  async function onSubmit() {
    setSubmitError(null);
    if (!validate()) return;
    try {
      const res = await submit.mutateAsync({ ...form, source: SOURCE_DEFAULT, tags: [] });
      for (const file of files) {
        await uploadTopicAttachment(res.id, file).catch(() => {
          /* a failed attachment shouldn't lose the created topic; detail-screen upload retry → PR3 */
        });
      }
      localStorage.removeItem(DRAFT_KEY);
      leaveTo(`/topics/${res.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('submit.submitError') : t('submit.submitError'));
    }
  }

  function saveDraftAndLeave() {
    try {
      localStorage.setItem(DRAFT_KEY, JSON.stringify(form));
    } catch {
      /* ignore */
    }
    leaveTo(AREAS.backlog.path);
  }

  function goSection(step: string) {
    setActiveStep(step);
    document.getElementById(`sec-${step}`)?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
  }

  function addFiles(list: FileList | null) {
    if (!list) return;
    const next: File[] = [];
    let rejected = false;
    for (const f of Array.from(list)) {
      if (f.size > MAX_FILE_BYTES) rejected = true;
      else next.push(f);
    }
    setFileError(rejected ? t('submit.err.fileSize', { max: 50 }) : null);
    if (next.length) setFiles((prev) => [...prev, ...next]);
  }

  return (
    <section className="page">
      <Breadcrumb
        ariaLabel={t('topics.newTopic')}
        items={[{ label: t('topics.backlog'), href: AREAS.backlog.path }, { label: t('topics.newTopic'), current: true }]}
      />

      <div className="bk-head sub-head">
        <div>
          <h1 className="page-title">{t('submit.title')}</h1>
          <div className="bk-head-sub">{t('submit.subtitle')}</div>
        </div>
        <div className={`sub-save sub-save-${saveState}`} aria-live="polite">
          <Icon name={saveState === 'saved' ? 'checkCircle' : 'infoCircle'} size={14} aria-hidden />
          {saveState === 'saved' ? t('submit.saved') : t('submit.saving')}
        </div>
      </div>

      <div className="sub-layout">
        <nav className="sub-nav" aria-label={t('submit.sectionsLabel')}>
          {STEPS.map((s, i) => {
            const done = i < STEPS.indexOf(activeStep);
            return (
              <button
                key={s}
                type="button"
                className={`sub-nav-item ${activeStep === s ? 'active' : ''}`}
                aria-current={activeStep === s ? 'step' : undefined}
                onClick={() => goSection(s)}
              >
                <span className={`sub-nav-num ${done ? 'done' : ''}`} aria-hidden="true">
                  {done ? <Icon name="check" size={11} /> : i + 1}
                </span>
                {t(`submit.sec.${s}`)}
              </button>
            );
          })}
        </nav>

        <form
          className="sub-form"
          onSubmit={(e) => {
            e.preventDefault();
            void onSubmit();
          }}
        >
          {/* 1. Type & title */}
          <fieldset id="sec-type" className="sub-fieldset">
            <legend className="sub-legend">{t('submit.sec.type')}</legend>
            <p className="sub-sub">{t('submit.sec.typeHelp')}</p>
            <div className="sub-cards" role="group" aria-label={t('submit.fType')}>
              {TYPES.map((ty) => (
                <button
                  key={ty.v}
                  type="button"
                  className={`sub-card ${form.type === ty.v ? 'selected' : ''}`}
                  aria-pressed={form.type === ty.v}
                  onClick={() => update({ type: ty.v })}
                >
                  <Icon name={ty.icon} size={18} aria-hidden />
                  <span className="sub-card-title">{t(`topics.type.${ty.v}`)}</span>
                  <span className="sub-card-desc">{t(`submit.typeDesc.${ty.v}`)}</span>
                </button>
              ))}
            </div>
            {errors.type && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{errors.type}</p>}
            <div className="sub-mt">
              <Field label={t('submit.fTitle')} required error={errors.title}>
                {(p) => (
                  <>
                    <Input
                      {...p}
                      aria-describedby={[p['aria-describedby'], 'sub-title-hint'].filter(Boolean).join(' ') || undefined}
                      value={form.title}
                      maxLength={MAX_TITLE}
                      placeholder={t('submit.fTitlePh')}
                      onChange={(e) => update({ title: e.target.value })}
                    />
                    {/* hint (start) + char counter (end) on one justified row, per the design */}
                    <div className="sub-title-foot">
                      <span className="sub-hint" id="sub-title-hint">{t('submit.fTitleHelp')}</span>
                      <span className="sub-count">{form.title.length}/{MAX_TITLE}</span>
                    </div>
                  </>
                )}
              </Field>
            </div>
          </fieldset>

          {/* 2. Justification */}
          <fieldset id="sec-justification" className="sub-fieldset">
            <legend className="sub-legend">{t('submit.sec.justification')}</legend>
            <p className="sub-sub">{t('submit.sec.justificationHelp')}</p>
            <Field label={t('submit.fDescription')} required error={errors.description}>
              {(p) => (
                <Textarea
                  {...p}
                  rows={4}
                  value={form.description}
                  placeholder={t('submit.fDescriptionPh')}
                  onChange={(e) => update({ description: e.target.value })}
                />
              )}
            </Field>
            <Field label={t('submit.fJustification')} required error={errors.justification}>
              {(p) => (
                <Textarea
                  {...p}
                  rows={3}
                  value={form.justification}
                  placeholder={t('submit.fJustificationPh')}
                  onChange={(e) => update({ justification: e.target.value })}
                />
              )}
            </Field>
          </fieldset>

          {/* 3. Scope */}
          <fieldset id="sec-scope" className="sub-fieldset">
            <legend className="sub-legend">{t('submit.sec.scope')}</legend>
            <p className="sub-sub">{t('submit.sec.scopeHelp')}</p>
            <Field label={t('submit.fStreams')} required error={errors.streams}>
              {(p) => (
                <TokenInput
                  id={p.id}
                  ariaInvalid={p['aria-invalid']}
                  describedby={p['aria-describedby']}
                  values={form.streams}
                  onChange={(streams) => update({ streams })}
                  placeholder={t('submit.fStreamsPh')}
                  ariaLabel={t('submit.fStreams')}
                  removeLabel={(v) => t('topics.removeFilter', { label: v })}
                />
              )}
            </Field>
            <Field label={t('submit.fSystems')}>
              {(p) => (
                <TokenInput
                  id={p.id}
                  values={form.systems}
                  onChange={(systems) => update({ systems })}
                  placeholder={t('submit.fSystemsPh')}
                  ariaLabel={t('submit.fSystems')}
                  removeLabel={(v) => t('topics.removeFilter', { label: v })}
                />
              )}
            </Field>
          </fieldset>

          {/* 4. Attachments */}
          <fieldset id="sec-attachments" className="sub-fieldset">
            <legend className="sub-legend">{t('submit.sec.attachments')}</legend>
            <p className="sub-sub">{t('submit.sec.attachmentsHelp')}</p>
            <FileDrop onFiles={addFiles} hint={t('submit.dropHint')} label={t('submit.dropFiles')} />
            {fileError && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{fileError}</p>}
            {files.length > 0 && (
              <ul className="sub-files">
                {files.map((f, i) => (
                  <li key={`${f.name}-${i}`} className="sub-file">
                    <span className="sub-file-ic" aria-hidden="true"><Icon name="doc" size={15} /></span>
                    <span className="sub-file-main">
                      <span className="sub-file-name">{f.name}</span>
                      <span className="sub-file-meta">{formatBytes(f.size)}</span>
                    </span>
                    <button
                      type="button"
                      className="sub-file-rm"
                      aria-label={t('topics.removeFilter', { label: f.name })}
                      onClick={() => setFiles((prev) => prev.filter((_, j) => j !== i))}
                    >
                      <Icon name="x" size={15} aria-hidden />
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </fieldset>

          {/* 5. Urgency */}
          <fieldset id="sec-urgency" className="sub-fieldset sub-fieldset-last">
            <legend className="sub-legend">{t('submit.sec.urgency')}</legend>
            <p className="sub-sub">{t('submit.sec.urgencyHelp')}</p>
            <div className="sub-cards sub-cards-3" role="group" aria-label={t('topics.filter.urgency')}>
              {URGENCIES.map((u) => (
                <button
                  key={u}
                  type="button"
                  className={`sub-card sub-urg urg-${u.toLowerCase()} ${form.urgency === u ? 'selected' : ''}`}
                  aria-pressed={form.urgency === u}
                  onClick={() => update({ urgency: u })}
                >
                  <span className={`sub-urg-dot urg-${u.toLowerCase()}`} aria-hidden="true" />
                  <span className="sub-card-title">{t(`topics.urgency.${u}`)}</span>
                  <span className="sub-card-desc">{t(`submit.urgencyDesc.${u}`)}</span>
                </button>
              ))}
            </div>
          </fieldset>

          <div className="sub-foot">
            <span className="sub-foot-note">
              <Icon name="infoCircle" size={14} aria-hidden /> {t('submit.autosaveNote')}
            </span>
            {submitError && <span className="field-error sub-foot-err" role="alert">{submitError}</span>}
            <div className="sub-foot-actions">
              <Button type="button" variant="secondary" onClick={saveDraftAndLeave}>{t('submit.saveDraft')}</Button>
              <Button type="submit" loading={submit.isPending}>{t('submit.submit')}</Button>
            </div>
          </div>
        </form>
      </div>

      <Dialog
        open={blocker.state === 'blocked'}
        onClose={() => blocker.reset?.()}
        tone="warn"
        icon={<Icon name="warnTriangle" size={20} aria-hidden />}
        title={t('submit.guardTitle')}
        description={t('submit.guardBody')}
        footer={
          <>
            <Button variant="secondary" onClick={() => blocker.reset?.()}>{t('submit.keepEditing')}</Button>
            <Button variant="danger" onClick={() => blocker.proceed?.()}>{t('submit.leave')}</Button>
          </>
        }
      />
    </section>
  );
}

interface TokenInputProps {
  values: string[];
  onChange: (v: string[]) => void;
  placeholder: string;
  ariaLabel: string;
  removeLabel: (v: string) => string;
  id?: string;
  ariaInvalid?: true;
  describedby?: string;
}

/** Free-text token input (Enter/comma adds; Backspace on empty removes the last). */
function TokenInput({ values, onChange, placeholder, ariaLabel, removeLabel, id, ariaInvalid, describedby }: TokenInputProps) {
  const [draft, setDraft] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);
  const add = () => {
    const v = draft.trim();
    if (v && !values.includes(v)) onChange([...values, v]);
    setDraft('');
  };
  return (
    <div className="tokens-field" onClick={() => inputRef.current?.focus()}>
      {values.map((v) => (
        <span className="token" key={v}>
          {v}
          <button type="button" className="token-remove" aria-label={removeLabel(v)} onClick={(e) => { e.stopPropagation(); onChange(values.filter((x) => x !== v)); }}>
            <Icon name="x" size={13} aria-hidden />
          </button>
        </span>
      ))}
      <input
        ref={inputRef}
        id={id}
        className="tokens-input"
        aria-label={ariaLabel}
        aria-invalid={ariaInvalid}
        aria-describedby={describedby}
        value={draft}
        placeholder={values.length === 0 ? placeholder : undefined}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ',') {
            e.preventDefault();
            add();
          } else if (e.key === 'Backspace' && !draft && values.length) {
            onChange(values.slice(0, -1));
          }
        }}
        onBlur={add}
      />
    </div>
  );
}

function FileDrop({ onFiles, label, hint }: { onFiles: (l: FileList | null) => void; label: string; hint: string }) {
  const ref = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  return (
    <div
      className={`sub-drop ${over ? 'over' : ''}`}
      onDragOver={(e) => { e.preventDefault(); setOver(true); }}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => { e.preventDefault(); setOver(false); onFiles(e.dataTransfer.files); }}
    >
      <input ref={ref} type="file" multiple aria-label={label} className="visually-hidden" onChange={(e) => onFiles(e.target.files)} />
      <div className="sub-drop-ic" aria-hidden="true"><Icon name="upload" size={19} /></div>
      <button type="button" className="sub-drop-btn" onClick={() => ref.current?.click()}>{label}</button>
      <div className="sub-drop-hint">{hint}</div>
    </div>
  );
}
