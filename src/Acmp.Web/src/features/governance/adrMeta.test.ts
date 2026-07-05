import { describe, it, expect, vi, afterEach } from 'vitest';
import { statusTone, ADR_STATUSES, exportMarkdown, downloadMarkdown, type AdrExportLabels } from './adrMeta';
import type { AdrDetail } from '../../api/adrs';

const LABELS: AdrExportLabels = {
  status: 'Status', context: 'Context', drivers: 'Decision drivers', options: 'Considered options',
  chosen: 'Chosen', decision: 'Decision', consequences: 'Consequences', positive: 'Positive', negative: 'Negative',
};

const BASE: AdrDetail = {
  id: 'g1', key: 'ADR-2026-003', title: { en: 'Keycloak as the standard IdP', ar: 'كيكلوك' },
  status: 'Approved', context: { en: 'Fragmented auth.', ar: 'مصادقة مجزأة.' }, decisionDrivers: null,
  decisionText: { en: 'Adopt Keycloak.', ar: 'اعتماد كيكلوك.' }, consequencesPositive: null, consequencesNegative: null,
  options: [], authorUserId: 'kc-1', authorName: 'Khalid A', sourceDecisionId: null,
  approvedAt: '2026-02-18T00:00:00Z', approvedByName: 'S. M.', supersededByAdrId: null, supersededByAdrKey: null,
  supersessionReason: null, supersedesAdrId: null, supersedesAdrKey: null, deprecationReason: null,
  createdAt: '2026-02-10T00:00:00Z',
};

describe('statusTone', () => {
  it('maps every one of the five canonical statuses to a chip tone', () => {
    expect(ADR_STATUSES).toEqual(['Draft', 'Proposed', 'Approved', 'Superseded', 'Deprecated']);
    expect(statusTone('Draft')).toBe('neutral');
    expect(statusTone('Proposed')).toBe('info');
    expect(statusTone('Approved')).toBe('success');
    expect(statusTone('Superseded')).toBe('neutral');
    expect(statusTone('Deprecated')).toBe('danger');
  });
});

describe('exportMarkdown', () => {
  it('emits only the required sections for a lean record (no drivers/options/consequences)', () => {
    const md = exportMarkdown(BASE, 'en', LABELS);
    expect(md).toContain('# ADR-2026-003 — Keycloak as the standard IdP');
    expect(md).toContain('**Status:** Approved');
    expect(md).toContain('## Context\nFragmented auth.');
    expect(md).toContain('## Decision\nAdopt Keycloak.');
    // Optional sections are absent.
    expect(md).not.toContain('## Decision drivers');
    expect(md).not.toContain('## Considered options');
    expect(md).not.toContain('## Consequences');
  });

  it('emits every optional section when present and marks the chosen option (Arabic locale)', () => {
    const full: AdrDetail = {
      ...BASE,
      decisionDrivers: { en: 'Security consolidation.', ar: 'توحيد الأمن.' },
      options: [
        { id: 'o1', name: { en: 'Keycloak', ar: 'كيكلوك' }, body: { en: 'Mature OSS IdP.', ar: 'مفتوح المصدر.' }, isChosen: true },
        { id: 'o2', name: { en: 'In-house', ar: 'داخلي' }, body: null, isChosen: false },
      ],
      consequencesPositive: { en: 'Unified SSO.', ar: 'دخول موحّد.' },
      consequencesNegative: { en: 'Migration effort.', ar: 'جهد الترحيل.' },
    };
    const md = exportMarkdown(full, 'ar', LABELS);
    expect(md).toContain('# ADR-2026-003 — كيكلوك');
    expect(md).toContain('## Decision drivers\nتوحيد الأمن.');
    expect(md).toContain('## Considered options');
    expect(md).toContain('- **كيكلوك** _(Chosen)_ — مفتوح المصدر.');
    expect(md).toContain('- **داخلي**'); // no chosen mark, no body suffix
    expect(md).not.toContain('- **داخلي** _(Chosen)_');
    expect(md).toContain('**Positive:** دخول موحّد.');
    expect(md).toContain('**Negative:** جهد الترحيل.');
  });

  it('emits a consequences section with only the positive block when negative is absent', () => {
    const md = exportMarkdown({ ...BASE, consequencesPositive: { en: 'Good.', ar: 'جيد.' } }, 'en', LABELS);
    expect(md).toContain('## Consequences');
    expect(md).toContain('**Positive:** Good.');
    expect(md).not.toContain('**Negative:**');
  });
});

describe('downloadMarkdown', () => {
  const origCreate = URL.createObjectURL;
  const origRevoke = URL.revokeObjectURL;
  afterEach(() => {
    URL.createObjectURL = origCreate;
    URL.revokeObjectURL = origRevoke;
    vi.restoreAllMocks();
  });

  it('creates a blob URL, clicks a download anchor, and revokes the URL', () => {
    URL.createObjectURL = vi.fn(() => 'blob:adr');
    URL.revokeObjectURL = vi.fn();
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    downloadMarkdown('ADR-2026-003.md', '# hi');
    expect(URL.createObjectURL).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:adr');
  });
});
