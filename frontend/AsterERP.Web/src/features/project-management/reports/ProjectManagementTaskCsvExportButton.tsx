import { useMemo } from 'react';
import { useLocation } from 'react-router-dom';

import { exportProjectManagementTasksCsv } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementTaskLabelFilter, ProjectManagementTaskView } from '../../../api/project-management/projectManagement.types';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { useTaskWorkspaceUrlState } from '../hooks/useTaskWorkspaceUrlState';
import { taskWorkspaceStateToQuery } from '../state/taskWorkspaceState';

interface ProjectManagementTaskCsvExportButtonProps {
  filter: ProjectManagementTaskLabelFilter;
  projectId: string;
}

export function ProjectManagementTaskCsvExportButton({ filter, projectId }: ProjectManagementTaskCsvExportButtonProps) {
  const location = useLocation();
  const message = useMessage();
  const viewKey = resolveView(location.pathname);
  const { state } = useTaskWorkspaceUrlState(viewKey);
  const query = useMemo(() => ({
    ...taskWorkspaceStateToQuery(projectId, state),
    labelFilter: filter.labelIds.length > 0 ? filter : undefined,
  }), [filter, projectId, state]);
  const mutation = useApiMutation({
    mutationFn: () => exportProjectManagementTasksCsv(query),
    onError: (error) => message.error(getErrorMessage(error, '任务 CSV 导出失败')),
    onSuccess: (result) => download(result.blob, result.fileName, message),
  });

  return (
    <PermissionButton code="project-management:report:export" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
      {mutation.isPending ? '导出中…' : '导出当前筛选 CSV'}
    </PermissionButton>
  );
}

function resolveView(pathname: string): ProjectManagementTaskView {
  if (pathname.endsWith('/list')) return 'list';
  if (pathname.endsWith('/card')) return 'card';
  if (pathname.endsWith('/board')) return 'board';
  if (pathname.endsWith('/gantt')) return 'gantt';
  if (pathname.endsWith('/calendar')) return 'calendar';
  return 'tree';
}

function download(blob: Blob, fileName: string, message: { success: (content: string) => void }) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
  message.success(`已生成 ${fileName}`);
}
