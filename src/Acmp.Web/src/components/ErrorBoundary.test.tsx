import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorBoundary } from './ErrorBoundary';
import i18n from '../i18n';

let shouldThrow = true;
function Boom() {
  if (shouldThrow) throw new Error('boom-internal-detail');
  return <div>recovered content</div>;
}

// The boundary must catch a child render error, show a SAFE fallback (no technical detail leaked,
// docs/14 p92), log to console for diagnostics only, and recover via the retry action.
describe('ErrorBoundary', () => {
  afterEach(() => {
    shouldThrow = true;
    vi.restoreAllMocks();
  });

  it('shows the safe fallback and logs diagnostics when a child throws', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );
    expect(screen.getByRole('button', { name: i18n.t('common.retry') })).toBeInTheDocument();
    expect(screen.queryByText(/boom-internal-detail/)).not.toBeInTheDocument(); // no leak
    expect(spy).toHaveBeenCalled(); // componentDidCatch logged
  });

  it('renders children untouched when nothing throws', () => {
    shouldThrow = false;
    render(
      <ErrorBoundary>
        <div>ok content</div>
      </ErrorBoundary>,
    );
    expect(screen.getByText('ok content')).toBeInTheDocument();
  });

  it('recovers via retry once the child stops throwing', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );
    shouldThrow = false; // the next render will succeed
    await userEvent.click(screen.getByRole('button', { name: i18n.t('common.retry') }));
    expect(screen.getByText('recovered content')).toBeInTheDocument();
  });
});
