import { useQuery } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';

import { getProjectManagementTasks } from '../../../api/project-management/projectManagement.api';
import type {
  ProjectManagementMemberCandidate,
  ProjectManagementMemberUpsertRequest,
} from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import {
  PmBox,
  PmButton,
  PmFormSelect,
  PmMenuItem,
  PmNotice,
  PmSelect,
  PmText,
} from '../../../ui/project-management';
import { useProjectManagementI18n } from '../projectManagementI18n';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import {
  filterEmploymentsForUser,
  resolveAutoEmploymentId,
} from './memberCandidateUtils';
import { ProjectMemberCandidateAutocomplete } from './ProjectMemberCandidateAutocomplete';

const projectMemberRoles = ['Owner', 'Manager', 'Lead', 'Member', 'Viewer'] as const;

export type ProjectMemberFormPanelProps = {
  editingId: string | null;
  excludeUserIds: ReadonlySet<string> | string[];
  form: ProjectManagementMemberUpsertRequest;
  onCancelEdit: () => void;
  onFormChange: (next: ProjectManagementMemberUpsertRequest) => void;
  onSubmit: () => void;
  pending?: boolean;
  projectId: string;
  selectedUserLabel?: string;
};

export function ProjectMemberFormPanel({
  editingId,
  excludeUserIds,
  form,
  onCancelEdit,
  onFormChange,
  onSubmit,
  pending = false,
  projectId,
  selectedUserLabel,
}: ProjectMemberFormPanelProps) {
  const { t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const [knownCandidates, setKnownCandidates] = useState<ProjectManagementMemberCandidate[]>([]);
  const [candidatesError, setCandidatesError] = useState(false);
  const [candidatesRetry, setCandidatesRetry] = useState<(() => void) | null>(null);

  const topicRootsQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryKey: projectManagementQueryKeys.tasks(scope, {
      projectId,
      pageIndex: 1,
      pageSize: 200,
      viewKey: 'tree',
      sortBy: 'tree',
      sortDirection: 'asc',
      includeCompleted: true,
    }),
    queryFn: ({ signal }) =>
      getProjectManagementTasks(
        {
          projectId,
          pageIndex: 1,
          pageSize: 200,
          viewKey: 'tree',
          sortBy: 'tree',
          sortDirection: 'asc',
          includeCompleted: true,
        },
        signal,
      ),
  });

  const topicRoots = useMemo(
    () => (topicRootsQuery.data?.data?.items ?? []).filter((task) => !task.parentTaskId),
    [topicRootsQuery.data?.data?.items],
  );

  const employmentCandidates = useMemo(
    () => filterEmploymentsForUser(knownCandidates, form.userId),
    [form.userId, knownCandidates],
  );

  const mergeCandidates = useCallback((items: ProjectManagementMemberCandidate[]) => {
    setKnownCandidates((prev) => {
      const map = new Map(prev.map((item) => [`${item.userId}:${item.employmentId}`, item]));
      for (const item of items) {
        map.set(`${item.userId}:${item.employmentId}`, item);
      }
      return Array.from(map.values());
    });
  }, []);

  const handleQueryStatusChange = useCallback((status: { isError: boolean; refetch: () => void }) => {
    setCandidatesError(status.isError);
    setCandidatesRetry(() => status.refetch);
  }, []);

  const handleUserChange = useCallback(
    (userId: string, candidate: ProjectManagementMemberCandidate | null) => {
      if (!userId) {
        onFormChange({ ...form, userId: '', employmentId: undefined });
        return;
      }
      const nextKnown = candidate
        ? (() => {
            const map = new Map(
              knownCandidates.map((item) => [`${item.userId}:${item.employmentId}`, item] as const),
            );
            map.set(`${candidate.userId}:${candidate.employmentId}`, candidate);
            return Array.from(map.values());
          })()
        : knownCandidates;
      if (candidate) mergeCandidates([candidate]);
      const employments = filterEmploymentsForUser(nextKnown, userId);
      const autoEmploymentId = resolveAutoEmploymentId(employments);
      onFormChange({
        ...form,
        userId,
        employmentId: autoEmploymentId,
      });
    },
    [form, knownCandidates, mergeCandidates, onFormChange],
  );

  const isEditing = Boolean(editingId);
  const employmentDisabled = !form.userId;
  const scopeDisabled = form.roleCode !== 'Lead' || topicRootsQuery.isLoading || topicRootsQuery.isError;
  const canSubmit = Boolean(form.userId.trim() && form.employmentId && !pending);

  return (
    <PmBox className="pm-member-form-panel">
      <PmBox className="pm-member-form-panel__head">
        <PmText className="pm-member-form-panel__title" component="h2" fontWeight={700}>
          {isEditing ? t('projectManagement.members.form.editTitle') : t('projectManagement.members.form.title')}
        </PmText>
        <PmText className="pm-member-form-panel__hint" color="text.secondary" component="p" fontSize=".75rem">
          {t('projectManagement.members.form.hint')}
        </PmText>
      </PmBox>

      <PmBox className="pm-member-form-panel__fields">
        <ProjectMemberCandidateAutocomplete
          disabled={isEditing}
          excludeUserIds={excludeUserIds}
          onCandidatesChange={mergeCandidates}
          onQueryStatusChange={handleQueryStatusChange}
          onUserChange={handleUserChange}
          value={form.userId}
          valueLabel={selectedUserLabel}
        />

        <PmBox className="pm-member-form-panel__row">
          <PmSelect
            disabled={employmentDisabled}
            displayEmpty
            fullWidth
            inputProps={{ 'aria-label': t('projectManagement.members.form.employment') }}
            onChange={(event) => {
              const employmentId = String(event.target.value);
              const match = employmentCandidates.find((item) => item.employmentId === employmentId);
              onFormChange({ ...form, employmentId: match?.employmentId || employmentId || undefined });
            }}
            size="small"
            value={form.employmentId ?? ''}
          >
            <PmMenuItem value="">{t('projectManagement.members.form.selectEmployment')}</PmMenuItem>
            {form.employmentId &&
            !employmentCandidates.some((candidate) => candidate.employmentId === form.employmentId) ? (
              <PmMenuItem value={form.employmentId}>
                {t('projectManagement.members.form.employmentUnavailable')}
              </PmMenuItem>
            ) : null}
            {employmentCandidates
              .filter((candidate) => candidate.isSelectable || candidate.employmentId === form.employmentId)
              .map((candidate) => (
                <PmMenuItem key={candidate.employmentId} value={candidate.employmentId}>
                  {candidate.employmentName ||
                    candidate.positionName ||
                    t('projectManagement.members.form.defaultEmployment')}
                </PmMenuItem>
              ))}
          </PmSelect>

          <PmFormSelect
            label={t('projectManagement.members.form.role')}
            onChange={(event) => {
              const roleCode = String(event.target.value);
              onFormChange({
                ...form,
                roleCode,
                scopeRootTaskId: roleCode === 'Lead' ? form.scopeRootTaskId : undefined,
              });
            }}
            value={form.roleCode ?? ''}
          >
            {projectMemberRoles.map((roleCode) => (
              <PmMenuItem key={roleCode} value={roleCode}>
                {t(`projectManagement.memberRole.${roleCode}`)}
              </PmMenuItem>
            ))}
          </PmFormSelect>

          <PmSelect
            disabled={scopeDisabled}
            displayEmpty
            fullWidth
            inputProps={{ 'aria-label': t('projectManagement.members.form.scope') }}
            onChange={(event) => {
              const next = String(event.target.value);
              onFormChange({ ...form, scopeRootTaskId: next || undefined });
            }}
            size="small"
            value={form.scopeRootTaskId ?? ''}
          >
            <PmMenuItem value="">{t('projectManagement.members.form.scopeWholeProject')}</PmMenuItem>
            {form.scopeRootTaskId && !topicRoots.some((task) => task.id === form.scopeRootTaskId) ? (
              <PmMenuItem value={form.scopeRootTaskId}>
                {t('projectManagement.members.form.scopeUnavailable')}
              </PmMenuItem>
            ) : null}
            {topicRoots.map((task) => (
              <PmMenuItem key={task.id} value={task.id}>
                {task.taskCode} · {task.title}
              </PmMenuItem>
            ))}
          </PmSelect>
        </PmBox>

        {form.roleCode === 'Lead' ? (
          <PmText color="text.secondary" fontSize=".7rem">
            {t('projectManagement.members.form.leadScopeHint')}
          </PmText>
        ) : null}
      </PmBox>

      <PmBox className="pm-member-form-panel__actions">
        <PermissionButton code="project-management:member:manage" disabled={!canSubmit} onClick={onSubmit}>
          {pending
            ? t('projectManagement.members.form.submitting')
            : isEditing
              ? t('projectManagement.members.form.save')
              : t('projectManagement.home.members.add')}
        </PermissionButton>
        {isEditing ? (
          <PmButton color="inherit" onClick={onCancelEdit} type="button">
            {t('projectManagement.members.form.cancelEdit')}
          </PmButton>
        ) : null}
      </PmBox>

      {candidatesError ? (
        <PmNotice severity="warning">
          {t('projectManagement.members.form.candidatesFailed')}
          {candidatesRetry ? (
            <PmButton color="inherit" onClick={() => candidatesRetry()} size="small" sx={{ ml: 1 }}>
              {t('projectManagement.members.form.retry')}
            </PmButton>
          ) : null}
        </PmNotice>
      ) : null}
      {topicRootsQuery.isError ? (
        <PmNotice severity="warning">
          {t('projectManagement.members.form.topicRootsFailed')}
          <PmButton color="inherit" onClick={() => void topicRootsQuery.refetch()} size="small" sx={{ ml: 1 }}>
            {t('projectManagement.members.form.retry')}
          </PmButton>
        </PmNotice>
      ) : null}
    </PmBox>
  );
}
