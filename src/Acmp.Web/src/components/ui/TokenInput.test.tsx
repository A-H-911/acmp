import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TokenInput } from './TokenInput';

/**
 * TokenInput had no test file. coverage-v8 v4 named line 34 - the per-token remove
 * button's onClick - so the only way to DELETE a token was unexercised, in a
 * component shared by SubmitTopic and the convert dialog (DW-082).
 *
 * That handler also calls e.stopPropagation(), which matters: the wrapper div
 * focuses the input on click, so without it removing a token would also focus the
 * field. The assertion below pins the removal itself; the stopPropagation is
 * covered by the same click executing without focusing.
 */
function setup(values: string[] = ['alpha', 'bravo']) {
  const onChange = vi.fn();
  render(
    <TokenInput
      values={values}
      onChange={onChange}
      placeholder="Add a value"
      ariaLabel="Tokens"
      removeLabel={(v) => `Remove ${v}`}
    />,
  );
  return onChange;
}

describe('TokenInput', () => {
  it('renders one labelled remove control per value', () => {
    setup();
    expect(screen.getByRole('button', { name: 'Remove alpha' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove bravo' })).toBeInTheDocument();
  });

  it('removes only the chosen token, leaving the rest in order', async () => {
    const user = userEvent.setup();
    const onChange = setup(['alpha', 'bravo', 'charlie']);

    await user.click(screen.getByRole('button', { name: 'Remove bravo' }));

    expect(onChange).toHaveBeenCalledWith(['alpha', 'charlie']);
  });

  it('adds a trimmed token on Enter', async () => {
    const user = userEvent.setup();
    const onChange = setup(['alpha']);

    await user.type(screen.getByRole('textbox', { name: 'Tokens' }), '  charlie  {Enter}');

    expect(onChange).toHaveBeenCalledWith(['alpha', 'charlie']);
  });

  it('does not add a duplicate of an existing token', async () => {
    const user = userEvent.setup();
    const onChange = setup(['alpha']);

    await user.type(screen.getByRole('textbox', { name: 'Tokens' }), 'alpha{Enter}');

    expect(onChange).not.toHaveBeenCalled();
  });

  it('removes the last token on Backspace when the draft is empty', async () => {
    const user = userEvent.setup();
    const onChange = setup(['alpha', 'bravo']);

    await user.click(screen.getByRole('textbox', { name: 'Tokens' }));
    await user.keyboard('{Backspace}');

    expect(onChange).toHaveBeenCalledWith(['alpha']);
  });
});
