/*
 * Knowledge templates register (P15e, FR-119) — the isTemplates .gTpl grid of "ACMP Administration.dc.html"
 * (lines 174-193), relocated to a standalone /templates route in the Knowledge nav group (was an Admin
 * tab). Reads are committee-wide; New/Edit/Deprecate are managers only (Chairman/Secretary/Administrator —
 * the API enforces Template.Manage). Renders the REAL backend enum values (Active/Deprecated; the design's
 * sample active/draft/archived are illustrative). A Deprecate row action is added (design draws only edit).
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useTemplates, useDeprecateTemplate, type TemplateSummary, type TemplateTargetType } from '../../api/templates';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { StatusChip } from '../../components/ui/StatusChip';
import { FilterChip } from '../../components/ui/FilterChip';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, TEMPLATE_STATUSES, TARGET_TYPES } from './templatesMeta';
import { TemplateFormDialog } from './TemplateFormDialog';
import { formatDmy } from '../../lib/p15Date';
import './templates.css';

export function TemplatesRegister() {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const canManage = hasRole(auth, 'chairman', 'secretary', 'administrator');
  const [statuses, setStatuses] = useState<string[]>([]);
  const [targetType, setTargetType] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<TemplateSummary | null>(null);
  const [deprecating, setDeprecating] = useState<TemplateSummary | null>(null);

  const { data, isLoading, isError, refetch } = useTemplates({
    statuses: statuses.length ? (statuses as TemplateSummary['status'][]) : undefined,
    targetType: (targetType || undefined) as TemplateTargetType | undefined,
  });
  const deprecate = useDeprecateTemplate();

  const rows = data?.items ?? [];
  const hasFilters = statuses.length > 0 || targetType !== '';
  const clearAllFilters = () => {
    setStatuses([]);
    setTargetType('');
  };
  const fmtDate = (iso: string) => formatDmy(iso, i18n.language);
  const pick = (l: TemplateSummary['name']) => (i18n.language === 'ar' ? l.ar : l.en);

  async function onConfirmDeprecate() {
    if (!deprecating) return;
    await deprecate.mutateAsync(deprecating.id);
    setDeprecating(null);
  }

  return (
    <section className="page">
      <div className="tpl-head">
        <div>
          <h1 className="page-title">{t('templates.title')}</h1>
          <div className="tpl-head-sub">{t('templates.subtitle')}</div>
        </div>
        {canManage && (
          <Button variant="primary" onClick={() => setCreateOpen(true)}>
            <Icon name="plus" size={16} aria-hidden /> {t('templates.newTemplate')}
          </Button>
        )}
      </div>

      <div className="tpl-bar" role="search" aria-label={t('templates.filtersLabel')}>
        <FilterChip
          label={t('templates.filter.type')}
          anyLabel={t('templates.filter.anyType')}
          options={TARGET_TYPES.map((v) => ({ value: v, label: t(`templates.targetType.${v}`) }))}
          value={targetType}
          onChange={setTargetType}
        />
        <FilterChip
          multiple
          label={t('templates.filter.status')}
          options={TEMPLATE_STATUSES.map((v) => ({ value: v, label: t(`templates.status.${v}`) }))}
          values={statuses}
          onChange={setStatuses}
          clearLabel={t('templates.clearFilters')}
        />
        {data && <span className="tpl-count"><Icon name="template" size={13} aria-hidden /> {t('templates.showing', { count: rows.length })}</span>}
      </div>

      {isLoading ? (
        <TemplatesSkeleton />
      ) : isError ? (
        <ErrorState title={t('templates.error.title')} body={t('templates.error.body')} onRetry={() => refetch()} />
      ) : rows.length === 0 ? (
        hasFilters ? (
          <div>
            <EmptyState icon="search" title={t('templates.filterEmpty.title')} body={t('templates.filterEmpty.body')} />
            <div className="tpl-foot" style={{ display: 'flex', justifyContent: 'center' }}>
              <Button variant="secondary" onClick={clearAllFilters}>{t('templates.clearFilters')}</Button>
            </div>
          </div>
        ) : (
          <div>
            <EmptyState icon="template" title={t('templates.empty.title')} body={t('templates.empty.body')} />
            {canManage && (
              <div className="tpl-foot" style={{ display: 'flex', justifyContent: 'center' }}>
                <Button variant="primary" onClick={() => setCreateOpen(true)}>
                  <Icon name="plus" size={16} aria-hidden /> {t('templates.newTemplate')}
                </Button>
              </div>
            )}
          </div>
        )
      ) : (
        <div className="tpl-card">
          <div className="gTpl tpl-hrow">
            <span className="tpl-hcell">{t('templates.col.name')}</span>
            <span className="tpl-hcell">{t('templates.col.type')}</span>
            <span className="tpl-hcell">{t('templates.col.version')}</span>
            <span className="tpl-hcell">{t('templates.col.status')}</span>
            <span className="tpl-hcell">{t('templates.col.updated')}</span>
            <span className="tpl-hcell" />
          </div>
          {rows.map((tp) => (
            <div className="gTpl tpl-row" key={tp.id}>
              <span className="tpl-cell tpl-name-cell">
                <span className="tpl-name-icon" aria-hidden="true"><Icon name="template" size={15} /></span>
                <span className="tpl-name">{pick(tp.name)}</span>
              </span>
              <span className="tpl-cell"><span className="tpl-type">{t(`templates.targetType.${tp.targetType}`)}</span></span>
              <span className="tpl-cell"><span className="tpl-version">v{tp.version}</span></span>
              <span className="tpl-cell"><StatusChip tone={statusTone(tp.status)} label={t(`templates.status.${tp.status}`)} size="sm" /></span>
              <span className="tpl-cell"><span className="tpl-updated">{fmtDate(tp.updatedAt ?? tp.createdAt)}</span></span>
              <span className="tpl-cell tpl-actions">
                {canManage && (
                  <>
                    <button type="button" className="tpl-icon-btn" aria-label={t('templates.editAria', { name: pick(tp.name) })} onClick={() => setEditing(tp)}>
                      <Icon name="pencil" size={14} aria-hidden />
                    </button>
                    <button
                      type="button"
                      className="tpl-icon-btn"
                      aria-label={t('templates.deprecateAria', { name: pick(tp.name) })}
                      disabled={tp.status === 'Deprecated'}
                      onClick={() => setDeprecating(tp)}
                    >
                      <Icon name="archive" size={14} aria-hidden />
                    </button>
                  </>
                )}
              </span>
            </div>
          ))}
        </div>
      )}

      {canManage && <TemplateFormDialog open={createOpen} onClose={() => setCreateOpen(false)} />}
      {canManage && editing && (
        <TemplateFormDialog key={editing.id} open onClose={() => setEditing(null)} template={editing} />
      )}
      <Dialog
        open={!!deprecating}
        onClose={() => setDeprecating(null)}
        tone="danger"
        icon={<Icon name="archive" size={20} aria-hidden />}
        title={t('templates.deprecate.title')}
        description={t('templates.deprecate.subtitle')}
        footer={
          <>
            <Button variant="secondary" onClick={() => setDeprecating(null)}>{t('common.cancel')}</Button>
            <Button variant="danger" loading={deprecate.isPending} onClick={() => void onConfirmDeprecate()}>{t('templates.deprecate.confirm')}</Button>
          </>
        }
      />
    </section>
  );
}

function TemplatesSkeleton() {
  const { t } = useTranslation();
  const rowWidths = ['58%', '82%', '50%', '70%', '46%', '64%', '56%'];
  return (
    <div className="tpl-card" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      {rowWidths.map((w, i) => (
        <div key={i} className="gTpl tpl-row" aria-hidden="true">
          <span className="tpl-cell"><span className="skeleton" style={{ blockSize: 12, borderRadius: 5, inlineSize: w }} /></span>
          <span className="tpl-cell"><span className="skeleton" style={{ blockSize: 12, borderRadius: 5, inlineSize: 60 }} /></span>
          <span className="tpl-cell"><span className="skeleton" style={{ blockSize: 12, borderRadius: 5, inlineSize: 30 }} /></span>
          <span className="tpl-cell"><span className="skeleton" style={{ blockSize: 12, borderRadius: 5, inlineSize: 70 }} /></span>
          <span className="tpl-cell"><span className="skeleton" style={{ blockSize: 12, borderRadius: 5, inlineSize: 80 }} /></span>
          <span className="tpl-cell" />
        </div>
      ))}
    </div>
  );
}
