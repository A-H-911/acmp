import { describe, it, expect } from 'vitest';
import type { RiskSummary } from '../../api/risks';
import type { DependencySummary } from '../../api/dependencies';
import type { TopicSummary } from '../../api/topics';
import {
  severityZone,
  isActiveRisk,
  riskMatrix,
  riskStats,
  depStats,
  depsByKind,
  buildTopicStreamMap,
  risksByStream,
  blockedDepsByStream,
} from './reportAgg';

function risk(over: Partial<RiskSummary>): RiskSummary {
  return {
    id: 'r', key: 'RSK-2026-001', title: { en: 'r', ar: 'r' },
    status: 'Open', likelihood: 'Low', impact: 'Low', severity: 1, exposure: 'Low',
    ownerUserId: '', ownerName: '', subjectType: 'Topic', subjectId: 't1', subjectKey: null,
    ...over,
  };
}
function dep(over: Partial<DependencySummary>): DependencySummary {
  return {
    id: 'd', key: 'DPN-2026-001',
    fromType: 'Topic', fromId: 't1', fromKey: 'TOP-1', fromTitle: 'a',
    toType: 'Action', toId: 'a1', toKey: 'ACT-1', toTitle: 'b',
    kind: 'DependsOn', status: 'Open', isBlocker: false,
    ...over,
  };
}
function topic(id: string, streams: string[]): TopicSummary {
  return { id, key: `TOP-${id}`, title: { en: id, ar: id }, status: 'Accepted', streams } as unknown as TopicSummary;
}

describe('severityZone', () => {
  it('maps L×I severity onto the design heat zones', () => {
    expect(severityZone(1)).toBe('success');
    expect(severityZone(2)).toBe('success');
    expect(severityZone(3)).toBe('warn');
    expect(severityZone(4)).toBe('warn');
    expect(severityZone(6)).toBe('danger');
    expect(severityZone(9)).toBe('danger');
  });
});

describe('isActiveRisk', () => {
  it('excludes the two terminal states', () => {
    expect(isActiveRisk(risk({ status: 'Open' }))).toBe(true);
    expect(isActiveRisk(risk({ status: 'Mitigating' }))).toBe(true);
    expect(isActiveRisk(risk({ status: 'Escalated' }))).toBe(true);
    expect(isActiveRisk(risk({ status: 'Closed' }))).toBe(false);
    expect(isActiveRisk(risk({ status: 'Accepted' }))).toBe(false);
  });
});

describe('riskMatrix', () => {
  it('counts active risks per (impact,probability) cell and ignores terminal risks', () => {
    const m = riskMatrix([
      risk({ likelihood: 'High', impact: 'High' }), // top-left of High row? no — impact High row, prob High col
      risk({ likelihood: 'Low', impact: 'Low' }),
      risk({ likelihood: 'Low', impact: 'Low' }),
      risk({ status: 'Closed', likelihood: 'Low', impact: 'Low' }), // excluded
    ]);
    expect(m.active).toBe(3);
    // rows: [High impact, Med, Low]; cols: [Low prob, Med, High]
    const highImpact = m.rows[0];
    expect(highImpact.impact).toBe('High');
    expect(highImpact.cells[2].count).toBe(1); // High impact × High prob
    expect(highImpact.cells[2].zone).toBe('danger'); // sev 9
    const lowImpact = m.rows[2];
    expect(lowImpact.cells[0].count).toBe(2); // Low impact × Low prob
    expect(lowImpact.cells[0].zone).toBe('success'); // sev 1
  });

  it('tints every cell by position even when empty', () => {
    const m = riskMatrix([]);
    // High impact × Med prob = sev 6 → danger, tinted despite 0 count
    expect(m.rows[0].cells[1]).toEqual({ count: 0, zone: 'danger' });
  });
});

describe('riskStats & depStats', () => {
  it('counts risk statuses and active high-severity/critical', () => {
    const s = riskStats([
      risk({ status: 'Open' }),
      risk({ status: 'Mitigating' }),
      risk({ status: 'Open', exposure: 'High' }),
      risk({ status: 'Critical' as never, exposure: 'Critical' }), // odd status guarded by exposure+active check
      risk({ status: 'Closed', exposure: 'Critical' }), // not active → excluded from high/critical
    ]);
    expect(s[0].value).toBe(2); // Open
    expect(s[1].value).toBe(1); // Mitigating
    // high severity = active & (High|Critical): the Open/High one only (Closed/Critical excluded)
    expect(s[2].value).toBe(1);
  });

  it('counts dependency totals, open, blocked, resolved', () => {
    const s = depStats([
      dep({ status: 'Open', isBlocker: true }),
      dep({ status: 'Open', isBlocker: false }),
      dep({ status: 'Resolved', isBlocker: false }),
    ]);
    expect(s[0].value).toBe(3); // total
    expect(s[1].value).toBe(2); // open
    expect(s[2].value).toBe(1); // blocked
    expect(s[3].value).toBe(1); // resolved
  });
});

