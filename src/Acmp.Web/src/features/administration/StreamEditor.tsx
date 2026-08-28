/*
 * Administration → Streams, write path (WBS-24.7 / DW-063 / NFR-010).
 *
 * NFR-010 has two clauses and only one of them held. There is no hard-coded stream limit anywhere —
 * that was verified, not assumed — but the taxonomy was seeded by raw SQL inside a migration and
 * Stream.Create had NO CALLER, so adding a sixth stream meant a code change and a deployment. This
 * dialog is the missing caller, and it is what makes "stream count is configuration-driven" true.
 *
 * INV-014. The design reference (`ACMP Administration.dc.html`) gives this section the primary action
 * `Add stream` / `إضافة مسار` with a plus glyph, which is what the button below reproduces. ⚠ The
 * design places that action in the PAGE header; this page has no primary-action slot for any of its
 * six tabs, so it is rendered at the head of the streams body instead. That is a deliberate,
 * recorded divergence rather than an oversight — adding a header-action channel for one tab would
 * leave the other five inconsistent with both the design and each other.
 *
 * ⚠ THE CODE IS A SCOPE KEY, NOT A LABEL, and the form says so out loud. Topics carry stream codes
 * and the ABAC intersect resolves on them, so it is offered ONCE at creation and is absent from the
 * rename path entirely — the API cannot even express a re-code (see useRenameStream).
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useCreateStream, useRenameStream, type StreamRef } from '../../api/members';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { Field, Input } from '../../components/ui/Field';
import { Icon } from '../../components/icons';

/** Mirrors CreateStreamValidator's pattern so the refusal is explained here, not just server-side. */
const CODE_PATTERN = /^[a-zA-Z0-9][a-zA-Z0-9-]*$/;

interface StreamFormState {
  code: string;
  nameEn: string;
  nameAr: string;
}

const EMPTY: StreamFormState = { code: '', nameEn: '', nameAr: '' };

export function AddStreamButton() {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<StreamFormState>(EMPTY);
  const [serverError, setServerError] = useState<string | null>(null);
  const create = useCreateStream();

  const codeError =
    form.code.trim() === ''
      ? undefined
      : CODE_PATTERN.test(form.code.trim())
        ? undefined
        : t('admin.streams.form.codeInvalid');

  // Both language halves are required: guardrail 9 forbids a single-language user-facing string, and
  // the server's column is NOT NULL, so an omitted Arabic name is refused there too.
  const canSubmit =
    form.code.trim() !== '' && form.nameEn.trim() !== '' && form.nameAr.trim() !== '' && !codeError;

  function close() {
    setOpen(false);
    setForm(EMPTY);
    setServerError(null);
  }

  async function submit() {
    setServerError(null);
    try {
      await create.mutateAsync({
        code: form.code.trim(),
        nameEn: form.nameEn.trim(),
        nameAr: form.nameAr.trim(),
      });
      close();
    } catch {
      // The duplicate-code refusal is the one an administrator will actually meet, and it must be
      // legible rather than a silently-closed dialog that appeared to work.
      setServerError(t('admin.streams.form.saveFailed'));
    }
  }

  return (
    <>
      <div className="adm-streams-actions">
        <Button variant="primary" onClick={() => setOpen(true)}>
          <Icon name="plus" size={16} aria-hidden />
          {t('admin.streams.add')}
        </Button>
      </div>

      <Dialog
        open={open}
        onClose={close}
        icon={<Icon name="stream" size={20} aria-hidden />}
        title={t('admin.streams.form.addTitle')}
        description={t('admin.streams.form.addSubtitle')}
        footer={
          <>
            <Button variant="secondary" onClick={close}>
              {t('common.cancel')}
            </Button>
            <Button variant="primary" disabled={!canSubmit} loading={create.isPending} onClick={() => void submit()}>
              {t('admin.streams.form.addConfirm')}
            </Button>
          </>
        }
      >
        <div className="adm-stream-form">
          <Field
            label={t('admin.streams.col.code')}
            required
            help={t('admin.streams.form.codeHelp')}
            error={codeError ?? serverError ?? undefined}
          >
            {(p) => (
              <Input
                {...p}
                value={form.code}
                maxLength={64}
                // ⚠ dir="ltr" is REQUIRED, not cosmetic (WBS-24.3 / WBS-24.5's bidi finding). A code
                // like `shared-services` is ASCII with a NEUTRAL hyphen, and neutral characters in an
                // RTL paragraph are reordered — the same class that rendered {"years":7} as {years":7"}
                // in the retention panel. unicode-bidi: plaintext does NOT help here: it takes its
                // direction from the first strong character and an ASCII slug has none.
                dir="ltr"
                onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
              />
            )}
          </Field>

          <Field label={t('admin.streams.form.nameEn')} required>
            {(p) => (
              <Input
                {...p}
                value={form.nameEn}
                maxLength={128}
                dir="ltr"
                onChange={(e) => setForm((f) => ({ ...f, nameEn: e.target.value }))}
              />
            )}
          </Field>

          <Field label={t('admin.streams.form.nameAr')} required>
            {(p) => (
              <Input
                {...p}
                value={form.nameAr}
                maxLength={128}
                dir="rtl"
                onChange={(e) => setForm((f) => ({ ...f, nameAr: e.target.value }))}
              />
            )}
          </Field>
        </div>
      </Dialog>
    </>
  );
}

