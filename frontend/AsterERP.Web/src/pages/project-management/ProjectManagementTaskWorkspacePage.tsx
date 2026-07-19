import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useParams } from 'react-router-dom';

import {
  createProjectManagementSavedView,
  deleteProjectManagementSavedView,
  ensureProjectManagementImConversation,
  createProjectManagementTask,
  changeProjectManagementTaskStatus,
  createProjectManagementTaskComment,
  downloadProjectManagementTaskAttachment,
  executeProjectManagementTasksBatch,
  deleteProjectManagementTask,
  deleteProjectManagementTaskComment,
  createProjectManagementTaskReminders,
  cancelProjectManagementTaskReminder,
  deleteProjectManagementTaskReminder,
  getProjectManagementLabels,
  getProjectManagementImConversation,
  getProjectManagementMemberCandidates,
  getProjectManagementMilestones,
  getProjectManagementMembers,
  getProjectManagementSavedViews,
  getProjectManagementTaskAttachments,
  getProjectManagementTaskComments,
  getProjectManagementTask,
  getProjectManagementTaskLabels,
  getProjectManagementTaskDependencies,
  getProjectManagementTaskReminders,
  getProjectManagementTasks,
  exportProjectManagementTasksCsv,
  previewProjectManagementTaskAttachment,
  moveProjectManagementTask,
  setProjectManagementTaskLabels,
  updateProjectManagementTask,
  updateProjectManagementTaskComment,
  updateProjectManagementSavedView,
} from '../../api/project-management/projectManagement.api';
import type {
  ProjectManagementTaskAttachment,
  ProjectManagementTaskComment,
  ProjectManagementTaskCommentUpsertRequest,
  ProjectManagementActivityQuery,
  ProjectManagementTaskBatchUpdateRequest,
  ProjectManagementTaskBatchExecutionResult,
  ProjectManagementTaskDetail,
  ProjectManagementTaskLabelFilter,
  ProjectManagementTaskListItem,
  ProjectManagementTaskReminder,
  ProjectManagementTaskReminderCreateRequest,
  ProjectManagementTaskUpsertRequest,
  ProjectManagementTaskView,
} from '../../api/project-management/projectManagement.types';
import { isHttpError } from '../../core/http/httpError';
import { usePermission } from '../../core/auth/usePermission';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useOpenImConversation } from '../../features/im/hooks/useOpenImConversation';
import '../../features/project-management/projectManagement.css';
import { useProjectManagementRealtimeConnection } from '../../features/project-management/hooks/useProjectManagementRealtimeConnection';
import { useTaskWorkspaceUrlState } from '../../features/project-management/hooks/useTaskWorkspaceUrlState';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { taskWorkspaceStateToQuery, taskWorkspaceStateToSavedView } from '../../features/project-management/state/taskWorkspaceState';
import { SavedViewManager } from '../../features/project-management/task-workspace/SavedViewManager';
import { createTaskMoveRequest, type TaskGroupDropTarget } from '../../features/project-management/task-workspace/taskMoveIntent';
import { TaskWorkspaceBatchCommandPanel } from '../../features/project-management/task-workspace/TaskWorkspaceBatchCommandPanel';
import { TaskWorkspaceBatchResultPanel } from '../../features/project-management/task-workspace/TaskWorkspaceBatchResultPanel';
import { taskBatchResultToCsv } from '../../features/project-management/task-workspace/taskBatchExecutionModel';
import { resolveBoardStatusProgress, rollbackBoardStatus } from '../../features/project-management/task-workspace/taskBoardStatusMutationModel';
import { applyOptimisticBoardMove, clearOptimisticBoardMove, rollbackOptimisticBoardMove } from '../../features/project-management/task-workspace/taskBoardInteractionModel';
import { TaskWorkspaceImConversationPanel } from '../../features/project-management/task-workspace/TaskWorkspaceImConversationPanel';
import { TaskWorkspaceLabelManager } from '../../features/project-management/task-workspace/TaskWorkspaceLabelManager';
import { TaskWorkspaceProjection } from '../../features/project-management/task-workspace/TaskWorkspaceProjection';
import { TaskWorkspaceSelectionPanel } from '../../features/project-management/task-workspace/TaskWorkspaceSelectionPanel';
import { TaskDetailChildrenSection, TaskDetailDependenciesSection, TaskDetailDrawer } from '../../features/project-management/task-workspace/TaskDetailDrawer';
import { readProjectManagementTaskConflict, taskDetailToForm, type TaskDetailSection } from '../../features/project-management/task-workspace/taskDetailDrawerModel';
import { TaskWorkspaceToolbar } from '../../features/project-management/task-workspace/TaskWorkspaceToolbar';
import { ProjectManagementTaskAttachmentPreviewDialog } from '../../features/project-management/task-workspace/ProjectManagementTaskAttachmentPreviewDialog';
import { uploadProjectManagementTaskAttachmentWithProgress } from '../../features/project-management/task-workspace/taskAttachmentUpload.api';
import { getProjectManagementTaskActivities } from '../../features/project-management/task-workspace/taskActivity.api';
import { TaskActivityTimeline } from '../../features/project-management/task-workspace/TaskActivityTimeline';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import { saveBlob } from '../../shared/file-preview/filePreviewUtils';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { getErrorMessage } from '../../shared/utils/errorMessage';

const emptyForm: ProjectManagementTaskUpsertRequest = {
  priority: 'Medium',
  progressPercent: 0,
  status: 'Todo',
  taskCode: '',
  title: '',
  weight: 1,
};

type QuickTaskAction = 'complete' | 'delete';

function toTaskUpsertRequest(task: ProjectManagementTaskDetail, status = task.status): ProjectManagementTaskUpsertRequest {
  return {
    assigneeEmploymentId: task.assigneeEmploymentId,
    assigneeUserId: task.assigneeUserId,
    description: task.markdown ?? task.description,
    dueDate: task.dueDate,
    estimateMinutes: task.estimateMinutes,
    milestoneId: task.milestoneId,
    parentTaskId: task.parentTaskId,
    priority: task.priority,
    progressPercent: status === 'Done' ? 100 : task.progressPercent,
    startDate: task.startDate,
    status,
    taskCode: task.taskCode,
    title: task.title,
    versionNo: task.versionNo,
    weight: task.weight,
  };
}

function resolveView(pathname: string): ProjectManagementTaskView {
  if (pathname.endsWith('/list')) return 'list';
  if (pathname.endsWith('/card')) return 'card';
  if (pathname.endsWith('/board')) return 'board';
  if (pathname.endsWith('/gantt')) return 'gantt';
  if (pathname.endsWith('/calendar')) return 'calendar';
  return 'tree';
}

