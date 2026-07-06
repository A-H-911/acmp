import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { ErrorBoundary } from './ErrorBoundary';
import i18n from '../i18n';

let shouldThrow = true;
function Boom() {
  if (shouldThrow) throw new Error('boom-internal-detail');
  return <div>recovered content</div>;
}

/** The boundary fallback uses routing (Go to dashboard), so it mounts under a router. */
function renderBoundary() {
  return render(
    <MemoryRouter initialEntries={['/boom']}>
      <Routes>
        <Route path="/" element={<div>home page</div>} />
        <Route path="*" element={<ErrorBoundary><Boom /></ErrorBoundary>} />
      </Routes>
    </MemoryRouter>,
  );
}

// The boundary must catch a child render error, show a SAFE fallback reconciled to
// System States `error` (no technical detail leaked, docs/domain/information-architecture.md p92), log to console for
// diagnostics only, and offer the reload + go-to-dashboard recovery actions.
describe('ErrorBoundary', () => {
  afterEach(() => {
    shouldThrow = true;
    vi.restoreAllMocks();
  });

  it('shows the safe fallback and logs diagnostics when a child throws', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    renderBoundary();
    expect(screen.getByText(i18n.t('errorBoundary.title'))).toBeInTheDocument();
    expect(screen.getByRole('button', { name: i18n.t('common.reload') })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: i18n.t('common.goToDashboard') })).toBeInTheDocument();
    expect(screen.queryByText(/boom-internal-detail/)).not.toBeInTheDocument(); // no leak
    expect(spy).toHaveBeenCalled(); // componentDidCatch logged
  });

  it('renders children untouched when nothing throws', () => {
    shouldThrow = false;
    render(
      <MemoryRouter>
        <ErrorBoundary>
          <div>ok content</div>
        </ErrorBoundary>
      </MemoryRouter>,
    );
    expect(screen.getByText('ok content')).toBeInTheDocument();
  });

  it('navigates home from the fallback "go to dashboard" action', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    renderBoundary();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('common.goToDashboard') }));
    expect(screen.getByText('home page')).toBeInTheDocument();
  });
});
