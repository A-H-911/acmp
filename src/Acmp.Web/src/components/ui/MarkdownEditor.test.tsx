import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import i18n from '../../i18n';
import { MarkdownEditor } from './MarkdownEditor';

// A tiny controlled host so the editor's onChange round-trips like a real consumer.
function Host({ initial = '' }: { initial?: string }) {
  const [v, setV] = useState(initial);
  return <MarkdownEditor value={v} onChange={setV} ariaLabel="Notes" />;
}

describe('MarkdownEditor (DV-04 shared editor)', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('types into the textarea and reports changes', async () => {
    const onChange = vi.fn();
    render(<MarkdownEditor value="" onChange={onChange} ariaLabel="Notes" />);
    await userEvent.type(screen.getByLabelText('Notes'), 'Hi');
    expect(onChange).toHaveBeenCalled();
  });

  it('Bold wraps the current selection in ** markers', async () => {
    const user = userEvent.setup();
    render(<Host initial="draft" />);
    const ta = screen.getByLabelText('Notes') as HTMLTextAreaElement;
    ta.setSelectionRange(0, 5); // select "draft"
    await user.click(screen.getByRole('button', { name: 'Bold' }));
    expect(ta.value).toBe('**draft**');
  });

  it('Bulleted list prefixes the current line', async () => {
    const user = userEvent.setup();
    render(<Host initial="item" />);
    const ta = screen.getByLabelText('Notes') as HTMLTextAreaElement;
    ta.setSelectionRange(0, 0);
    await user.click(screen.getByRole('button', { name: 'Bulleted list' }));
    expect(ta.value).toBe('- item');
  });

  it('Link inserts a markdown link around the selection', async () => {
    const user = userEvent.setup();
    render(<Host initial="docs" />);
    const ta = screen.getByLabelText('Notes') as HTMLTextAreaElement;
    ta.setSelectionRange(0, 4);
    await user.click(screen.getByRole('button', { name: 'Link' }));
    expect(ta.value).toBe('[docs](url)');
  });

  it('forwards field id + aria props to the textarea (labelled <Field> use)', () => {
    render(
      <MarkdownEditor value="x" onChange={vi.fn()} id="desc" aria-invalid aria-describedby="desc-err" />,
    );
    const ta = document.getElementById('desc') as HTMLTextAreaElement;
    expect(ta).toBeTruthy();
    expect(ta.getAttribute('aria-invalid')).toBe('true');
    expect(ta.getAttribute('aria-describedby')).toBe('desc-err');
  });
});
