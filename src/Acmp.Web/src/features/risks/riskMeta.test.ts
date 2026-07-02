import { describe, it, expect } from 'vitest';
import {
  statusTone, exposureTone, levelColor, heatCells, exposureMatrix, initials,
  RISK_STATUSES, RISK_EXPOSURES, RISK_LEVELS,
} from './riskMeta';

describe('statusTone', () => {
  it('maps the 5 risk statuses (design 3 + 2 no-reference)', () => {
    expect(statusTone('Open')).toBe('danger');
    expect(statusTone('Mitigating')).toBe('warn');
    expect(statusTone('Closed')).toBe('neutral');
    expect(statusTone('Accepted')).toBe('info');
    expect(statusTone('Escalated')).toBe('danger');
  });
});

describe('exposureTone', () => {
  it('follows the design expSem (Critical/High danger, Medium warn, Low success)', () => {
    expect(exposureTone('Critical')).toBe('danger');
    expect(exposureTone('High')).toBe('danger');
    expect(exposureTone('Medium')).toBe('warn');
    expect(exposureTone('Low')).toBe('success');
  });
});

describe('levelColor', () => {
  it('colours the probability/impact level words', () => {
    expect(levelColor('High')).toBe('var(--st-danger-fg)');
    expect(levelColor('Medium')).toBe('var(--st-warn-fg)');
    expect(levelColor('Low')).toBe('var(--text-2)');
  });
});

describe('heatCells', () => {
  it('lights only the (prob, impact) cell with the exposure colour — top-right for High×High', () => {
    // x = lvlIdx[High] = 2, yTop = 2 - lvlIdx[High] = 0 → index 0*3 + 2 = 2 (top-right).
    const cells = heatCells('High', 'High', 'Critical');
    expect(cells).toHaveLength(9);
    expect(cells[2]).toBe('var(--st-danger-dot)');
    expect(cells.filter((c) => c === 'var(--sunken)')).toHaveLength(8);
  });

  it('lights the bottom-left cell for Low×Low with the success colour', () => {
    // x = 0, yTop = 2 → index 2*3 + 0 = 6 (bottom-left).
    const cells = heatCells('Low', 'Low', 'Low');
    expect(cells[6]).toBe('var(--st-success-dot)');
  });
});

describe('exposureMatrix', () => {
  it('fills the (prob, impact) cell with the exposure bg + border, others plain surface', () => {
    // Medium prob (x=1), High impact (yTop=0) → row 0, col 1 is on.
    const m = exposureMatrix('Medium', 'High', 'High');
    expect(m).toHaveLength(3);
    expect(m[0][1]).toEqual({ bg: 'var(--st-danger-bg)', bd: 'var(--st-danger-dot)' });
    expect(m[0][0]).toEqual({ bg: 'var(--surface)', bd: 'var(--border)' });
  });
});

describe('initials', () => {
  it('takes the first two name parts, falling back to ?', () => {
    expect(initials('Noura P')).toBe('NP');
    expect(initials('Sara')).toBe('S');
    expect(initials('')).toBe('?');
  });
});

describe('option lists', () => {
  it('cover all enum values for the filters + create form', () => {
    expect(RISK_STATUSES).toHaveLength(5);
    expect(RISK_EXPOSURES).toHaveLength(4);
    expect(RISK_LEVELS).toEqual(['Low', 'Medium', 'High']);
  });
});
