/*
 * Administration → Retention (WBS-24.5 / DW-036; FR-155, NFR-059, NFR-060; DEC-080 / SC-035).
 *
 * ⚠⚠ NO-REFERENCE COMPOSITION, FLAGGED AS ONE (INV-014, CLAUDE.md). "ACMP Administration.dc.html"
 * draws exactly five tabs and the app implements those five; retention is not among them. This screen
 * is composed from the shared design system and the tabs that already exist — it follows
 * StreamsReference's and RolesReference's "canonical read-only reference" shape rather than inventing
 * a layout — and is recorded as a divergence rather than presented as design fidelity.
 *
 * ⚠ WHAT THIS SCREEN MUST NOT IMPLY. Nothing purges. NFR-059/060 require "no automatic purge in v1",
 * SEC-089 places enforcement in Phase 2, and the server reports `automaticPurgeEnabled` as a CONSTANT
 * rather than a setting, so there is nothing here to switch on. A screen showing a retention period
 * without saying that would be a blind control: a reader would take a stored number for an enforced
 * one. The posture is stated first, in plain words, above anything configurable.
 *
 * ⚠ v1 SHIPS NO PERIODS. SEC-080 says periods are "configurable but unset in v1" and OQ-DATA-004
 * leaves the values to legal, so the empty state is the CORRECT state and says so, rather than reading
 * as a screen that failed to load.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useRetentionPolicy, useSetRetentionSetting, type RetentionSetting } from '../../api/retention';
import { StatusChip } from '../../components/ui/StatusChip';
import { Table, type Column } from '../../components/ui/Table';
import { Field } from '../../components/ui/Field';
import { EmptyState, ErrorState, LoadingState } from '../../components/states';

/*
 * The worked example. These are IDENTIFIERS, not copy: a configuration key and a JSON value read the
 * same in every locale, so they are not translatable and do not belong behind t() (NFR-035). Hoisting
 * them says so, keeps the example and the placeholders from drifting apart, and stops
 * check-hardcoded-strings flagging a literal it cannot tell from display text.
 */
const EXAMPLE_KEY = 'retention.topic.years';
const EXAMPLE_VALUE = '{"years":7}';

export function RetentionSettings() {
  const { t } = useTranslation();
  const { data, isLoading, isError, refetch } = useRetentionPolicy();
  const save = useSetRetentionSetting();
  const [key, setKey] = useState('');
  const [value, setValue] = useState('');

  if (isLoading) return <LoadingState />;
  if (isError || !data) return <ErrorState onRetry={() => void refetch()} />;

  const columns: Column<RetentionSetting>[] = [
    /*
     * ⚠ dir="ltr" ON EVERY CODE-LIKE CELL, AND IT IS NOT COSMETIC. A key and a JSON value are made
     * almost entirely of NEUTRAL characters — braces, quotes, colons, digits — which take the
     * paragraph's direction under the bidi algorithm. In Arabic that renders {"years":7} as
     * {years":7"}: the quote migrates. WBS-24.3 hit the identical fault in the wiki diff and its
     * lesson named "a config panel" as the next place it would appear. It did.
     * ⚠ `unicode-bidi: plaintext` is NOT the fix here, unlike in the diff: plaintext takes direction
     * from the first STRONG character and a JSON fragment has none, so it falls back to RTL again.
     */
    { id: 'key', header: t('admin.retention.col.key'), cell: (r) => <code dir="ltr">{r.key}</code> },
    { id: 'value', header: t('admin.retention.col.value'), cell: (r) => <code dir="ltr">{r.valueJson}</code> },
  ];

  const canSave = key.trim().length > 0 && value.trim().length > 0 && !save.isPending;

  return (
    <div className="adm-section">
      {/* The posture, stated BEFORE anything configurable — see the header for why. */}
      <div className="adm-detail-card adm-card-overflow">
        <h2>{t('admin.retention.postureTitle')}</h2>
        <p>{t('admin.retention.postureBody')}</p>
        <StatusChip
          tone={data.automaticPurgeEnabled ? 'warn' : 'success'}
          label={t(data.automaticPurgeEnabled ? 'admin.retention.purgeOn' : 'admin.retention.purgeOff')}
        />
      </div>

      {data.settings.length === 0 ? (
        <EmptyState
          icon="shieldUser"
          title={t('admin.retention.emptyTitle')}
          body={t('admin.retention.emptyBody')}
        />
      ) : (
        <Table
          caption={t('admin.retention.tableCaption')}
          columns={columns}
          rows={data.settings}
          getRowKey={(r) => r.key}
        />
      )}

      <form
        className="adm-detail-card adm-card-overflow"
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate({ key: key.trim(), valueJson: value.trim() });
        }}
      >
        <h2>{t('admin.retention.setTitle')}</h2>
        {/* The SERVER owns the real rules — the `retention.` prefix and well-formed JSON. This states
            them so a refusal is predictable, but deliberately does NOT re-implement them: the endpoint
            is what refuses, and a client-side copy would drift from it silently. */}
        <p>{t('admin.retention.setHint')}</p>
        {/* The example lives OUTSIDE the sentence: inline in translated RTL prose it reorders, and a
            reader copying a mangled example gets a refusal they cannot explain. */}
        <p>
          <code dir="ltr">{EXAMPLE_KEY}</code> = <code dir="ltr">{EXAMPLE_VALUE}</code>
        </p>
        <Field label={t('admin.retention.col.key')}>
          {(p) => (
            <input
              {...p}
              type="text"
              dir="ltr"
              value={key}
              placeholder={EXAMPLE_KEY}
              onChange={(e) => setKey(e.target.value)}
            />
          )}
        </Field>
        <Field label={t('admin.retention.col.value')}>
          {(p) => (
            <input
              {...p}
              type="text"
              dir="ltr"
              value={value}
              placeholder={EXAMPLE_VALUE}
              onChange={(e) => setValue(e.target.value)}
            />
          )}
        </Field>
        <button type="submit" className="btn btn-primary" disabled={!canSave}>
          {t('admin.retention.save')}
        </button>
        {save.isError && (
          <p role="alert" className="field-error">
            {t('admin.retention.saveFailed')}
          </p>
        )}
      </form>
    </div>
  );
}
