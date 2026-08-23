/*
 * Administration → Streams (mirrors the "ACMP Administration" `streams` section). The committee's
 * delivery-stream taxonomy, read live from GET /api/members/streams.
 *
 * ⚠ THIS TAB SHIPPED FALSE COPY. It rendered "No streams configured — delivery streams and their
 * committee owners land with the stream registry (BL-024)" for as long as that was true, and kept
 * rendering it after migration 20260813125628_Membership_StreamTaxonomy_ADR0042 seeded five delivery
 * streams plus the wildcard. An honest-empty state that outlives its own premise is not honest; it
 * is a claim the screen keeps making about a table it never reads.
 *
 * ⚠ PARTIAL-FIDELITY COMPOSITION (INV-014). The reference draws five columns — Stream, Owner,
 * Members, Active topics, Status — and the server sources exactly one of them. Streams are seeded
 * reference data: there is no stream owner, no per-stream topic count and no stream lifecycle status
 * anywhere behind the endpoint. Those three columns are declared missing here rather than filled
 * with placeholders, because a fabricated column is the same lie this tab is being fixed for, only
 * with more pixels. When an owner/status/topic-count actually exists, add the columns then.
 *
 * `Code` is NOT a design column, and it is here on purpose. It is the real key: topics carry stream
 * CODES and the ABAC intersect resolves on them (api/members.ts), so it is the field an
 * administrator diagnosing a refused write actually needs. A truthful non-design column beats a
 * design-shaped column of blanks.
 */
import { useTranslation } from 'react-i18next';
import { streamName, useStreams, type StreamRef } from '../../api/members';
import { Table, type Column } from '../../components/ui/Table';
import { EmptyState, ErrorState, LoadingState } from '../../components/states';

export function StreamsReference() {
  const { t, i18n } = useTranslation();
  const { data: streams, isLoading, isError, refetch } = useStreams();
  const isArabic = i18n.language === 'ar';

  if (isLoading) return <LoadingState />;

  // ⚠ A FAILED LOAD MUST NOT FALL THROUGH TO THE EMPTY STATE. "This committee has no streams" and
  // "we could not ask" are different facts, and an administrator who reads the first when the second
  // is true concludes there is nothing to assign — which is precisely the wrong belief this whole
  // change exists to stop the screen from creating.
  if (isError || !streams) return <ErrorState onRetry={() => void refetch()} />;

  if (streams.length === 0) {
    return <EmptyState icon="stream" title={t('admin.streams.emptyTitle')} body={t('admin.streams.emptyBody')} />;
  }

  const columns: Column<StreamRef>[] = [
    {
      id: 'stream',
      header: t('admin.streams.col.stream'),
      width: '62%',
      cell: (s) => (
        <span className="adm-stream-ref">
          <span className={`adm-stream-dot ${s.isWildcard ? 'is-wildcard' : ''}`} aria-hidden="true" />
          <span className="adm-role-name">{streamName(s, isArabic)}</span>
        </span>
      ),
    },
    {
      id: 'code',
      header: t('admin.streams.col.code'),
      width: '38%',
      // The wildcard's own name already says "All streams"; the dot is drawn distinctly (DEC-043)
      // rather than adding a badge, so unrestricted scope reads the same here as on the member chips.
      cell: (s) => <span className="adm-mchip">{s.code}</span>,
    },
  ];

  return <Table caption={t('admin.tabs.streams')} columns={columns} rows={streams} getRowKey={(s) => s.publicId} />;
}