describe('depsByKind', () => {
  it('bars the 4 kinds in order, normalized to the largest', () => {
    const bars = depsByKind([
      dep({ kind: 'DependsOn' }),
      dep({ kind: 'DependsOn' }),
      dep({ kind: 'Blocks' }),
    ]);
    expect(bars.map((b) => b.key)).toEqual(['DependsOn', 'BlockedBy', 'Blocks', 'RelatesTo']);
    expect(bars[0].count).toBe(2);
    expect(bars[0].pct).toBe(100);
    expect(bars[2].pct).toBe(50);
    expect(bars[2].zone).toBe('danger'); // Blocks
    expect(bars[0].zone).toBe('info'); // DependsOn
  });
});

describe('by-stream join (the money path)', () => {
  const topics = [topic('t1', ['identity', 'payments']), topic('t2', ['identity']), topic('t3', [])];
  const map = buildTopicStreamMap(topics);

  it('counts a multi-stream topic under EACH of its streams, KPI stays distinct', () => {
    const res = risksByStream(
      [
        risk({ subjectType: 'Topic', subjectId: 't1', likelihood: 'High', impact: 'High' }), // identity + payments
        risk({ subjectType: 'Topic', subjectId: 't2', likelihood: 'Low', impact: 'Low' }), // identity
      ],
      map,
    );
    expect(res.kpi).toBe(2); // distinct contributing risks
    const identity = res.bars.find((b) => b.key === 'identity')!;
    const payments = res.bars.find((b) => b.key === 'payments')!;
    expect(identity.count).toBe(2);
    expect(payments.count).toBe(1);
    // Σbars ≥ distinct KPI (multi-stream double-count is intended)
    const sum = res.bars.reduce((a, b) => a + b.count, 0);
    expect(sum).toBeGreaterThanOrEqual(res.kpi);
    expect(sum).toBe(3);
    // bar colour = max severity in the stream (t1 sev 9 → danger)
    expect(payments.zone).toBe('danger');
  });

  it('excludes risks on non-Topic subjects, empty-stream topics, and terminal risks', () => {
    const res = risksByStream(
      [
        risk({ subjectType: 'Decision', subjectId: 'x' }), // non-Topic
        risk({ subjectType: 'Topic', subjectId: 't3' }), // topic with no streams
        risk({ status: 'Closed', subjectType: 'Topic', subjectId: 't2' }), // terminal
        risk({ subjectType: 'Topic', subjectId: 'unknown' }), // not in map
      ],
      map,
    );
    expect(res.kpi).toBe(0);
    expect(res.bars).toEqual([]);
  });

  it('tallies only blocker deps, unioning both Topic endpoints', () => {
    const res = blockedDepsByStream(
      [
        dep({ isBlocker: true, fromType: 'Topic', fromId: 't1', toType: 'Topic', toId: 't2' }), // identity(both)+payments
        dep({ isBlocker: true, fromType: 'Topic', fromId: 't2', toType: 'Action', toId: 'a1' }), // identity
        dep({ isBlocker: false, fromType: 'Topic', fromId: 't1' }), // not a blocker → skipped
      ],
      map,
    );
    expect(res.kpi).toBe(2); // two distinct blocker deps resolved
    const identity = res.bars.find((b) => b.key === 'identity')!;
    const payments = res.bars.find((b) => b.key === 'payments')!;
    expect(identity.count).toBe(2); // both deps touch identity
    expect(payments.count).toBe(1);
    expect(identity.zone).toBe('danger'); // count ≥ 2
    expect(payments.zone).toBe('warn'); // count == 1
  });

  it('dedupes a single dep touching the same stream on both ends', () => {
    const res = blockedDepsByStream(
      [dep({ isBlocker: true, fromType: 'Topic', fromId: 't2', toType: 'Topic', toId: 't2' })],
      map,
    );
    // both ends resolve to 'identity' → counted once for this dep
    expect(res.bars.find((b) => b.key === 'identity')!.count).toBe(1);
    expect(res.kpi).toBe(1);
  });
});
