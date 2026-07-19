// @vitest-environment jsdom

import { render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ProjectManagementEscapeStack, useProjectManagementEscapeLayer } from './ProjectManagementEscapeStack';

function Layer({ onEscape }: { onEscape: () => void }) {
  useProjectManagementEscapeLayer(true, onEscape);
  return null;
}

describe('ProjectManagementEscapeStack', () => {
  it('closes only the most recently opened layer', () => {
    const first = vi.fn();
    const second = vi.fn();
    render(<ProjectManagementEscapeStack><Layer onEscape={first} /><Layer onEscape={second} /></ProjectManagementEscapeStack>);

    const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'Escape' });
    window.dispatchEvent(event);

    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledOnce();
    expect(event.defaultPrevented).toBe(true);
  });
});
