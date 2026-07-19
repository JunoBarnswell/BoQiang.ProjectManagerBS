// @vitest-environment jsdom

import { fireEvent, render, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ProjectManagementEscapeStack } from './ProjectManagementEscapeStack';
import { ProjectManagementUnsavedChangesDialog } from './ProjectManagementUnsavedChangesDialog';
import { useProjectManagementUnsavedChangesGuard } from './useProjectManagementUnsavedChangesGuard';

function GuardFixture({ onDiscard, onLeave, onSave }: { onDiscard: () => void; onLeave: () => void; onSave: () => void }) {
  const guard = useProjectManagementUnsavedChangesGuard({ isDirty: true, onDiscard, onSave });
  return <ProjectManagementEscapeStack>
    <button type="button" onClick={() => guard.requestLeave(onLeave)}>leave</button>
    <ProjectManagementUnsavedChangesDialog guard={guard} />
  </ProjectManagementEscapeStack>;
}

describe('ProjectManagementUnsavedChangesDialog', () => {
  it('offers save, discard and cancel without navigating on cancel', async () => {
    const onDiscard = vi.fn();
    const onLeave = vi.fn();
    const onSave = vi.fn();
    const { getByRole, getByText, queryByRole } = render(<GuardFixture onDiscard={onDiscard} onLeave={onLeave} onSave={onSave} />);

    fireEvent.click(getByText('leave'));
    expect(getByRole('dialog', { name: '未保存的更改' })).not.toBeNull();
    fireEvent.click(getByText('取消'));
    expect(onLeave).not.toHaveBeenCalled();
    expect(queryByRole('dialog', { name: '未保存的更改' })).toBeNull();

    fireEvent.click(getByText('leave'));
    fireEvent.click(getByText('不保存'));
    await waitFor(() => expect(onDiscard).toHaveBeenCalledOnce());
    expect(onLeave).toHaveBeenCalledOnce();

    fireEvent.click(getByText('leave'));
    fireEvent.click(getByText('保存并离开'));
    await waitFor(() => expect(onSave).toHaveBeenCalledOnce());
    expect(onLeave).toHaveBeenCalledTimes(2);
  });
});
