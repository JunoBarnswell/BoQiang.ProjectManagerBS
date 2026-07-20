import type { ProjectManagementMemberCandidate } from '../../../api/project-management/projectManagement.types';

export function formatMemberCandidate(candidate: {
  displayName: string;
  userName: string;
  deptName?: string;
  positionName?: string;
}): string {
  const organization = [candidate.deptName, candidate.positionName].filter(Boolean).join(' · ');
  const name = candidate.displayName || candidate.userName;
  return organization ? `${name} · ${organization}` : name;
}

export function dedupeUserCandidates(
  candidates: ProjectManagementMemberCandidate[],
): ProjectManagementMemberCandidate[] {
  return Array.from(
    candidates
      .reduce((byUser, candidate) => {
        const current = byUser.get(candidate.userId);
        if (!current || (candidate.isSelectable && !current.isSelectable)) {
          byUser.set(candidate.userId, candidate);
        }
        return byUser;
      }, new Map<string, ProjectManagementMemberCandidate>())
      .values(),
  );
}

export function filterEmploymentsForUser(
  candidates: ProjectManagementMemberCandidate[],
  userId: string,
): ProjectManagementMemberCandidate[] {
  if (!userId) return [];
  return candidates.filter((candidate) => candidate.userId === userId);
}

export function buildMemberSearchLabel(candidate: ProjectManagementMemberCandidate): {
  primary: string;
  secondary: string;
} {
  const primary = candidate.displayName || candidate.userName;
  const org = [candidate.deptName, candidate.positionName].filter(Boolean).join(' · ');
  const secondary = [candidate.userName !== primary ? candidate.userName : null, org]
    .filter(Boolean)
    .join(' · ');
  return { primary, secondary };
}

export function resolveAutoEmploymentId(
  employments: ProjectManagementMemberCandidate[],
): string | undefined {
  const selectable = employments.filter((item) => item.isSelectable);
  if (selectable.length === 1) return selectable[0].employmentId;
  return undefined;
}
