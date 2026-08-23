import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, renderHook, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StreamPicker } from './StreamPicker';
import { useAssignableStreams, type StreamRef } from '../../api/members';
import { makeQueryWrapper, stubFetch } from '../../test/queryHarness';
import i18n from '../../i18n';

const CORE: StreamRef = { publicId: 's1', code: 'core', nameEn: 'Core', nameAr: 'الأساسي', isWildcard: false };
const GOV: StreamRef = { publicId: 's2', code: 'government', nameEn: 'Government', nameAr: 'الحكومي', isWildcard: false };
const WILDCARD: StreamRef = { publicId: 's3', code: 'all-streams', nameEn: 'All streams', nameAr: 'كل المسارات', isWildcard: true };

vi.mock('../../api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/members')>()),
  useAssignableStreams: vi.fn(),
}));

const mockHook = useAssignableStreams as unknown as ReturnType<typeof vi.fn>;
const ready = (data: StreamRef[]) => ({ data, isLoading: false, isError: false });

function setup(values: string[] = [], onChange = vi.fn()) {
  render(<StreamPicker ariaLabel="Affected streams" values={values} onChange={onChange} />);
  return onChange;
}

describe('StreamPicker (ADR-0042 step 2)', () => {
  beforeEach(() => {
    mockHook.mockReturnValue(ready([CORE, GOV]));
    return i18n.changeLanguage('en');
  });

  it('renders one toggle button per stream, none pressed initially', () => {
    setup();

    expect(screen.getByRole('button', { name: 'Core' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'Government' })).toHaveAttribute('aria-pressed', 'false');
  });

  // ⚠ The value handed back is the CODE, never the display label. The ABAC stream check resolves
  // Stream.Code, so posting "Core" instead of "core" would be the free-text mismatch this whole
  // slice exists to remove (DEF-057's third finding).
  it('reports the stream CODE, not the label, when a chip is selected', async () => {
    const user = userEvent.setup();
    const onChange = setup([]);

    await user.click(screen.getByRole('button', { name: 'Core' }));

    expect(onChange).toHaveBeenCalledWith(['core']);
  });

  it('adds to the existing selection rather than replacing it', async () => {
    const user = userEvent.setup();
    const onChange = setup(['core']);

    await user.click(screen.getByRole('button', { name: 'Government' }));

    expect(onChange).toHaveBeenCalledWith(['core', 'government']);
  });

  it('deselects a chip that is already selected', async () => {
    const user = userEvent.setup();
    const onChange = setup(['core', 'government']);

    await user.click(screen.getByRole('button', { name: 'Core' }));

    expect(onChange).toHaveBeenCalledWith(['government']);
  });

  it('marks the selected chips pressed', () => {
    setup(['government']);

    expect(screen.getByRole('button', { name: 'Core' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'Government' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('labels chips in Arabic when the locale is Arabic', async () => {
    await i18n.changeLanguage('ar');
    setup();

    expect(screen.getByRole('button', { name: 'الأساسي' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Core' })).not.toBeInTheDocument();
  });

  // Loading and failure must SAY so. An empty chip row is indistinguishable from "this committee has
  // no streams", leaving the user unable to tell that a REQUIRED field is unfillable.
  it('says it is loading rather than rendering an empty row', () => {
    mockHook.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    setup();

    expect(screen.getByText('Loading…')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('announces a load failure', () => {
    mockHook.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    setup();

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});

// The wildcard exclusion lives in the HOOK, so it is asserted against the hook running for real over
// a stubbed fetch (the queryHarness contract) rather than through the component — a component test
// with a mocked hook could never have caught a broken filter.
describe('useAssignableStreams (ADR-0042 clause 4)', () => {
  it('omits the wildcard stream a topic may never claim', async () => {
    const actual = await vi.importActual<typeof import('../../api/members')>('../../api/members');
    stubFetch((url) => (url.includes('/members/streams') ? { jsonBody: [CORE, GOV, WILDCARD] } : undefined));
    const { wrapper } = makeQueryWrapper();

    const { result } = renderHook(() => actual.useAssignableStreams(), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(result.current.data?.map((s: StreamRef) => s.code)).toEqual(['core', 'government']);
  });
});
