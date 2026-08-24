import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { NotFoundPage } from './NotFoundPage';
import i18n from '../i18n';

/**
 * The 404 catch-all is reconciled to ACMP System States `404` and offers two
 * recovery routes, so it mounts under a router.
 *
 * This file exists because the page had NO test at all: both recovery actions are
 * inline `navigate()` handlers that had never been invoked, which coverage-v8 v4
 * surfaced (DW-082). v3 credited those lines for the JSX wrapped around them, so
 * the file read as covered while neither way out of a 404 had ever been exercised.
 */
function renderNotFound() {
  return render(
    <MemoryRouter initialEntries={['/no-such-route']}>
      <Routes>
        <Route path="/" element={<div>home page</div>} />
        <Route path="/search" element={<div>search page</div>} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('NotFoundPage', () => {
  it('renders the 404 state with its title and body', () => {
    renderNotFound();
    expect(screen.getByText('404')).toBeInTheDocument();
    expect(screen.getByText(i18n.t('notFound.title'))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('notFound.body'))).toBeInTheDocument();
  });

  it('routes to the dashboard from the primary action', async () => {
    renderNotFound();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('common.goToDashboard') }));
    expect(screen.getByText('home page')).toBeInTheDocument();
  });

  it('routes to search from the secondary action', async () => {
    renderNotFound();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('common.search') }));
    expect(screen.getByText('search page')).toBeInTheDocument();
  });
});
