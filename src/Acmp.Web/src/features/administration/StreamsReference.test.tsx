import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StreamsReference } from './StreamsReference';
import { useStreams, type StreamRef } from '../../api/members';
import i18n from '../../i18n';

const CORE: StreamRef = { publicId: 's1', code: 'core', nameEn: 'Core', nameAr: 'الأساسي', isWildcard: false };
const GOV: StreamRef = { publicId: 's2', code: 'government', nameEn: 'Government', nameAr: 'الحكومي', isWildcard: false };
const WILDCARD: StreamRef = { publicId: 'sw', code: 'all-streams', nameEn: 'All streams', nameAr: 'كل المسارات', isWildcard: true };

vi.mock('../../api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/members')>()),
  useStreams: vi.fn(),
}));

const mockStreams = useStreams as unknown as ReturnType<typeof vi.fn>;

const result = (over: Record<string, unknown> = {}) => ({
  data: [CORE, GOV, WILDCARD], isLoading: false, isError: false, refetch: vi.fn(), ...over,
});

describe('StreamsReference (Administration → Streams)', () => {
  beforeEach(() => {
    mockStreams.mockReturnValue(result());
    return i18n.changeLanguage('en');
  });

  it('lists the committee\'s seeded streams', () => {
    render(<StreamsReference />);

    expect(screen.getByText('Core')).toBeInTheDocument();
    expect(screen.getByText('Government')).toBeInTheDocument();
    expect(screen.getByText('All streams')).toBeInTheDocument();
  });

  // The code is the ABAC key topics carry, so a refused write is diagnosable from this table.
  it('shows each stream\'s code, not just its name', () => {
    render(<StreamsReference />);

    expect(screen.getByText('core')).toBeInTheDocument();
    expect(screen.getByText('all-streams')).toBeInTheDocument();
  });

  // ⚠ THE WHOLE POINT OF THIS COMPONENT. The tab used to claim "No streams configured" forever;
  // a seeded taxonomy must never render that copy again.
  it('never claims the committee has no streams while it has some', () => {
    render(<StreamsReference />);

    expect(screen.queryByText(/No streams/i)).not.toBeInTheDocument();
  });

  // ⚠ A FAILED LOAD IS NOT AN EMPTY COMMITTEE. Conflating them recreates the exact wrong belief
  // this component exists to stop: "there are no streams, so there is nothing to assign."
  it('reports a load failure rather than falling through to the empty state', () => {
    mockStreams.mockReturnValue(result({ data: undefined, isError: true }));
    render(<StreamsReference />);

    expect(screen.queryByText('No streams')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Retry/i })).toBeInTheDocument();
  });

  it('says it is loading rather than rendering an empty table', () => {
    mockStreams.mockReturnValue(result({ data: undefined, isLoading: true }));
    render(<StreamsReference />);

    expect(screen.queryByText('Core')).not.toBeInTheDocument();
    expect(screen.getByText('Loading…')).toBeInTheDocument();
  });

  // Reachable only before the seed migration runs on a given database — but it must still be an
  // honest statement of fact, not the old promise about a registry that has already shipped.
  it('shows a plain empty state when the taxonomy really is empty', () => {
    mockStreams.mockReturnValue(result({ data: [] }));
    render(<StreamsReference />);

    expect(screen.getByText('No streams')).toBeInTheDocument();
    expect(screen.queryByText(/BL-024/)).not.toBeInTheDocument();
  });

  it('renders the Arabic names under the Arabic locale', async () => {
    await i18n.changeLanguage('ar');
    render(<StreamsReference />);

    expect(screen.getByText('كل المسارات')).toBeInTheDocument();
    expect(screen.queryByText('All streams')).not.toBeInTheDocument();
  });
});
