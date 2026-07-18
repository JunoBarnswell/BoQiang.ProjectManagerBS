import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useLocation, useParams } from 'react-router-dom';

import {
  createProjectManagementSavedView,
  deleteProjectManagementSavedView,
  ensureProjectManagementImConversation,
  createProjectManagementTask,
  createProjectManagementTaskComment,
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
  getProjectManagementTaskReminders,
  getProjectManagementTasks,
  moveProjectManagementTask,
  updateProjectManagementTask,
  updateProjectManagementTaskComment,
  updateProjectManagementTasksBatch,
  updateProjectManagementSavedView,
  uploadProjectManagementTaskAttachment,
} from '../../api/project-management/projectManagement.api';
import type {
  ProjectManagementTaskComment,
  ProjectManagementTaskCommentUpsertRequest,
  ProjectManagementActivityQuery,
  ProjectManagementTaskBatchUpdateRequest,
  ProjectManagementTaskLabelFilter,
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
import { createTaskMoveRequest } from '../../features/project-management/task-workspace/taskMoveIntent';
import { TaskWorkspaceBatchCommandPanel } from '../../features/project-management/task-workspace/TaskWorkspaceBatchCommandPanel';
import { TaskWorkspaceImConversationPanel } from '../../features/project-management/task-workspace/TaskWorkspaceImConversationPanel';
import { TaskWorkspaceLabelManager } from '../../features/project-management/task-workspace/TaskWorkspaceLabelManager';
import { TaskWorkspaceProjection } from '../../features/project-management/task-workspace/TaskWorkspaceProjection';
import { TaskWorkspaceSelectionPanel } from '../../features/project-management/task-workspace/TaskWorkspaceSelectionPanel';
import { TaskWorkspaceToolbar } from '../../features/project-management/task-workspace/TaskWorkspaceToolbar';
import { getProjectManagementTaskActivities } from '../../features/project-management/task-workspace/taskActivity.api';
import { TaskActivityTimeline } from '../../features/project-management/task-workspace/TaskActivityTimeline';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
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
  calendar: { label: '日期投影', description: '按现有截止日期汇总任务，保持与其他视图相同的查询和权限。', note: '当前为按截止日期聚合的日历原型；月/周网格、日期拖动和快速新建由后续日历 Case 实现。' }
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
  const [batchOpen, setBatchOpen] = useState(false);
  const [selectedTaskIds, setSelectedTaskIds] = useState<Set<string>>(() => new Set());
  const [openingConversationScope, setOpeningConversationScope] = useState<'project' | 'task' | null>(null);
  const [form, setForm] = useState<ProjectManagementTaskUpsertRequest>(emptyForm);
  const [commentForm, setCommentForm] = useState<ProjectManagementTaskCommentUpsertRequest>({ markdown: '' });
  const [commentPageIndex, setCommentPageIndex] = useState(1);
  const [taskActivityQuery, setTaskActivityQuery] = useState<ProjectManagementActivityQuery>({ pageIndex: 1, pageSize: 20 });
  const [editingComment, setEditingComment] = useState<ProjectManagementTaskComment | null>(null);
  const [commentEditForm, setCommentEditForm] = useState<ProjectManagementTaskCommentUpsertRequest>({ markdown: '' });
  const [labelFilter, setLabelFilter] = useState<ProjectManagementTaskLabelFilter>({ labelIds: [], matchMode: 'Any' });
  const query = useMemo(() => ({
    ...taskWorkspaceStateToQuery(projectId, state),
    labelFilter: labelFilter.labelIds.length > 0 ? labelFilter : undefined,
  }), [labelFilter, projectId, state]);
  const activeView = taskViewMeta[state.viewKey];

  useProjectManagementRealtimeConnection('/hubs/system-notification', scope, projectId, scope.isAvailable && Boolean(projectId));

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
    enabled: scope.isAvailable && canViewTaskActivities && Boolean(projectId && selectedTaskId),
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
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId),
    queryFn: ({ signal }) => getProjectManagementTaskComments(selectedTaskId, { pageIndex: commentPageIndex, pageSize: 50, sort: 'timeline' }, signal),
    queryKey: queryKeys.projectManagement.taskComments(scope, projectId, selectedTaskId),
  });
  useEffect(() => {
    setCommentPageIndex(1);
    setTaskActivityQuery({ pageIndex: 1, pageSize: 20 });
    setEditingComment(null);
    setCommentEditForm({ markdown: '' });
  }, [selectedTaskId]);
  const attachmentsQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId),
    queryFn: ({ signal }) => getProjectManagementTaskAttachments(selectedTaskId, signal),
    queryKey: queryKeys.projectManagement.taskAttachments(scope, projectId, selectedTaskId),
  });
  const remindersQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId),
    queryFn: ({ signal }) => getProjectManagementTaskReminders(selectedTaskId, signal),
    queryKey: queryKeys.projectManagement.taskReminders(scope, projectId, selectedTaskId),
  });
  const membersQuery = useQuery({
    enabled: scope.isAvailable && Boolean(projectId && selectedTaskId),
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
  const rows = useMemo(() => tasksQuery.data?.data?.items ?? [], [tasksQuery.data?.data?.items]);
  const selectedListTask = rows.find((task) => task.id === selectedTaskId);
  const selectedTask = taskDetailQuery.data?.data;
  const selectedTasks = useMemo(() => rows.filter((task) => selectedTaskIds.has(task.id)), [rows, selectedTaskIds]);

  useEffect(() => {
    if (selectedTaskId && tasksQuery.isSuccess && !selectedListTask) {
      setState({ selectedTaskId: undefined }, { replace: true });
      setCreating(false);
    }
  }, [selectedListTask, selectedTaskId, setState, tasksQuery.isSuccess]);

  useEffect(() => {
    const availableIds = new Set(rows.map((task) => task.id));
    setSelectedTaskIds((current) => {
      const next = new Set([...current].filter((taskId) => availableIds.has(taskId)));
      return next.size === current.size ? current : next;
    });
  }, [rows]);

  useEffect(() => {
    if (!selectedTask || creating) return;
    setForm({
      assigneeEmploymentId: selectedTask.assigneeEmploymentId,
      assigneeUserId: selectedTask.assigneeUserId,
      description: selectedTask.description,
      dueDate: selectedTask.dueDate,
      estimateMinutes: selectedTask.estimateMinutes,
      milestoneId: selectedTask.milestoneId,
      parentTaskId: selectedTask.parentTaskId,
      priority: selectedTask.priority,
      progressPercent: selectedTask.progressPercent,
      startDate: selectedTask.startDate,
      status: selectedTask.status,
      taskCode: selectedTask.taskCode,
      title: selectedTask.title,
      versionNo: selectedTask.versionNo,
      weight: selectedTask.weight,
    });
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
    mutationFn: () => selectedTask && !creating ? updateProjectManagementTask(selectedTask.id, form) : createProjectManagementTask(projectId, form),
    onError: (error) => message.error(getErrorMessage(error, selectedTask && !creating ? '任务保存失败' : '任务创建失败')),
    onSuccess: async (result) => {
      const task = result.data;
      message.success(creating ? '任务已创建' : '任务已更新');
      setCreating(false);
      setForm(emptyForm);
      setState({ selectedTaskId: task?.id }, { replace: true });
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
    mutationFn: (file: File) => uploadProjectManagementTaskAttachment(selectedTaskId, file),
    onError: (error) => message.error(getErrorMessage(error, '附件上传失败')),
    onSuccess: async () => {
      message.success('附件已上传');
      await attachmentsQuery.refetch();
    },
  });
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
    mutationFn: (request: ProjectManagementTaskBatchUpdateRequest) => updateProjectManagementTasksBatch(request),
    onError: (error) => message.error(getErrorMessage(error, '批量任务更新失败')),
    onSuccess: async (result) => {
      message.success(`已更新 ${result.data?.length ?? selectedTasks.length} 个任务`);
      setBatchOpen(false);
      setSelectedTaskIds(new Set());
      await invalidateProjectTaskViews();
    },
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
          onOpenBatch={() => setBatchOpen(true)}
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
      <section className="pm-detail-region" aria-label={creating ? '新建任务' : selectedTask ? '任务详情' : '任务详情占位区域'}>
        <TaskWorkspaceSelectionPanel
          attachments={attachmentsQuery.data?.data ?? []}
          attachmentsError={attachmentsQuery.isError}
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
            setCreating(false);
            setForm(emptyForm);
            setState({ selectedTaskId: undefined });
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
          onSubmit={() => saveMutation.mutate()}
          onUpload={(file) => attachmentMutation.mutate(file)}
          reminderCreating={reminderCreateMutation.isPending || reminderCancelMutation.isPending || reminderDeleteMutation.isPending}
          reminderMembers={membersQuery.data?.data ?? []}
          reminders={remindersQuery.data?.data ?? []}
          remindersError={remindersQuery.isError}
          remindersLoading={remindersQuery.isLoading}
          saving={saveMutation.isPending}
          selectedTask={selectedTask}
        />
        {selectedTask && !creating ? <TaskActivityTimeline
          canView={canViewTaskActivities}
          isError={taskActivitiesQuery.isError}
          isLoading={taskActivitiesQuery.isLoading}
          onQueryChange={setTaskActivityQuery}
          page={taskActivitiesQuery.data?.data}
          query={taskActivityQuery}
        /> : null}
      </section>
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
      <section className="pm-projection" aria-labelledby="task-projection-title">
        <div className="pm-projection-heading"><div><h2 id="task-projection-title">{activeView.label}</h2><p>当前渲染的是服务端返回的任务数据；选择、批量更新和可用的移动命令均继续使用原有请求链路。</p></div><span className="pm-view-note">{rows.length} 条已加载</span></div>
        <TaskWorkspaceProjection
          onMoveTask={(task, target) => {
            if (moveMutation.isPending) {
              message.error('正在提交上一项任务移动，请稍候');
              return;
            }
            const request = createTaskMoveRequest(task, target);
            if (request) moveMutation.mutate({ taskId: task.id, request });
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
          selectedTaskIds={selectedTaskIds}
          state={state}
        />
      </section>
    </ResponsivePage>
  );
}