const taskViewMeta: Record<ProjectManagementTaskView, { description: string; label: string; note?: string }> = {
  tree: { label: '任务树', description: '按父子层级查看并执行当前项目的真实任务命令。' },
  list: { label: '任务列表', description: '以同一查询口径平铺显示任务，筛选和权限与其他视图一致。' },
  card: { label: '任务卡片', description: '以卡片投影查看当前筛选结果，任务编辑仍走统一命令。' },
  board: { label: '任务看板', description: '按任务状态分列显示当前筛选结果，拖动仍由后端校验。' },
  gantt: { label: '计划投影', description: '按现有开始/截止日期显示任务计划数据。', note: '当前为计划数据投影；可缩放时间轴、依赖连线、关键路径和日期拖动由后续甘特 Case 实现。' },
  calendar: { label: '项目日历', description: '按当前筛选和权限显示连续任务计划，支持月/周切换、当日任务抽屉和调期。' }
};

export function ProjectManagementTaskWorkspacePage() {
  const scope = useProjectManagementWorkspaceScope();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const { pathname } = useLocation();
  const viewKey = resolveView(pathname);
  const { state, setState } = useTaskWorkspaceUrlState(viewKey);
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const { hasPermission: canViewTaskActivities } = usePermission('project-management:audit:view');
  const openImConversation = useOpenImConversation();
  const [creating, setCreating] = useState(false);
  const [detailSection, setDetailSection] = useState<TaskDetailSection>('basic');
  const [taskConflict, setTaskConflict] = useState<ReturnType<typeof readProjectManagementTaskConflict>>(null);
  const [batchOpen, setBatchOpen] = useState(false);
  const [batchResult, setBatchResult] = useState<ProjectManagementTaskBatchExecutionResult | null>(null);
  const [optimisticBoardStatuses, setOptimisticBoardStatuses] = useState<Record<string, string>>({});
  const [optimisticBoardProgress, setOptimisticBoardProgress] = useState<Record<string, number>>({});
  const [optimisticBoardRows, setOptimisticBoardRows] = useState<Record<string, ProjectManagementTaskListItem>>({});
  const [boardRowsById, setBoardRowsById] = useState<Record<string, ProjectManagementTaskListItem>>({});
  const [selectedTaskIds, setSelectedTaskIds] = useState<Set<string>>(() => new Set());
  const [openingConversationScope, setOpeningConversationScope] = useState<'project' | 'task' | null>(null);
  const [form, setForm] = useState<ProjectManagementTaskUpsertRequest>(emptyForm);
  const [commentForm, setCommentForm] = useState<ProjectManagementTaskCommentUpsertRequest>({ markdown: '' });
  const [commentPageIndex, setCommentPageIndex] = useState(1);
  const [taskActivityQuery, setTaskActivityQuery] = useState<ProjectManagementActivityQuery>({ pageIndex: 1, pageSize: 20 });
  const [editingComment, setEditingComment] = useState<ProjectManagementTaskComment | null>(null);
  const [commentEditForm, setCommentEditForm] = useState<ProjectManagementTaskCommentUpsertRequest>({ markdown: '' });
  const [attachmentUploadFile, setAttachmentUploadFile] = useState<File | null>(null);
  const [attachmentUploadProgress, setAttachmentUploadProgress] = useState(0);
  const [attachmentUploadError, setAttachmentUploadError] = useState<string | undefined>();
  const attachmentAbortController = useRef<AbortController | null>(null);
  const [attachmentDownloadingId, setAttachmentDownloadingId] = useState<string | undefined>();
  const [attachmentDownloadError, setAttachmentDownloadError] = useState<string | undefined>();
  const [attachmentDownloadErrorId, setAttachmentDownloadErrorId] = useState<string | undefined>();
  const [attachmentPreviewState, setAttachmentPreviewState] = useState<{
    attachment: ProjectManagementTaskAttachment | null;
    error?: string;
    loading: boolean;
    previewFile: File | null;
  }>({ attachment: null, loading: false, previewFile: null });
  const attachmentPreviewAbortController = useRef<AbortController | null>(null);
  const [labelFilter, setLabelFilter] = useState<ProjectManagementTaskLabelFilter>({ labelIds: [], matchMode: 'Any' });
  const query = useMemo(() => ({
    ...taskWorkspaceStateToQuery(projectId, state),
    ...(state.viewKey === 'calendar' ? { pageIndex: 1, pageSize: 200 } : {}),
    labelFilter: labelFilter.labelIds.length > 0 ? labelFilter : undefined,
  }), [labelFilter, projectId, state]);
  const activeView = taskViewMeta[state.viewKey];
  const clearOptimisticBoardState = useCallback(() => {
    setOptimisticBoardRows({});
    setOptimisticBoardStatuses({});
    setOptimisticBoardProgress({});
  }, []);

  useProjectManagementRealtimeConnection('/hubs/system-notification', scope, projectId, scope.isAvailable && Boolean(projectId), clearOptimisticBoardState);

  const tasksQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryFn: ({ signal }) => getProjectManagementTasks(query, signal),
    queryKey: queryKeys.projectManagement.tasks(scope, query),
  });
  const selectedTaskId = state.selectedTaskId ?? '';
  const taskDetailQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId) && !creating,
    queryFn: ({ signal }) => getProjectManagementTask(selectedTaskId, signal),
    queryKey: queryKeys.projectManagement.task(scope, projectId, selectedTaskId),
  });
  const taskActivitiesQuery = useQuery({
    enabled: scope.isAvailable && canViewTaskActivities && Boolean(projectId && selectedTaskId) && detailSection === 'activity',
    queryFn: ({ signal }) => getProjectManagementTaskActivities(selectedTaskId, taskActivityQuery, signal),
    queryKey: ['project-management-task-activities', scope.tenantId, scope.appCode, selectedTaskId, taskActivityQuery],
  });
  const projectConversationQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryFn: ({ signal }) => getProjectManagementImConversation(projectId, undefined, signal),
    queryKey: queryKeys.projectManagement.imConversation(scope, projectId),
  });
  const taskConversationQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId),
    queryFn: ({ signal }) => getProjectManagementImConversation(projectId, selectedTaskId, signal),
    queryKey: queryKeys.projectManagement.imConversation(scope, projectId, selectedTaskId),
  });
  const commentsQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId) && detailSection === 'comments',
    queryFn: ({ signal }) => getProjectManagementTaskComments(selectedTaskId, { pageIndex: commentPageIndex, pageSize: 50, sort: 'timeline' }, signal),
    queryKey: queryKeys.projectManagement.taskComments(scope, projectId, selectedTaskId),
  });
  useEffect(() => {
    attachmentAbortController.current?.abort();
    attachmentAbortController.current = null;
    setAttachmentUploadFile(null);
    setAttachmentUploadProgress(0);
    setAttachmentUploadError(undefined);
    attachmentPreviewAbortController.current?.abort();
    attachmentPreviewAbortController.current = null;
    setAttachmentDownloadingId(undefined);
    setAttachmentDownloadError(undefined);
    setAttachmentDownloadErrorId(undefined);
    setAttachmentPreviewState({ attachment: null, loading: false, previewFile: null });
    setCommentPageIndex(1);
    setTaskActivityQuery({ pageIndex: 1, pageSize: 20 });
    setEditingComment(null);
    setCommentEditForm({ markdown: '' });
    setDetailSection('basic');
    setTaskConflict(null);
  }, [selectedTaskId]);
  const attachmentsQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId) && detailSection === 'attachments',
    queryFn: ({ signal }) => getProjectManagementTaskAttachments(selectedTaskId, signal),
    queryKey: queryKeys.projectManagement.taskAttachments(scope, projectId, selectedTaskId),
  });
  const remindersQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId) && detailSection === 'reminders',
    queryFn: ({ signal }) => getProjectManagementTaskReminders(selectedTaskId, signal),
    queryKey: queryKeys.projectManagement.taskReminders(scope, projectId, selectedTaskId),
  });
  const membersQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId) && detailSection === 'reminders',
    queryFn: ({ signal }) => getProjectManagementMembers(projectId, signal),
    queryKey: queryKeys.projectManagement.members(scope, projectId),
  });
  const savedViewsQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryFn: ({ signal }) => getProjectManagementSavedViews(projectId, signal),
    queryKey: queryKeys.projectManagement.savedViews(scope, projectId),
  });
  const milestonesQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryFn: ({ signal }) => getProjectManagementMilestones(projectId, signal),
    queryKey: queryKeys.projectManagement.milestones(scope, projectId),
  });
  const labelsQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryFn: ({ signal }) => getProjectManagementLabels(projectId, signal),
    queryKey: queryKeys.projectManagement.labels(scope, projectId),
  });
  const memberCandidatesQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId),
    queryFn: ({ signal }) => getProjectManagementMemberCandidates({ pageIndex: 1, pageSize: 200 }, signal),
    queryKey: queryKeys.projectManagement.memberCandidates(scope, { pageIndex: 1, pageSize: 200 }),
  });
  const childTasksQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId) && detailSection === 'children',
    queryFn: ({ signal }) => getProjectManagementTasks({ projectId, pageIndex: 1, pageSize: 100, parentTaskId: selectedTaskId, viewKey: 'list', sortBy: 'tree', sortDirection: 'asc' }, signal),
    queryKey: queryKeys.projectManagement.tasks(scope, { projectId, pageIndex: 1, pageSize: 100, parentTaskId: selectedTaskId, viewKey: 'list', sortBy: 'tree', sortDirection: 'asc' }),
  });
  const dependenciesQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId) && (state.viewKey === 'calendar' || state.viewKey === 'gantt' || Boolean(selectedTaskId) && detailSection === 'dependencies'),
    queryFn: ({ signal }) => getProjectManagementTaskDependencies(projectId, signal),
    queryKey: ['project-management-task-dependencies', scope.tenantId, scope.appCode, projectId],
  });
  const serverRows = useMemo(() => tasksQuery.data?.data?.items ?? [], [tasksQuery.data?.data?.items]);
  const rows = useMemo(() => serverRows.map((task) => optimisticBoardStatuses[task.id] || optimisticBoardProgress[task.id] !== undefined
    ? { ...task, progressPercent: optimisticBoardProgress[task.id] ?? task.progressPercent, status: optimisticBoardStatuses[task.id] ?? task.status }
    : task), [optimisticBoardProgress, optimisticBoardStatuses, serverRows]);
  const boardRows = useMemo(() => Object.values(boardRowsById).map((task) => optimisticBoardStatuses[task.id] || optimisticBoardProgress[task.id] !== undefined
    ? { ...task, progressPercent: optimisticBoardProgress[task.id] ?? task.progressPercent, status: optimisticBoardStatuses[task.id] ?? task.status }
    : task), [boardRowsById, optimisticBoardProgress, optimisticBoardStatuses]);
  const handleBoardRowsLoaded = useCallback((loadedRows: ProjectManagementTaskListItem[]) => {
    setBoardRowsById(Object.fromEntries(loadedRows.map((task) => [task.id, task])));
  }, []);
  const selectableRows = useMemo(() => {
    const byId = new Map<string, ProjectManagementTaskListItem>(rows.map((task) => [task.id, task]));
    if (state.viewKey === 'board') boardRows.forEach((task) => byId.set(task.id, task));
    return [...byId.values()];
  }, [boardRows, rows, state.viewKey]);
  const participantLabels = useMemo(() => Object.fromEntries((memberCandidatesQuery.data?.data?.items ?? []).map((candidate) => [candidate.userId, candidate.displayName || candidate.userName])), [memberCandidatesQuery.data?.data?.items]);
  const milestoneLabels = useMemo(() => Object.fromEntries((milestonesQuery.data?.data ?? []).map((milestone) => [milestone.id, milestone.milestoneName])), [milestonesQuery.data?.data]);
  const selectedListTask = rows.find((task) => task.id === selectedTaskId);
  const selectedTask = taskDetailQuery.data?.data;
  const selectedTasks = useMemo(() => selectableRows.filter((task) => selectedTaskIds.has(task.id)), [selectableRows, selectedTaskIds]);
  const childTasks = childTasksQuery.data?.data?.items ?? [];
  const dependencyLabels = useMemo(() => Object.fromEntries(rows.map((task) => [task.id, `${task.taskCode} · ${task.title}`])), [rows]);
  const taskDetailErrorMessage = taskDetailQuery.error
    ? isHttpError(taskDetailQuery.error) && taskDetailQuery.error.status === 403
      ? '没有查看该任务详情的权限。'
      : isHttpError(taskDetailQuery.error) && taskDetailQuery.error.status === 404
        ? '任务不存在、已删除或已归档。'
        : '任务详情加载失败。'
    : undefined;

  useEffect(() => {
    if (selectedTaskId && tasksQuery.isSuccess && !selectedListTask) {
      setState({ selectedTaskId: undefined }, { replace: true });
      setCreating(false);
    }
  }, [selectedListTask, selectedTaskId, setState, tasksQuery.isSuccess]);

  useEffect(() => {
    const availableIds = new Set(selectableRows.map((task) => task.id));
    setSelectedTaskIds((current) => {
      const next = new Set([...current].filter((taskId) => availableIds.has(taskId)));
      return next.size === current.size ? current : next;
    });
  }, [selectableRows]);

  useEffect(() => {
    if (state.viewKey !== 'board') setBoardRowsById({});
  }, [state.viewKey]);

  useEffect(() => {
    if (!selectedTask || creating) return;
    setForm(taskDetailToForm(selectedTask));
  }, [creating, selectedTask]);

  const invalidateProjectTaskViews = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.tasksProject(scope, projectId) });
    await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.overview(scope, { projectId }) });
  };

  const openProjectManagementConversation = async (scopeName: 'project' | 'task') => {
    if (openingConversationScope) return;
    const taskId = scopeName === 'task' ? selectedTaskId : undefined;
    const queryResult = scopeName === 'task' ? taskConversationQuery.data?.data : projectConversationQuery.data?.data;
    setOpeningConversationScope(scopeName);
    try {
      const conversation = queryResult?.status === 'Active'
        ? queryResult
        : (await ensureProjectManagementImConversation(projectId, { taskId })).data;
      if (!conversation?.conversationId) throw new Error('关联会话创建后没有返回会话标识');
      await openImConversation(conversation.conversationId);
      await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.imConversation(scope, projectId, taskId) });
    } catch (error) {
      message.error(getErrorMessage(error, '关联协作会话打开失败'));
    } finally {
      setOpeningConversationScope(null);
    }
  };

  const saveMutation = useApiMutation({
    mutationFn: ({ overwriteVersionNo }: { overwriteVersionNo?: number } = {}) => {
      const request = overwriteVersionNo === undefined ? form : { ...form, versionNo: overwriteVersionNo };
      return selectedTask && !creating ? updateProjectManagementTask(selectedTask.id, request) : createProjectManagementTask(projectId, request);
    },
    onError: (error) => {
      const conflict = readProjectManagementTaskConflict(error);
      if (conflict) {
        setTaskConflict(conflict);
        message.error('任务保存发生并发冲突，请比较服务器值和本地值后处理');
        return;
      }
      message.error(getErrorMessage(error, selectedTask && !creating ? '任务保存失败' : '任务创建失败'));
    },
    onSuccess: async (result) => {
      const task = result.data;
      message.success(creating ? '任务已创建' : '任务已更新');
      setTaskConflict(null);
      setCreating(false);
      setForm(taskDetailToForm(task));
      setState({ selectedTaskId: task?.id }, { replace: true });
      await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.task(scope, projectId, task.id) });
      await invalidateProjectTaskViews();
    },
  });
  const [quickActionTaskId, setQuickActionTaskId] = useState<string>();
  const quickActionMutation = useApiMutation({
    mutationFn: async ({ action, task }: { action: QuickTaskAction; task: ProjectManagementTaskListItem }) => {
      if (action === 'delete') return deleteProjectManagementTask(task.id, task.versionNo);
      const detail = (await getProjectManagementTask(task.id)).data;
      return updateProjectManagementTask(task.id, toTaskUpsertRequest(detail, 'Done'));
    },
    onError: (error) => message.error(getErrorMessage(error, '任务快速操作失败')),
    onSuccess: async (_result, variables) => {
      message.success(variables.action === 'delete' ? '任务已删除' : '任务已完成');
      if (variables.task.id === selectedTaskId) {
        setCreating(false);
        setState({ selectedTaskId: undefined }, { replace: true });
      }
      setQuickActionTaskId(undefined);
      await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.all(scope) });
      await invalidateProjectTaskViews();
    },
    onSettled: () => setQuickActionTaskId(undefined),
  });
  const scheduleMutation = useApiMutation({
    mutationFn: async ({ dueDate, startDate, task }: { dueDate: string | undefined; startDate: string | undefined; task: ProjectManagementTaskListItem }) => {
      const current = (await getProjectManagementTask(task.id)).data;
      if (!current) throw new Error('任务不存在、已删除或无权调整日期');
      return updateProjectManagementTask(task.id, { ...taskDetailToForm(current), dueDate, startDate, versionNo: current.versionNo });
    },
    onError: async (error) => {
      message.error(getErrorMessage(error, '任务调期失败，已刷新服务器最新任务'));
      await invalidateProjectTaskViews();
    },
    onSuccess: async (result) => {
      message.success(`任务“${result.data?.title ?? ''}”计划日期已更新`);
      await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.task(scope, projectId, result.data?.id ?? '') });
      await invalidateProjectTaskViews();
    },
  });
  const commentMutation = useApiMutation({
    mutationFn: () => createProjectManagementTaskComment(selectedTaskId, commentForm),
    onError: (error) => message.error(getErrorMessage(error, '评论发布失败')),
    onSuccess: async () => {
      message.success('评论已发布');
      setCommentForm({ markdown: '' });
      await commentsQuery.refetch();
    },
  });
  const commentUpdateMutation = useApiMutation({
    mutationFn: ({ comment, request }: { comment: ProjectManagementTaskComment; request: ProjectManagementTaskCommentUpsertRequest }) => updateProjectManagementTaskComment(selectedTaskId, comment.id, { ...request, versionNo: comment.versionNo }),
    onError: (error) => message.error(getErrorMessage(error, '评论修改失败')),
    onSuccess: async () => {
      message.success('评论已修改');
      setEditingComment(null);
      setCommentEditForm({ markdown: '' });
      await commentsQuery.refetch();
    },
  });
  const commentDeleteMutation = useApiMutation({
    mutationFn: (comment: ProjectManagementTaskComment) => deleteProjectManagementTaskComment(selectedTaskId, comment.id, comment.versionNo),
    onError: (error) => message.error(getErrorMessage(error, '评论删除失败')),
    onSuccess: async () => {
      message.success('评论已删除');
      await commentsQuery.refetch();
    },
  });
  const attachmentMutation = useApiMutation({
    mutationFn: (file: File) => {
      const controller = new AbortController();
      attachmentAbortController.current = controller;
      return uploadProjectManagementTaskAttachmentWithProgress(selectedTaskId, file, {
        onProgress: (loaded, total) => setAttachmentUploadProgress(total > 0 ? Math.round((loaded / total) * 100) : 0),
        signal: controller.signal,
      });
    },
    onError: (error) => {
      setAttachmentUploadError(error.name === 'AbortError' ? '上传已取消' : getErrorMessage(error, '附件上传失败'));
      if (error.name !== 'AbortError') message.error(getErrorMessage(error, '附件上传失败'));
    },
    onSuccess: async () => {
      message.success('附件已上传');
      setAttachmentUploadError(undefined);
      setAttachmentUploadProgress(100);
      await attachmentsQuery.refetch();
    },
    onSettled: () => { attachmentAbortController.current = null; },
  });
  const downloadAttachment = async (attachment: ProjectManagementTaskAttachment) => {
    setAttachmentDownloadingId(attachment.id);
    setAttachmentDownloadError(undefined);
    setAttachmentDownloadErrorId(undefined);
    try {
      const result = await downloadProjectManagementTaskAttachment(attachment);
      saveBlob(result.blob, result.fileName || attachment.fileName);
    } catch (error) {
      setAttachmentDownloadErrorId(attachment.id);
      setAttachmentDownloadError(getErrorMessage(error, '附件下载失败，链接可能已失效'));
    } finally {
      setAttachmentDownloadingId(undefined);
    }
  };
  const previewAttachment = async (attachment: ProjectManagementTaskAttachment) => {
    attachmentPreviewAbortController.current?.abort();
    const controller = new AbortController();
    attachmentPreviewAbortController.current = controller;
    setAttachmentPreviewState({ attachment, loading: true, previewFile: null });
    try {
      const result = await previewProjectManagementTaskAttachment(attachment, controller.signal);
      const previewFile = new File([result.blob], result.fileName || attachment.fileName, { type: result.blob.type || attachment.contentType });
      setAttachmentPreviewState({ attachment, loading: false, previewFile });
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') return;
      setAttachmentPreviewState({ attachment, error: getErrorMessage(error, '附件预览失败，链接可能已失效'), loading: false, previewFile: null });
    } finally {
      if (attachmentPreviewAbortController.current === controller) attachmentPreviewAbortController.current = null;
    }
  };
  const closeAttachmentPreview = () => {
    attachmentPreviewAbortController.current?.abort();
    attachmentPreviewAbortController.current = null;
    setAttachmentPreviewState({ attachment: null, loading: false, previewFile: null });
  };
  const reminderCreateMutation = useApiMutation({
    mutationFn: (request: ProjectManagementTaskReminderCreateRequest) => createProjectManagementTaskReminders(selectedTaskId, request),
    onError: (error) => message.error(getErrorMessage(error, '提醒创建失败')),
    onSuccess: async () => {
      message.success('任务提醒已创建');
      await remindersQuery.refetch();
    },
  });
  const reminderCancelMutation = useApiMutation({
    mutationFn: (reminder: ProjectManagementTaskReminder) => cancelProjectManagementTaskReminder(selectedTaskId, reminder.id, reminder.versionNo),
    onError: (error) => message.error(getErrorMessage(error, '提醒取消失败')),
    onSuccess: async () => {
      message.success('任务提醒已取消');
      await remindersQuery.refetch();
    },
  });
  const reminderDeleteMutation = useApiMutation({
    mutationFn: (reminder: ProjectManagementTaskReminder) => deleteProjectManagementTaskReminder(selectedTaskId, reminder.id, reminder.versionNo),
    onError: (error) => message.error(getErrorMessage(error, '提醒记录删除失败')),
    onSuccess: async () => {
      message.success('提醒记录已删除');
      await remindersQuery.refetch();
    },
  });
  const batchMutation = useApiMutation({
    mutationFn: (request: ProjectManagementTaskBatchUpdateRequest) => executeProjectManagementTasksBatch(request),
    onError: (error) => message.error(getErrorMessage(error, '批量任务更新失败')),
    onSuccess: async (result) => {
      if (result.data) {
        setBatchResult(result.data);
        message.success(`批量操作完成：成功 ${result.data.succeededCount} 项，失败 ${result.data.failedCount + result.data.conflictCount} 项`);
      }
      setBatchOpen(false);
      setSelectedTaskIds(new Set());
      await invalidateProjectTaskViews();
    },
  });
  const exportMutation = useApiMutation({
    mutationFn: () => exportProjectManagementTasksCsv(query),
    onError: (error) => message.error(getErrorMessage(error, '任务导出失败')),
    onSuccess: (result) => saveBlob(result.blob, result.fileName || 'project-management-tasks.csv'),
  });
  const moveMutation = useApiMutation({
    mutationFn: ({ taskId, request }: { taskId: string; request: ReturnType<typeof createTaskMoveRequest> }) => {
      if (!request) throw new Error('无效的任务移动目标');
      return moveProjectManagementTask(taskId, request);
    },
    onError: async (error) => {
      message.error(getErrorMessage(error, '任务移动失败，已刷新最新排序'));
      await invalidateProjectTaskViews();
    },
    onSuccess: async () => {
      message.success('任务位置已更新');
      await invalidateProjectTaskViews();
    },
  });
  const groupMutation = useApiMutation({
    mutationFn: async ({ task, target }: { task: ProjectManagementTaskListItem; target: TaskGroupDropTarget }) => {
      const original = (await getProjectManagementTask(task.id)).data;
      if (!original) throw new Error('任务不存在或已被删除');
      const originalLabelIds = (await getProjectManagementTaskLabels(task.id)).data?.map((label) => label.labelId) ?? [];
      let groupChanged = false;
      let current = original;
      try {
        if (target.groupBy === 'label') {
          const desiredLabelIds = target.groupValue === 'unassigned'
            ? []
            : [...new Set([...originalLabelIds, target.groupValue])];
          if (!sameStringSet(originalLabelIds, desiredLabelIds)) {
            await setProjectManagementTaskLabels(task.id, { labelIds: desiredLabelIds, versionNo: current.versionNo });
            groupChanged = true;
            current = (await getProjectManagementTask(task.id)).data ?? current;
          }
        } else {
          const request = taskDetailToForm(original);
          const requestedValue = target.groupValue === 'unassigned' || target.groupValue === 'root' ? undefined : target.groupValue;
          if (target.groupBy === 'assignee') request.assigneeUserId = requestedValue;
          if (target.groupBy === 'milestone') request.milestoneId = requestedValue;
          if (target.groupBy === 'parent') request.parentTaskId = requestedValue;
          const currentValue = target.groupBy === 'assignee'
            ? original.assigneeUserId
            : target.groupBy === 'milestone'
              ? original.milestoneId
              : original.parentTaskId;
          if ((currentValue ?? undefined) !== requestedValue) {
            const result = await updateProjectManagementTask(task.id, request);
            if (!result.data) throw new Error('分组字段更新没有返回最新任务');
            groupChanged = true;
            current = result.data;
          }
        }

        if (target.status && target.status !== current.status) {
          const result = await changeProjectManagementTaskStatus(task.id, { status: target.status, versionNo: current.versionNo });
          if (!result.data) throw new Error('状态更新没有返回最新任务');
          groupChanged = true;
          current = result.data;
        }
        return current;
      } catch (error) {
        if (!groupChanged) throw error;
        try {
          let latest = (await getProjectManagementTask(task.id)).data;
          if (!latest) throw new Error('回滚时任务已不存在');
          if (target.groupBy === 'label') {
            const latestLabelIds = (await getProjectManagementTaskLabels(task.id)).data?.map((label) => label.labelId) ?? [];
            if (!sameStringSet(latestLabelIds, originalLabelIds)) {
              await setProjectManagementTaskLabels(task.id, { labelIds: originalLabelIds, versionNo: latest.versionNo });
              latest = (await getProjectManagementTask(task.id)).data ?? latest;
            }
            if (!target.status || latest.status === original.status) throw error;
          }
          const rollback = await updateProjectManagementTask(task.id, taskDetailToForm(original, latest.versionNo));
          if (!rollback.data) throw new Error('回滚没有返回最新任务');
        } catch (rollbackError) {
          throw new Error(`${getErrorMessage(error, '分组移动失败')}；自动回滚失败：${getErrorMessage(rollbackError, '请刷新任务确认当前值')}`);
        }
        throw error;
      }
    },
    onError: async (error, variables) => {
      message.error(getErrorMessage(error, `任务“${variables.task.title}”分组移动失败，已回滚`));
      await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.task(scope, projectId, variables.task.id) });
      await invalidateProjectTaskViews();
    },
    onSuccess: async (_result, variables) => {
      message.success(`任务“${variables.task.title}”已移动到分组`);
      await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.task(scope, projectId, variables.task.id) });
      await invalidateProjectTaskViews();
    },
  });
  const boardStatusMutation = useApiMutation({
    mutationFn: ({ task, status }: { previousProgress: number; previousStatus: string; snapshot: ReturnType<typeof applyOptimisticBoardMove>['snapshot']; task: ProjectManagementTaskListItem; status: string }) => {
      return changeProjectManagementTaskStatus(task.id, { status, versionNo: task.versionNo });
    },
    onError: async (error, variables) => {
      setOptimisticBoardRows((current) => rollbackOptimisticBoardMove(current, variables.snapshot));
      setOptimisticBoardStatuses((current) => rollbackBoardStatus(current, variables.task.id, variables.previousStatus));
      setOptimisticBoardProgress((current) => ({ ...current, [variables.task.id]: variables.previousProgress }));
      message.error(getErrorMessage(error, '看板状态更新失败，已回滚显示'));
      await invalidateProjectTaskViews();
      setOptimisticBoardRows((current) => clearOptimisticBoardMove(current, variables.task.id));
      setOptimisticBoardStatuses((current) => { const next = { ...current }; delete next[variables.task.id]; return next; });
      setOptimisticBoardProgress((current) => { const next = { ...current }; delete next[variables.task.id]; return next; });
    },
    onSuccess: async (_result, variables) => {
      await invalidateProjectTaskViews();
      setOptimisticBoardRows((current) => clearOptimisticBoardMove(current, variables.task.id));
      setOptimisticBoardStatuses((current) => {
        const next = { ...current };
        delete next[variables.task.id];
        return next;
      });
      setOptimisticBoardProgress((current) => {
        const next = { ...current };
        delete next[variables.task.id];
        return next;
      });
      message.success('任务状态已更新');
    },
  });
  const savedViewMutation = useApiMutation({
    mutationFn: (viewName: string) => createProjectManagementSavedView(projectId, {
      isDefault: false,
      queryJson: JSON.stringify(taskWorkspaceStateToSavedView(state)),
      viewKey: state.viewKey,
      viewName,
    }),
    onError: (error) => message.error(getErrorMessage(error, '视图保存失败')),
    onSuccess: async () => {
      message.success('视图已保存');
      await savedViewsQuery.refetch();
    },
  });
  const updateSavedViewMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: Parameters<typeof updateProjectManagementSavedView>[2] }) => updateProjectManagementSavedView(projectId, id, request),
    onError: (error) => message.error(getErrorMessage(error, '保存视图更新失败')),
    onSuccess: async () => { message.success('保存视图已更新'); await savedViewsQuery.refetch(); },
  });
  const copySavedViewMutation = useApiMutation({
    mutationFn: ({ queryJson, viewKey, viewName }: { queryJson: string; viewKey: ProjectManagementTaskView; viewName: string }) => createProjectManagementSavedView(projectId, { isDefault: false, isShared: false, queryJson, viewKey, viewName }),
    onError: (error) => message.error(getErrorMessage(error, '保存视图复制失败')),
    onSuccess: async () => { message.success('保存视图已复制'); await savedViewsQuery.refetch(); },
  });
  const deleteSavedViewMutation = useApiMutation({
    mutationFn: ({ id, versionNo }: { id: string; versionNo: number }) => deleteProjectManagementSavedView(projectId, id, versionNo),
    onError: (error) => message.error(getErrorMessage(error, '保存视图删除失败')),
    onSuccess: async () => { message.success('保存视图已删除'); await savedViewsQuery.refetch(); },
  });
  const closeTaskDetail = useCallback(() => {
    setCreating(false);
    setTaskConflict(null);
    setState({ selectedTaskId: undefined });
  }, [setState]);
  const reloadTaskDetail = useCallback(async () => {
    const result = await taskDetailQuery.refetch();
    if (result.data?.data) setForm(taskDetailToForm(result.data.data));
    setTaskConflict(null);
  }, [taskDetailQuery, setForm]);
  const detailSectionContent = detailSection === 'children'
    ? <TaskDetailChildrenSection error={childTasksQuery.isError} loading={childTasksQuery.isLoading} onRetry={() => void childTasksQuery.refetch()} onSelect={(taskId) => { setState({ selectedTaskId: taskId }); setDetailSection('basic'); }} tasks={childTasks} />
    : detailSection === 'dependencies'
      ? <TaskDetailDependenciesSection dependencies={(dependenciesQuery.data?.data ?? []).filter((item) => item.predecessorTaskId === selectedTaskId || item.successorTaskId === selectedTaskId)} error={dependenciesQuery.isError} labels={dependencyLabels} loading={dependenciesQuery.isLoading} onRetry={() => void dependenciesQuery.refetch()} />
      : detailSection === 'activity'
        ? <TaskActivityTimeline canView={canViewTaskActivities} isError={taskActivitiesQuery.isError} isLoading={taskActivitiesQuery.isLoading} onQueryChange={setTaskActivityQuery} page={taskActivitiesQuery.data?.data} query={taskActivityQuery} />
        : undefined;

  if (!scope.isAvailable) return <PageError description="当前会话没有可用的租户和应用工作区" />;
  if (tasksQuery.isLoading) return <PageLoading />;
  if (tasksQuery.isError) {
    if (isHttpError(tasksQuery.error) && tasksQuery.error.status === 403) return <Page403 />;
    return <PageError action={<button type="button" onClick={() => void tasksQuery.refetch()}>重试</button>} description="任务列表加载失败" />;
  }

  return (
    <ResponsivePage
      className="pm-page"
      description={activeView.description}
      eyebrow="ProjectManagement / Tasks"
      title={`项目任务 · ${activeView.label}`}
      toolbar={
        <TaskWorkspaceToolbar
          onExport={() => exportMutation.mutate()}
          onOpenBatch={() => setBatchOpen(true)}
          onSelectAll={() => setSelectedTaskIds((current) => {
            const pageIds = selectableRows.map((task) => task.id);
            const allSelected = pageIds.length > 0 && pageIds.every((taskId) => current.has(taskId));
            const next = new Set(current);
            pageIds.forEach((taskId) => allSelected ? next.delete(taskId) : next.add(taskId));
            return next;
          })}
          onCreateTask={() => {
            setCreating(true);
            setForm(emptyForm);
            setState({ selectedTaskId: undefined });
          }}
          onSaveView={(viewName) => savedViewMutation.mutate(viewName)}
          onSelectSavedView={(view) => {
            try {
              const saved = JSON.parse(view.queryJson) as Partial<typeof state>;
              setState({ ...saved, selectedTaskId: saved.selectedTaskId });
            } catch {
              message.error('保存视图内容无效');
            }
          }}
          onStateChange={(next) => setState(next)}
          projectConversation={
            <TaskWorkspaceImConversationPanel
              conversation={projectConversationQuery.data?.data}
              error={projectConversationQuery.isError}
              loading={projectConversationQuery.isLoading}
              onOpen={() => void openProjectManagementConversation('project')}
              opening={openingConversationScope === 'project'}
              scope="project"
            />
          }
          projectId={projectId}
          savedViews={savedViewsQuery.data?.data ?? []}
          savingView={savedViewMutation.isPending}
          selectedCount={selectedTasks.length}
          state={state}
          total={tasksQuery.data?.data?.total ?? 0}
        />
      }
    >
      <section className="pm-workspace-summary" aria-label="当前任务工作区状态">
        <span><strong>{activeView.label}</strong> · 当前筛选共 {tasksQuery.data?.data?.total ?? 0} 个任务</span>
        <span>{selectedTasks.length ? `已选择 ${selectedTasks.length} 个任务，可批量更新。` : '选择任务可打开详情、协作、附件与提醒。'}</span>
      </section>
      {activeView.note ? <p className="pm-prototype-note" role="status">{activeView.note}</p> : null}
      <TaskDetailDrawer
        activeSection={detailSection}
        conflict={taskConflict}
        conflictPending={saveMutation.isPending}
        creating={creating}
        errorMessage={taskDetailErrorMessage}
        loading={taskDetailQuery.isLoading}
        onClose={closeTaskDetail}
        onKeepLocal={() => setTaskConflict(null)}
        onOverwrite={() => taskConflict && saveMutation.mutate({ overwriteVersionNo: taskConflict.serverValues.versionNo })}
        onReload={() => void reloadTaskDetail()}
        onSectionChange={setDetailSection}
        open={Boolean(selectedTaskId || creating)}
        sectionContent={detailSectionContent}
        selectedTask={selectedTask}
      >
        <TaskWorkspaceSelectionPanel
          activeSection={detailSection}
          attachments={attachmentsQuery.data?.data ?? []}
          attachmentsError={attachmentsQuery.isError}
          attachmentDownloadError={attachmentDownloadError}
          attachmentDownloadErrorId={attachmentDownloadErrorId}
          attachmentDownloadingId={attachmentDownloadingId}
          attachmentPreviewError={attachmentPreviewState.error}
          attachmentPreviewErrorId={attachmentPreviewState.attachment?.id}
          attachmentPreviewingId={attachmentPreviewState.loading ? attachmentPreviewState.attachment?.id : undefined}
          attachmentUploadError={attachmentUploadError}
          attachmentUploadProgress={attachmentUploadProgress}
          attachmentUploading={attachmentMutation.isPending}
          comments={commentsQuery.data?.data?.items ?? []}
          commentsTotal={commentsQuery.data?.data?.total ?? 0}
          commentsError={commentsQuery.isError}
          commentForm={commentForm}
          commentSubmitting={commentMutation.isPending}
          commentEditing={editingComment}
          commentEditForm={commentEditForm}
          commentEditSubmitting={commentUpdateMutation.isPending}
          creating={creating}
          form={form}
          onCancel={() => {
            closeTaskDetail();
            setForm(emptyForm);
          }}
          onCommentChange={setCommentForm}
          onCommentSubmit={() => commentMutation.mutate()}
          onCommentEditChange={setCommentEditForm}
          onCommentEditStart={(comment) => {
            setEditingComment(comment);
            setCommentEditForm({ markdown: comment.markdown, versionNo: comment.versionNo });
          }}
          onCommentEditCancel={() => {
            setEditingComment(null);
            setCommentEditForm({ markdown: '' });
          }}
          onCommentEditSubmit={() => editingComment && commentUpdateMutation.mutate({ comment: editingComment, request: commentEditForm })}
          onCommentDelete={(comment) => confirm({ title: '删除任务评论', content: '删除后保留审计记录，但评论正文不再显示。', confirmText: '删除评论', onConfirm: () => commentDeleteMutation.mutate(comment) })}
          commentPageIndex={commentPageIndex}
          commentPageSize={50}
          onCommentPageChange={setCommentPageIndex}
          onCreateReminder={(request) => reminderCreateMutation.mutate(request)}
          onCancelReminder={(reminder) => confirm({ title: '取消任务提醒', content: '取消后不会再向接收人投递该提醒。', confirmText: '取消提醒', onConfirm: () => reminderCancelMutation.mutate(reminder) })}
          onDeleteReminder={(reminder) => confirm({ title: '删除提醒记录', content: '删除后不再保留该提醒的历史记录。', confirmText: '删除记录', onConfirm: () => reminderDeleteMutation.mutate(reminder) })}
          onFormChange={setForm}
          onSubmit={() => saveMutation.mutate({})}
          onCancelUpload={() => attachmentAbortController.current?.abort()}
          onRetryUpload={() => attachmentUploadFile && attachmentMutation.mutate(attachmentUploadFile)}
          onRetryAttachments={() => void attachmentsQuery.refetch()}
          onDownloadAttachment={(attachment) => void downloadAttachment(attachment)}
          onRetryDownloadAttachment={(attachment) => void downloadAttachment(attachment)}
          onPreviewAttachment={(attachment) => void previewAttachment(attachment)}
          onRetryPreviewAttachment={(attachment) => void previewAttachment(attachment)}
          onUpload={(file) => {
            setAttachmentUploadFile(file);
            setAttachmentUploadProgress(0);
            setAttachmentUploadError(undefined);
            attachmentMutation.mutate(file);
          }}
          reminderCreating={reminderCreateMutation.isPending || reminderCancelMutation.isPending || reminderDeleteMutation.isPending}
          reminderMembers={membersQuery.data?.data ?? []}
          reminders={remindersQuery.data?.data ?? []}
          remindersError={remindersQuery.isError}
          remindersLoading={remindersQuery.isLoading}
          saving={saveMutation.isPending}
          selectedTask={selectedTask}
        />
        <ProjectManagementTaskAttachmentPreviewDialog
          attachment={attachmentPreviewState.attachment}
          error={attachmentPreviewState.error}
          loading={attachmentPreviewState.loading}
          open={Boolean(attachmentPreviewState.attachment)}
          previewFile={attachmentPreviewState.previewFile}
          onClose={closeAttachmentPreview}
        />
      </TaskDetailDrawer>
      {selectedTask && !creating ? (
        <TaskWorkspaceImConversationPanel
          conversation={taskConversationQuery.data?.data}
          error={taskConversationQuery.isError}
          loading={taskConversationQuery.isLoading}
          onOpen={() => void openProjectManagementConversation('task')}
          opening={openingConversationScope === 'task'}
          scope="task"
        />
      ) : null}
      <TaskWorkspaceLabelManager
        filter={labelFilter}
        labels={labelsQuery.data?.data ?? []}
        onFilterChange={(next) => {
          setLabelFilter(next);
          setState({ pageIndex: 1 }, { replace: true });
        }}
        onChanged={async () => { await queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.labels(scope, projectId) }); }}
        projectId={projectId}
      />
      <SavedViewManager
        onCopy={(view, viewName) => copySavedViewMutation.mutate({ queryJson: view.queryJson, viewKey: view.viewKey, viewName })}
        onDelete={(view) => deleteSavedViewMutation.mutate({ id: view.id, versionNo: view.versionNo })}
        onUpdate={(view, request) => updateSavedViewMutation.mutate({ id: view.id, request })}
        pending={savedViewMutation.isPending || copySavedViewMutation.isPending || updateSavedViewMutation.isPending || deleteSavedViewMutation.isPending}
        views={savedViewsQuery.data?.data ?? []}
      />
      <TaskWorkspaceBatchCommandPanel
        candidates={memberCandidatesQuery.data?.data?.items ?? []}
        candidatesError={memberCandidatesQuery.isError}
        labels={labelsQuery.data?.data ?? []}
        labelsError={labelsQuery.isError}
        milestones={milestonesQuery.data?.data ?? []}
        milestonesError={milestonesQuery.isError}
        onClose={() => setBatchOpen(false)}
        onSubmit={(request) => batchMutation.mutate(request)}
        open={batchOpen}
        pending={batchMutation.isPending}
        projectId={projectId}
        tasks={selectedTasks}
      />
      {batchResult ? <TaskWorkspaceBatchResultPanel
        onClose={() => setBatchResult(null)}
        onDownload={() => saveBlob(new Blob([taskBatchResultToCsv(batchResult)], { type: 'text/csv;charset=utf-8' }), `task-batch-${batchResult.operationId}.csv`)}
        result={batchResult}
      /> : null}
      <section className="pm-projection" aria-labelledby="task-projection-title">
        <div className="pm-projection-heading"><div><h2 id="task-projection-title">{activeView.label}</h2><p>当前渲染的是服务端返回的任务数据；选择、批量更新和可用的移动命令均继续使用原有请求链路。</p></div><span className="pm-view-note">{rows.length} 条已加载</span></div>
        <TaskWorkspaceProjection
          labelFilter={labelFilter.labelIds.length > 0 ? labelFilter : undefined}
          milestoneLabels={milestoneLabels}
          onAddChildTask={(task) => {
            setCreating(true);
            setForm({ ...emptyForm, milestoneId: task.milestoneId, parentTaskId: task.id });
            setState({ selectedTaskId: undefined });
          }}
          onChangeTaskSchedule={(task, startDate, dueDate) => {
            if (scheduleMutation.isPending) {
              message.error('正在提交上一项任务调期，请稍候');
              return;
            }
            scheduleMutation.mutate({ dueDate, startDate, task });
          }}
          onGanttScheduleSaved={invalidateProjectTaskViews}
          onCreateTaskOnDate={(date) => {
            setCreating(true);
            setForm({ ...emptyForm, dueDate: date, startDate: date });
            setState({ selectedTaskId: undefined });
          }}
          onBoardRowsLoaded={handleBoardRowsLoaded}
          onCompleteTask={(task) => confirm({ title: '完成任务', content: `将任务“${task.title}”标记为已完成，并将进度设为 100%。`, confirmText: '完成任务', onConfirm: () => { setQuickActionTaskId(task.id); quickActionMutation.mutate({ action: 'complete', task }); } })}
          onDeleteTask={(task) => confirm({ title: '删除任务', content: `删除任务“${task.title}”及其子任务？删除后可从回收站恢复。`, confirmText: '删除任务', onConfirm: () => { setQuickActionTaskId(task.id); quickActionMutation.mutate({ action: 'delete', task }); } })}
          participantLabels={participantLabels}
          pendingTaskId={quickActionTaskId}
          projectId={projectId}
          optimisticBoardRows={optimisticBoardRows}
          onMoveTask={(task, target) => {
            if (moveMutation.isPending || groupMutation.isPending || boardStatusMutation.isPending) {
              message.error('正在提交上一项任务移动，请稍候');
              return;
            }
            const request = createTaskMoveRequest(task, target);
            if (request) moveMutation.mutate({ taskId: task.id, request });
          }}
          onMoveTaskGroup={(task, target) => {
            if (moveMutation.isPending || groupMutation.isPending || boardStatusMutation.isPending) {
              message.error('正在提交上一项任务移动，请稍候');
              return;
            }
            groupMutation.mutate({ task, target });
          }}
          onChangeTaskStatus={(task, status) => {
            if (task.status === status) return;
            if (boardStatusMutation.isPending || groupMutation.isPending || moveMutation.isPending) {
              message.error('正在提交上一项状态变更，请稍候');
              return;
            }
            setOptimisticBoardStatuses((current) => ({ ...current, [task.id]: status }));
            const nextProgress = resolveBoardStatusProgress(task, status);
            setOptimisticBoardProgress((current) => ({ ...current, [task.id]: nextProgress }));
            const optimistic = applyOptimisticBoardMove(optimisticBoardRows, task, status, nextProgress);
            setOptimisticBoardRows(optimistic.rows);
            boardStatusMutation.mutate({ previousProgress: task.progressPercent, previousStatus: task.status, snapshot: optimistic.snapshot, task, status });
          }}
          onSelectTask={(taskId) => {
            setCreating(false);
            setState({ selectedTaskId: taskId });
          }}
          onToggleTaskSelection={(taskId) => setSelectedTaskIds((current) => {
            const next = new Set(current);
            if (next.has(taskId)) next.delete(taskId);
            else next.add(taskId);
            return next;
          })}
          rows={rows}
          schedulePending={scheduleMutation.isPending}
          selectedTaskIds={selectedTaskIds}
          state={state}
          dependencies={dependenciesQuery.data?.data ?? []}
          milestones={milestonesQuery.data?.data ?? []}
        />
      </section>
    </ResponsivePage>
  );
}

function sameStringSet(left: readonly string[], right: readonly string[]): boolean {
  if (left.length !== right.length) return false;
  const rightSet = new Set(right);
  return left.every((value) => rightSet.has(value));
}
