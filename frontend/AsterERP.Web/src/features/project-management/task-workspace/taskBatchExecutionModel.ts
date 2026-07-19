import type { ProjectManagementTaskBatchExecutionResult } from '../../../api/project-management/projectManagement.types';

export function taskBatchResultStatusLabel(status: ProjectManagementTaskBatchExecutionResult['items'][number]['status']): string {
  return ({ conflict: '冲突', failed: '失败', skipped: '跳过', succeeded: '成功' } as const)[status];
}

export function taskBatchResultToCsv(result: ProjectManagementTaskBatchExecutionResult): string {
  const escape = (value: unknown) => `"${String(value ?? '').replaceAll('"', '""')}"`;
  const rows = [
    ['operationId', 'projectId', 'taskId', 'taskCode', 'status', 'message', 'errorCode', 'versionNo'],
    ...result.items.map((item) => [
      result.operationId,
      result.projectId,
      item.taskId,
      item.taskCode ?? '',
      taskBatchResultStatusLabel(item.status),
      item.message ?? '',
      item.errorCode ?? '',
      item.versionNo ?? '',
    ]),
  ];
  return `\ufeff${rows.map((row) => row.map(escape).join(',')).join('\r\n')}\r\n`;
}
