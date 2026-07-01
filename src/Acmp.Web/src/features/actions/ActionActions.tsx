/*
 * Action lifecycle controls (P8b2a, W14). The button row + transition dialog on the action detail.
 *
 * NO-REFERENCE COMPOSITION (guardrail #14): the "ACMP Lists & Registers.dc.html" Actions drawer is a
 * READ view — it draws no lifecycle buttons. So this row + dialogs are composed from the shared design
 * system (Button + the Confirmation/Destructive Dialog patterns in "ACMP Create Flows & Dialogs.dc.html")
 * and flagged in the progress log for a later design pass.
 *
 * Gating (docs/10 rows 14–15): Chairman/Secretary may manage any action; a Member may manage only actions
 * they OWN. Verify is separated (SoD-1, AC-012/013): the owner/completer never sees it — a person may not
 * verify their own work. This is UI convenience only; the API re-checks every transition and audits a
 * denied verify. Which buttons a status allows comes from ALLOWED_TRANSITIONS (mirrors the domain guards).
 *
 * Bilingual reason/note are entered once and MIRRORED to both columns (en === ar), the locked FTS pattern
 * (see SupersedeDialog). UI chrome stays fully EN/AR via i18n.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input, Textarea } from '../../components/ui/Field';
import { Icon, type IconName } from '../../components/icons';
import { useAuth } from '../../auth/AcmpAuthContext';
import { ApiError } from '../../api/apiClient';
import {
  useStartAction,
  useUnblockAction,
  useVerifyAction,
  useBlockAction,
  useCancelAction,
  useUpdateActionProgress,
  useCompleteAction,
  type ActionDetail,
  type LocalizedText,
} from '../../api/actions';
import { ALLOWED_TRANSITIONS, type ActionTransition } from './actionMeta';

type Variant = 'primary' | 'secondary' | 'danger';
const OP_ICON: Record<ActionTransition, IconName> = {
  start: 'activity', block: 'lock', unblock: 'refresh', progress: 'arrowUp',
  complete: 'checkCircle', verify: 'shieldUser', cancel: 'x',
};
const OP_VARIANT: Record<ActionTransition, Variant> = {
  start: 'primary', complete: 'primary', verify: 'primary', unblock: 'primary',
  block: 'secondary', progress: 'secondary', cancel: 'danger',
};
const OP_TONE: Record<ActionTransition, 'default' | 'warn' | 'danger'> = {
  start: 'default', unblock: 'default', progress: 'default', complete: 'default',
  block: 'warn', verify: 'warn', cancel: 'danger',
};

export function ActionActions({ action }: { action: ActionDetail }) {
  const { t } = useTranslation();
  const { roles, userId } = useAuth();
  const [op, setOp] = useState<ActionTransition | null>(null);

  const has = (r: string) => roles.includes(r as never);
  const isPrivileged = has('chairman') || has('secretary');
  const isOwner = !!userId && userId === action.ownerUserId;
  const isCompleter = !!userId && userId === action.completedByUserId;
  // Manage = drive the action's own progress. Verify = a DIFFERENT person confirms it (SoD-1).
  const canManage = isPrivileged || (has('member') && isOwner);
  const canVerify = (isPrivileged || has('member')) && !isOwner && !isCompleter;

  const ops = ALLOWED_TRANSITIONS[action.status].filter((o) => (o === 'verify' ? canVerify : canManage));
  if (ops.length === 0) return null;

  return (
    <>
      <div className="act-lifecycle" role="group" aria-label={t('actions.lifecycle')}>
        {ops.map((o) => (
          <Button key={o} variant={OP_VARIANT[o]} onClick={() => setOp(o)}>
            <Icon name={OP_ICON[o]} size={15} aria-hidden />
            {t(`actions.op.${o}`)}
          </Button>
        ))}
      </div>
      {op && <TransitionDialog key={op} op={op} action={action} onClose={() => setOp(null)} />}
    </>
  );
}

interface DialogProps {
  op: ActionTransition;
  action: ActionDetail;
  onClose: () => void;
}

function TransitionDialog({ op, action, onClose }: DialogProps) {
  const { t } = useTranslation();
  const start = useStartAction();
  const unblock = useUnblockAction();
  const verify = useVerifyAction();
  const block = useBlockAction();
  const cancel = useCancelAction();
  const progress = useUpdateActionProgress();
  const complete = useCompleteAction();

  const [reason, setReason] = useState('');
  const [note, setNote] = useState('');
  const [pct, setPct] = useState<number>(action.progressPct);
  const [error, setError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const needsReason = op === 'block' || op === 'cancel';
  const pending =
    start.isPending || unblock.isPending || verify.isPending || block.isPending ||
    cancel.isPending || progress.isPending || complete.isPending;

  // Mirror the typed text into both bilingual columns (en === ar) — keeps both populated for FTS.
  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  async function onConfirm() {
    setSubmitError(null);
    if (needsReason && !reason.trim()) {
      setError(t('actions.dlg.reasonErr'));
      return;
    }
    if (op === 'progress' && (!Number.isInteger(pct) || pct < 0 || pct > 100)) {
      setError(t('actions.dlg.progressErr'));
      return;
    }
    setError(null);
    const id = action.id;
    try {
      switch (op) {
        case 'start': await start.mutateAsync({ id }); break;
        case 'unblock': await unblock.mutateAsync({ id }); break;
        case 'verify': await verify.mutateAsync({ id }); break;
        case 'block': await block.mutateAsync({ id, reason: loc(reason) }); break;
        case 'cancel': await cancel.mutateAsync({ id, reason: loc(reason) }); break;
        case 'progress': await progress.mutateAsync({ id, progressPct: pct }); break;
        case 'complete': await complete.mutateAsync({ id, completionNote: note.trim() ? loc(note) : null }); break;
      }
      onClose();
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('actions.dlg.error') : t('actions.dlg.error'));
    }
  }

  return (
    <Dialog
      open
      onClose={onClose}
      tone={OP_TONE[op]}
      icon={<Icon name={OP_ICON[op]} size={20} aria-hidden />}
      title={t(`actions.dlg.title.${op}`)}
      description={t(`actions.dlg.body.${op}`)}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('actions.dlg.back')}</Button>
          <Button variant={op === 'cancel' ? 'danger' : 'primary'} loading={pending} onClick={() => void onConfirm()}>
            {t(`actions.op.${op}`)}
          </Button>
        </>
      }
    >
      <div className="act-dlg-body">
        {needsReason && (
          <Field label={t('actions.dlg.reason')} required error={error ?? undefined}>
            {(p) => (
              <Textarea {...p} rows={3} value={reason} placeholder={t('actions.dlg.reasonPh')} onChange={(e) => setReason(e.target.value)} />
            )}
          </Field>
        )}
        {op === 'complete' && (
          <Field label={t('actions.dlg.note')} help={t('actions.dlg.noteHelp')}>
            {(p) => (
              <Textarea {...p} rows={3} value={note} placeholder={t('actions.dlg.notePh')} onChange={(e) => setNote(e.target.value)} />
            )}
          </Field>
        )}
        {op === 'progress' && (
          <Field label={t('actions.dlg.progress')} required error={error ?? undefined}>
            {(p) => (
              <Input
                {...p}
                type="number"
                min={0}
                max={100}
                value={String(pct)}
                onChange={(e) => setPct(e.target.value === '' ? NaN : Number(e.target.value))}
              />
            )}
          </Field>
        )}
        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
