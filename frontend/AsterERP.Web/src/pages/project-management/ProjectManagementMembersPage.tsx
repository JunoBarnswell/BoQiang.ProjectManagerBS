import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';

import {
  createProjectManagementMember,
  deleteProjectManagementMember,
  getProjectManagementMembers,
  getProjectManagementTasks,
  updateProjectManagementMember,
} from '../../api/project-management/projectManagement.api';
import type {
  ProjectManagementMember,
  ProjectManagementMemberUpsertRequest,
} from '../../api/project-management/projectManagement.types';
import { isHttpError } from '../../core/http/httpError';
import { projectManagementQueryKeys } from '../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { ProjectMemberFormPanel } from '../../features/project-management/members/ProjectMemberFormPanel';
import { useProjectManagementI18n } from '../../features/project-management/projectManagementI18n';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import '../../features/project-management/ui/projectWorkbench.css';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../shared/auth/PermissionGuard';
import { useMessage } from '../../shared/feedback/useMessage';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';
import { PmChip } from '../../ui/project-management';

const emptyForm: ProjectManagementMemberUpsertRequest = { userId: '', roleCode: 'Member', versionNo: 0 };

function roleChipColor(roleCode: string): 'default' | 'primary' | 'success' | 'warning' | 'error' {
  switch (roleCode) {
    case 'Owner':
      return 'primary';
    case 'Manager':
      return 'success';
    case 'Lead':
      return 'warning';
    case 'Viewer':
      return 'default';
    default:
      return 'default';
  }
}

export function ProjectManagementMembersPage() {
  const { t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);

  const membersQuery = useQuery({
    queryKey: projectManagementQueryKeys.members(scope, projectId),
    queryFn: ({ signal }) => getProjectManagementMembers(projectId, signal),
    enabled: scope.isAvailable && Boolean(projectId),
  });

  const topicRootsQuery = useQuery({
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
    enabled: scope.isAvailable && Boolean(projectId),
  });

  useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
      event.returnValue = '成员表单有未保存更改。';
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [dirty]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.members(scope, projectId) });

  const saveMutation = useApiMutation({
    mutationFn: () =>
      editingId
        ? updateProjectManagementMember(projectId, editingId, form)
        : createProjectManagementMember(projectId, form),
    onError: (error) => message.error(getErrorMessage(error, editingId ? '成员保存失败' : '成员添加失败')),
    onSuccess: async () => {
      message.success(editingId ? '成员已更新' : '成员已添加');
      setForm(emptyForm);
      setEditingId(null);
      setDirty(false);
      await refresh();
    },
  });

  const deleteMutation = useApiMutation({
    mutationFn: (member: ProjectManagementMember) =>
      deleteProjectManagementMember(projectId, member.id, member.versionNo),
    onError: (error) => message.error(getErrorMessage(error, '成员移除失败')),
    onSuccess: async () => {
      message.success('成员已移除');
      await refresh();
    },
  });

  const members = membersQuery.data?.data?.items ?? [];
  const excludeUserIds = useMemo(
    () => new Set(members.filter((member) => member.isActive).map((member) => member.userId)),
    [members],
  );
  const topicRoots = (topicRootsQuery.data?.data?.items ?? []).filter((task) => !task.parentTaskId);
  const topicRootLabels = Object.fromEntries(topicRoots.map((task) => [task.id, `${task.taskCode} · ${task.title}`]));

  const updateForm = (next: ProjectManagementMemberUpsertRequest) => {
    setForm(next);
    setDirty(true);
  };

  const selectedUserLabel = useMemo(() => {
    if (!form.userId) return undefined;
    const member = members.find((item) => item.userId === form.userId);
    return member?.displayName;
  }, [form.userId, members]);

  if (membersQuery.isLoading) return <PageLoading />;
  if (membersQuery.isError) {
    if (isHttpError(membersQuery.error) && membersQuery.error.status === 403) return <Page403 />;
    return (
      <PageError
        description="成员列表加载失败"
        action={
          <button type="button" onClick={() => void membersQuery.refetch()}>
            重试
          </button>
        }
      />
    );
  }

  return (
    <ResponsivePage title="项目成员" description="维护项目成员、角色与任务范围。" eyebrow="ProjectManagement / Members">
      <PermissionGuard code="project-management:member:manage" fallback={null}>
        <ProjectMemberFormPanel
          editingId={editingId}
          excludeUserIds={editingId ? [] : excludeUserIds}
          form={form}
          onCancelEdit={() => {
            setEditingId(null);
            setForm(emptyForm);
            setDirty(false);
          }}
          onFormChange={updateForm}
          onSubmit={() => saveMutation.mutate()}
          pending={saveMutation.isPending}
          projectId={projectId}
          selectedUserLabel={selectedUserLabel}
        />
      </PermissionGuard>

      {members.length === 0 ? (
        <div className="pm-member-table__empty">{t('projectManagement.members.list.empty')}</div>
      ) : (
        <div className="pm-member-table">
          <table>
            <thead>
              <tr>
                <th>{t('projectManagement.members.list.user')}</th>
                <th>{t('projectManagement.members.list.role')}</th>
                <th>{t('projectManagement.members.list.scope')}</th>
                <th>{t('projectManagement.members.list.status')}</th>
                <th>{t('projectManagement.members.list.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {members.map((member) => (
                <tr key={member.id}>
                  <td>
                    <div className="pm-member-table__user">
                      <span className="pm-member-table__user-name">
                        {member.displayName || t('projectManagement.members.form.userUnavailable')}
                      </span>
                      <span className="pm-member-table__user-meta">{member.userId}</span>
                    </div>
                  </td>
                  <td>
                    <PmChip color={roleChipColor(member.roleCode)} label={member.roleCode} />
                  </td>
                  <td>
                    {member.scopeRootTaskId
                      ? topicRootLabels[member.scopeRootTaskId] ?? t('projectManagement.members.form.scopeUnavailable')
                      : t('projectManagement.members.list.scopeAll')}
                  </td>
                  <td>
                    {member.isActive
                      ? t('projectManagement.members.list.active')
                      : t('projectManagement.members.list.left')}
                  </td>
                  <td>
                    <div className="flex gap-2">
                      <PermissionButton
                        code="project-management:member:manage"
                        onClick={() => {
                          setEditingId(member.id);
                          setForm({
                            userId: member.userId,
                            employmentId: member.employmentId,
                            roleCode: member.roleCode,
                            scopeRootTaskId: member.scopeRootTaskId,
                            versionNo: member.versionNo,
                          });
                          setDirty(false);
                        }}
                      >
                        {t('projectManagement.members.list.edit')}
                      </PermissionButton>
                      <PermissionButton
                        code="project-management:member:manage"
                        disabled={deleteMutation.isPending}
                        onClick={() => deleteMutation.mutate(member)}
                      >
                        {t('projectManagement.members.list.remove')}
                      </PermissionButton>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </ResponsivePage>
  );
}
