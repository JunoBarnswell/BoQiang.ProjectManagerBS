// @vitest-environment jsdom

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { TypedValueInput, resolveEnumOptions } from './TypedValueInput';

describe('TypedValueInput', () => {
  it('uses native boolean and numeric controls', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<><TypedValueInput ariaLabel="active" dataType="BOOLEAN" value="false" onChange={onChange} /><TypedValueInput ariaLabel="amount" dataType="DECIMAL(10,2)" value="1" onChange={onChange} /></>);
    await user.click(screen.getByRole('checkbox', { name: 'active' }));
    expect(onChange).toHaveBeenCalledWith('true');
    expect(screen.getByRole('spinbutton', { name: 'amount' }).getAttribute('type')).toBe('number');
  });

  it('uses JSON textarea and date controls', () => {
    render(<><TypedValueInput ariaLabel="payload" dataType="JSON" value="{}" onChange={vi.fn()} /><TypedValueInput ariaLabel="created" dataType="timestamp" value="2026-07-13T12:30:00Z" onChange={vi.fn()} /></>);
    expect(screen.getByRole('textbox', { name: 'payload' }).tagName).toBe('TEXTAREA');
    expect(screen.getByLabelText('created').getAttribute('type')).toBe('datetime-local');
  });

  it('uses enum options and binary file controls', () => {
    const { rerender } = render(<TypedValueInput ariaLabel="status" dataType="enum('draft','published')" value="draft" onChange={vi.fn()} />);
    expect(screen.getByRole('combobox', { name: 'status' })).toBeTruthy();
    expect(resolveEnumOptions("enum('draft','published')")).toEqual(['draft', 'published']);
    rerender(<TypedValueInput ariaLabel="binary-payload" dataType="blob" value="" onChange={vi.fn()} />);
    expect(screen.getByLabelText('binary-payload').getAttribute('type')).toBe('file');
  });
});
