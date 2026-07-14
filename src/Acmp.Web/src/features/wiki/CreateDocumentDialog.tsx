/*
 * Create-document dialog (P15e, FR-116) — a no-reference composition (the wiki design draws no create
 * form): Title, Category (a Select of suggested spaces), Body (MarkdownEditor), and Tags (TokenInput).
 * Chairman/Secretary only; the API re-checks Document.Manage. Title + Body are entered once and MIRRORED
 * to both LocalizedString locales (en === ar, the locked pattern); Category and Tags are plain strings.
 * A new document is born Draft; on success we route to /wiki/:key.
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { TokenInput } from '../../components/ui/TokenInput';
import { MarkdownEditor } from '../../components/ui/MarkdownEditor';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useCreateDocument, type LocalizedText } from '../../api/wiki';
import { WIKI_CATEGORIES } from './wikiMeta';

interface Props {
  open: boolean;
  onClose: () => void;
}

export function CreateDocumentDialog({ open, onClose }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const create = useCreateDocument();

  const [title, setTitle] = useState('');
  const [category, setCategory] = useState(WIKI_CATEGORIES[0]);
  const [body, setBody] = useState('');
  const [tags, setTags] = useState<string[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim()) e.title = t('wiki.create.err.title');
    if (!body.trim()) e.body = t('wiki.create.err.body');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    try {
      const result = await create.mutateAsync({ title: loc(title), category, body: loc(body), tags });
      onClose();
      navigate(`/wiki/${result.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('wiki.create.error') : t('wiki.create.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="wiki" size={20} aria-hidden />}
      title={t('wiki.create.title')}
      description={t('wiki.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending} onClick={() => void onConfirm()}>
            {t('wiki.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="wiki-create-form">
        <Field label={t('wiki.create.titleField')} required error={errors.title}>
          {(p) => (
            <Input {...p} value={title} maxLength={512} placeholder={t('wiki.create.titlePh')} onChange={(e) => setTitle(e.target.value)} />
          )}
        </Field>

        <Field label={t('wiki.create.category')} required>
          {(p) => (
            <Select
              id={p.id}
              options={WIKI_CATEGORIES.map((c) => ({ value: c, label: t(`wiki.category.${c}`) }))}
              value={category}
              onChange={setCategory}
              ariaLabel={t('wiki.create.category')}
            />
          )}
        </Field>

        <Field label={t('wiki.create.body')} required error={errors.body}>
          {(p) => (
            <MarkdownEditor id={p.id} aria-invalid={p['aria-invalid']} aria-describedby={p['aria-describedby']} value={body} rows={8} placeholder={t('wiki.create.bodyPh')} onChange={setBody} />
          )}
        </Field>

        <Field label={t('wiki.create.tags')}>
          {(p) => (
            <TokenInput
              id={p.id}
              values={tags}
              onChange={setTags}
              placeholder={t('wiki.create.tagsPh')}
              ariaLabel={t('wiki.create.tags')}
              removeLabel={(v) => t('wiki.create.removeTag', { tag: v })}
            />
          )}
        </Field>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
