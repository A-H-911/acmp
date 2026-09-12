import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { cssVars, setCssVars } from './cssVars';

describe('cssVars (DEC-174: data-driven values reach CSS as custom properties)', () => {
  it('sets each variable on the element', () => {
    const el = document.createElement('span');
    setCssVars(el, { '--pct': '42%', '--c': 'var(--st-danger-dot)' });
    expect(el.style.getPropertyValue('--pct')).toBe('42%');
    expect(el.style.getPropertyValue('--c')).toBe('var(--st-danger-dot)');
  });

  it('updates a changed value and removes a name the next call drops or nulls', () => {
    const el = document.createElement('span');
    setCssVars(el, { '--a': '1px', '--b': '2px', '--c': '3px' });
    setCssVars(el, { '--a': '9px', '--b': undefined });
    expect(el.style.getPropertyValue('--a')).toBe('9px');
    expect(el.style.getPropertyValue('--b')).toBe('');
    expect(el.style.getPropertyValue('--c')).toBe('');
  });

  it('never touches a property it did not set', () => {
    const el = document.createElement('span');
    el.style.setProperty('--foreign', 'x');
    setCssVars(el, { '--a': '1px' });
    setCssVars(el, {});
    expect(el.style.getPropertyValue('--foreign')).toBe('x');
    expect(el.style.getPropertyValue('--a')).toBe('');
  });

  it('works as a ref across re-renders, including a value that goes away', () => {
    const Bar = ({ pct }: { pct?: number }) => (
      <span data-testid="bar" ref={cssVars({ '--pct': pct == null ? undefined : `${pct}%` })} />
    );
    const { getByTestId, rerender, unmount } = render(<Bar pct={30} />);
    expect(getByTestId('bar').style.getPropertyValue('--pct')).toBe('30%');
    rerender(<Bar pct={75} />);
    expect(getByTestId('bar').style.getPropertyValue('--pct')).toBe('75%');
    rerender(<Bar />);
    expect(getByTestId('bar').style.getPropertyValue('--pct')).toBe('');
    unmount(); // the detach call (null) must be a no-op, not a throw
  });
});
