export type WorkspaceTransitionBlocker = {
  isDirty: () => boolean;
  reason: string;
};

const blockers = new Map<string, WorkspaceTransitionBlocker>();

export function registerWorkspaceTransitionBlocker(id: string, blocker: WorkspaceTransitionBlocker): () => void {
  blockers.set(id, blocker);
  return () => blockers.delete(id);
}

export function getWorkspaceTransitionBlockers(): string[] {
  return Array.from(blockers.values())
    .filter((blocker) => blocker.isDirty())
    .map((blocker) => blocker.reason);
}