/**
 * Per-row rename. The code is shown but not editable, so the screen states the same rule the API
 * enforces instead of leaving an administrator to discover it by having a change refused.
 */
export function RenameStreamButton({ stream }: { stream: StreamRef }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [nameEn, setNameEn] = useState(stream.nameEn);
  const [nameAr, setNameAr] = useState(stream.nameAr);
  const [serverError, setServerError] = useState<string | null>(null);
  const rename = useRenameStream();

  function open_() {
    setNameEn(stream.nameEn);
    setNameAr(stream.nameAr);
    setServerError(null);
    setOpen(true);
  }

  async function submit() {
    setServerError(null);
    try {
      await rename.mutateAsync({ publicId: stream.publicId, nameEn: nameEn.trim(), nameAr: nameAr.trim() });
      setOpen(false);
    } catch {
      setServerError(t('admin.streams.form.saveFailed'));
    }
  }

  return (
    <>
      <Button
        variant="ghost"
        size="sm"
        onClick={open_}
        aria-label={t('admin.streams.form.renameAria', { name: stream.nameEn })}
      >
        {t('admin.streams.form.renameAction')}
      </Button>

      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        icon={<Icon name="stream" size={20} aria-hidden />}
        title={t('admin.streams.form.renameTitle')}
        description={t('admin.streams.form.renameSubtitle', { code: stream.code })}
        footer={
          <>
            <Button variant="secondary" onClick={() => setOpen(false)}>
              {t('common.cancel')}
            </Button>
            <Button
              variant="primary"
              disabled={nameEn.trim() === '' || nameAr.trim() === ''}
              loading={rename.isPending}
              onClick={() => void submit()}
            >
              {t('admin.streams.form.renameConfirm')}
            </Button>
          </>
        }
      >
        <div className="adm-stream-form">
          <Field label={t('admin.streams.form.nameEn')} required error={serverError ?? undefined}>
            {(p) => (
              <Input {...p} value={nameEn} maxLength={128} dir="ltr" onChange={(e) => setNameEn(e.target.value)} />
            )}
          </Field>
          <Field label={t('admin.streams.form.nameAr')} required>
            {(p) => (
              <Input {...p} value={nameAr} maxLength={128} dir="rtl" onChange={(e) => setNameAr(e.target.value)} />
            )}
          </Field>
        </div>
      </Dialog>
    </>
  );
}
