// @vitest-environment jsdom

import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useProjectManagementGlobalShortcuts } from './useProjectManagementGlobalShortcuts';

function ShortcutFixture({ canExecute = () => true, onNewTask, onSwitchView }: { canExecute?: (shortcut: string) => boolean; onNewTask: () => void; onSwitchView: (view: string) => void }) {
  useProjectManagementGlobalShortcuts({ newTask: onNewTask, switchView: onSwitchView }, { canExecute });
  return <input aria-label="editable" />;
}

describe('useProjectManagementGlobalShortcuts', () => {
  it('handles supported commands but leaves inputs and editors untouched', () => {
    const onNewTask = vi.fn();
    const onSwitchView = vi.fn();
    const { getByLabelText, unmount } = render(<ShortcutFixture onNewTask={onNewTask} onSwitchView={onSwitchView} />);

    const taskEvent = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ctrlKey: true, key: 'n' });
    window.dispatchEvent(taskEvent);
    expect(onNewTask).toHaveBeenCalledTimes(1);
    expect(taskEvent.defaultPrevented).toBe(true);

    const viewEvent = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ctrlKey: true, key: '5' });
    window.dispatchEvent(viewEvent);
    expect(onSwitchView).toHaveBeenCalledWith('gantt');

    fireEvent.keyDown(getByLabelText('editable'), { ctrlKey: true, key: 'n' });
    expect(onNewTask).toHaveBeenCalledTimes(1);
    unmount();
  });

  it('does not bypass the caller permission decision', () => {
    const onNewTask = vi.fn();
    const { unmount } = render(<ShortcutFixture canExecute={() => false} onNewTask={onNewTask} onSwitchView={vi.fn()} />);
    const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ctrlKey: true, key: 'n' });
    window.dispatchEvent(event);
    expect(onNewTask).not.toHaveBeenCalled();
    expect(event.defaultPrevented).toBe(false);
    unmount();
  });
});
