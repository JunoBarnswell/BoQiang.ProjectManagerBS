import { useCallback, useEffect, useRef, useState } from 'react';

export interface ProjectManagementUnsavedChangesGuardOptions {
  isDirty: boolean;
  onDiscard: () => void | Promise<void>;
  onSave: () => void | Promise<void>;
}

export interface ProjectManagementUnsavedChangesGuard {
  cancel: () => void;
  discard: () => Promise<void>;
  isOpen: boolean;
  isSaving: boolean;
  requestLeave: (leave: () => void) => void;
  save: () => Promise<void>;
}

export function useProjectManagementUnsavedChangesGuard({ isDirty, onDiscard, onSave }: ProjectManagementUnsavedChangesGuardOptions): ProjectManagementUnsavedChangesGuard {
  const pendingLeaveRef = useRef<(() => void) | undefined>(undefined);
  const [isOpen, setIsOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const cancel = useCallback(() => {
    pendingLeaveRef.current = undefined;
    setIsOpen(false);
  }, []);

  const complete = useCallback(() => {
    const leave = pendingLeaveRef.current;
    pendingLeaveRef.current = undefined;
    setIsOpen(false);
    leave?.();
  }, []);

  const requestLeave = useCallback((leave: () => void) => {
    if (!isDirty) {
      leave();
      return;
    }
    pendingLeaveRef.current = leave;
    setIsOpen(true);
  }, [isDirty]);

  const save = useCallback(async () => {
    if (isSaving) return;
    try {
      setIsSaving(true);
      await onSave();
      complete();
    } finally {
      setIsSaving(false);
    }
  }, [complete, isSaving, onSave]);

  const discard = useCallback(async () => {
    if (isSaving) return;
    try {
      setIsSaving(true);
      await onDiscard();
      complete();
    } finally {
      setIsSaving(false);
    }
  }, [complete, isSaving, onDiscard]);

  useEffect(() => {
    if (!isDirty) cancel();
  }, [cancel, isDirty]);

  useEffect(() => {
    if (!isDirty) return;
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [isDirty]);

  return { cancel, discard, isOpen, isSaving, requestLeave, save };
}
