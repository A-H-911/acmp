import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TemplatePicker } from './TemplatePicker';

/* The picker reads the P15d/P15e templates seam — stub both hooks. `state` lets each test set the
 * register contents and the detail-fetch flag without re-mocking the module. */
const state = vi.hoisted(() => ({
  items: [] as { id: string; key: string; name: { en: string; ar: string }; targetType: string; status: string; version: number }[],
  fetching: false,
}));

vi.mock('../../api/templates', () => ({
  useTemplates: () => ({ data: { items: state.items } }),
  useTemplate: (key: string | undefined) => ({
    data: key ? { key, body: `BODY-OF-${key}` } : undefined,
    isFetching: state.fetching,
  }),
}));

const tpl = (key: string, en: string) => ({
  id: key, key, name: { en, ar: en }, targetType: 'Adr', status: 'Active', version: 1,
});

async function selectFirst(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Start from a template' }));
  await user.click(screen.getByRole('option', { name: /TPL-2026-001/ }));
}

describe('TemplatePicker (P15h / FR-120)', () => {
  beforeEach(() => {
    state.items = [tpl('TPL-2026-001', 'Standard ADR'), tpl('TPL-2026-002', 'Lightweight ADR')];
    state.fetching = false;
  });

  it('renders nothing when there are no active templates for the target type', () => {
    state.items = [];
    const { container } = render(<TemplatePicker targetType="Adr" onApply={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('lists the templates for the target type', async () => {
    const user = userEvent.setup();
    render(<TemplatePicker targetType="Adr" onApply={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Start from a template' }));
    expect(screen.getByRole('option', { name: /Standard ADR/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Lightweight ADR/ })).toBeInTheDocument();
  });

  it('applies the selected template body to the caller on Use template', async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<TemplatePicker targetType="Adr" onApply={onApply} />);
    await selectFirst(user);
    await user.click(screen.getByRole('button', { name: 'Use template' }));
    expect(onApply).toHaveBeenCalledWith('BODY-OF-TPL-2026-001');
  });

  it('disables Use template until a template is selected', () => {
    render(<TemplatePicker targetType="Adr" onApply={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Use template' })).toBeDisabled();
  });

  it('disables Use template while the template body is still loading', async () => {
    state.fetching = true;
    const user = userEvent.setup();
    render(<TemplatePicker targetType="Adr" onApply={vi.fn()} />);
    await selectFirst(user);
    expect(screen.getByRole('button', { name: 'Use template' })).toBeDisabled();
  });

  it('guards against clobbering existing content: Apply disabled + hint shown when hasContent', async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<TemplatePicker targetType="Adr" onApply={onApply} hasContent />);
    await selectFirst(user);
    expect(screen.getByText('Clear the field to use a template.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Use template' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Use template' }));
    expect(onApply).not.toHaveBeenCalled();
  });
});
