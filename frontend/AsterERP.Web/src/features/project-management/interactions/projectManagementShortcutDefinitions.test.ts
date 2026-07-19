// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import { resolveProjectManagementShortcut, shouldIgnoreProjectManagementShortcut } from './projectManagementShortcutDefinitions';

describe('projectManagement shortcuts', () => {
  it.each([
    [{ ctrlKey: true, key: 'n', metaKey: false, shiftKey: false }, { id: 'newTask' }],
    [{ ctrlKey: true, key: 'N', metaKey: false, shiftKey: true }, { id: 'newChildTask' }],
    [{ ctrlKey: true, key: 'f', metaKey: false, shiftKey: false }, { id: 'search' }],
    [{ ctrlKey: true, key: 'z', metaKey: false, shiftKey: false }, { id: 'undo' }],
    [{ ctrlKey: true, key: 'y', metaKey: false, shiftKey: false }, { id: 'redo' }],
    [{ ctrlKey: true, key: '4', metaKey: false, shiftKey: false }, { id: 'switchView', view: 'board' }],
  ] as const)('resolves %o', (event, expected) => {
    expect(resolveProjectManagementShortcut(event)).toEqual(expected);
  });

  it('does not take over editable controls', () => {
    const input = document.createElement('input');
    const editor = document.createElement('div');
    editor.setAttribute('contenteditable', 'true');
    expect(shouldIgnoreProjectManagementShortcut(input)).toBe(true);
    expect(shouldIgnoreProjectManagementShortcut(editor)).toBe(true);
    expect(shouldIgnoreProjectManagementShortcut(document.body)).toBe(false);
  });
});
