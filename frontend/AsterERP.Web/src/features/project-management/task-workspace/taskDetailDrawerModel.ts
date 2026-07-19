import type {
  ProjectManagementTaskDetail,
  ProjectManagementTaskUpsertRequest,
  ProjectManagementTaskVersionConflictResponse,
} from '../../../api/project-management/projectManagement.types';
import { isHttpError } from '../../../core/http/httpError';

export const taskDetailSections = [
  { key: 'basic', label: '基本信息' },
  { key: 'children', label: '子任务' },
  { key: 'comments', label: '评论' },
  { key: 'attachments', label: '附件' },
  { key: 'reminders', label: '提醒' },
  { key: 'dependencies', label: '依赖' },
  { key: 'activity', label: '活动' },
] as const;

export type TaskDetailSection = typeof taskDetailSections[number]['key'];

export function taskDetailToForm(task: ProjectManagementTaskDetail, versionNo = task.versionNo): ProjectManagementTaskUpsertRequest {
  return {
    assigneeEmploymentId: task.assigneeEmploymentId,
    assigneeUserId: task.assigneeUserId,
    description: task.markdown ?? task.description,
    dueDate: task.dueDate,
    estimateMinutes: task.estimateMinutes,
    markdown: task.markdown,
    milestoneId: task.milestoneId,
    parentTaskId: task.parentTaskId,
    priority: task.priority,
    progressPercent: task.progressPercent,
    startDate: task.startDate,
    status: task.status,
    summary: task.summary,
    taskCode: task.taskCode,
    title: task.title,
    versionNo,
    weight: task.weight,
  };
}

export function readProjectManagementTaskConflict(error: unknown): ProjectManagementTaskVersionConflictResponse | null {
  const candidate = isHttpError(error) ? error.data : error;
  if (!isRecord(candidate) || !isRecord(candidate.serverValues) || !isRecord(candidate.localValues)) return null;
  if (!isTaskDetail(candidate.serverValues) || !isRecord(candidate.localValues)) return null;
  const fields = Array.isArray(candidate.fieldConflicts) ? candidate.fieldConflicts.filter(isConflictField) : [];
  return {
    fieldConflicts: fields,
    localValues: candidate.localValues as unknown as ProjectManagementTaskVersionConflictResponse['localValues'],
    serverValues: candidate.serverValues as unknown as ProjectManagementTaskDetail,
  };
}

function isTaskDetail(value: Record<string, unknown>): boolean {
  return typeof value.id === 'string' && typeof value.taskCode === 'string' && typeof value.title === 'string' && typeof value.versionNo === 'number';
}

function isConflictField(value: unknown): value is ProjectManagementTaskVersionConflictResponse['fieldConflicts'][number] {
  return isRecord(value) && typeof value.field === 'string' && typeof value.displayName === 'string' && 'serverValue' in value && 'localValue' in value;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}
