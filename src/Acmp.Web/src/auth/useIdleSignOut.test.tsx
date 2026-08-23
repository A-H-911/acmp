import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, act } from '@testing-library/react';
import { useIdleSignOut, IDLE_TIMEOUT_MS } from './useIdleSignOut';

/*
 * AC-004 / OQ-076. The point of these is that the timeout is FORCED, not observed: an idle timeout
 * that never fires looks identical to one that works, right up until it matters. Every case here
 * either drives the clock past the deadline and asserts the callback ran, or drives it to just
 * before and asserts it did NOT — the second half is what makes the first mean anything.
 */

function Harness({ enabled, onIdle, idleMs }: { enabled: boolean; onIdle: () => void; idleMs?: number }) {
  useIdleSignOut(enabled, onIdle, idleMs);
  return null;
}

describe('useIdleSignOut (AC-004 / OQ-076)', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('signs out after the idle window with no activity', () => {
    const onIdle = vi.fn();
    render(<Harness enabled onIdle={onIdle} idleMs={1000} />);

    expect(onIdle).not.toHaveBeenCalled();
    act(() => void vi.advanceTimersByTime(1000));
    expect(onIdle).toHaveBeenCalledTimes(1);
  });

  it('does NOT sign out one tick before the window closes', () => {
    // The discriminating half: without it, a hook that fired immediately would pass the test above.
    const onIdle = vi.fn();
    render(<Harness enabled onIdle={onIdle} idleMs={1000} />);

    act(() => void vi.advanceTimersByTime(999));
    expect(onIdle).not.toHaveBeenCalled();
  });

  it.each([
    ['a keypress', () => window.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }))],
    // A plain Event, not a PointerEvent: jsdom does not implement the latter, and the listener never
    // inspects the event object — so this triggers the same code path a real press does.
    ['a pointer press', () => window.dispatchEvent(new Event('pointerdown'))],
  ])('restarts the clock on %s', (_label, activity) => {
    const onIdle = vi.fn();
    render(<Harness enabled onIdle={onIdle} idleMs={1000} />);

    act(() => void vi.advanceTimersByTime(900));
    act(() => activity());
    // Past the ORIGINAL deadline but not the restarted one — proves the clock actually reset rather
    // than the event merely being listened for.
    act(() => void vi.advanceTimersByTime(900));
    expect(onIdle).not.toHaveBeenCalled();

    act(() => void vi.advanceTimersByTime(100));
    expect(onIdle).toHaveBeenCalledTimes(1);
  });

  it('does NOT restart the clock when the tab is merely HIDDEN', () => {
    // An abandoned machine is the case this hook exists for, so leaving must not count as presence.
    const onIdle = vi.fn();
    render(<Harness enabled onIdle={onIdle} idleMs={1000} />);

    act(() => void vi.advanceTimersByTime(900));
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');
    act(() => document.dispatchEvent(new Event('visibilitychange')));
    act(() => void vi.advanceTimersByTime(100));

    expect(onIdle).toHaveBeenCalledTimes(1);
  });

  it('fires only once, even if timers keep running through the redirect', () => {
    const onIdle = vi.fn();
    render(<Harness enabled onIdle={onIdle} idleMs={1000} />);

    act(() => void vi.advanceTimersByTime(5000));
    expect(onIdle).toHaveBeenCalledTimes(1);
  });

  it('is inert while unauthenticated — nobody is signed out of nothing', () => {
    const onIdle = vi.fn();
    render(<Harness enabled={false} onIdle={onIdle} idleMs={1000} />);

    act(() => void vi.advanceTimersByTime(10_000));
    expect(onIdle).not.toHaveBeenCalled();
  });

  it('a re-rendered inline callback does not reset the clock', () => {
    /*
     * The trap this hook is written against. AuthProvider passes an inline arrow, which every render
     * recreates; if the effect depended on it, the clock would restart on every render and the
     * timeout would be unreachable in exactly the app that renders most often — while looking, in
     * every other test here, like it worked.
     */
    const onIdle = vi.fn();
    const { rerender } = render(<Harness enabled onIdle={() => onIdle()} idleMs={1000} />);

    act(() => void vi.advanceTimersByTime(900));
    rerender(<Harness enabled onIdle={() => onIdle()} idleMs={1000} />);
    act(() => void vi.advanceTimersByTime(100));

    expect(onIdle).toHaveBeenCalledTimes(1);
  });

  it('stops the timer on unmount', () => {
    const onIdle = vi.fn();
    const { unmount } = render(<Harness enabled onIdle={onIdle} idleMs={1000} />);

    unmount();
    act(() => void vi.advanceTimersByTime(5000));
    expect(onIdle).not.toHaveBeenCalled();
  });

  it('defaults to 30 minutes, matching the realm ssoSessionIdleTimeout it replaces', () => {
    expect(IDLE_TIMEOUT_MS).toBe(1800_000);
  });
});
