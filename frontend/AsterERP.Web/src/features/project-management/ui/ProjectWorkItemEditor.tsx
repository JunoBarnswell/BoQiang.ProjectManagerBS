import { Box, Stack, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';

import {
  addProjectManagementTaskFollower,
  cancelProjectManagementTaskReminder,
  createProjectManagementTask,
  createProjectManagementTaskComment,
  createProjectManagementTaskDraft,
  createProjectManagementTaskReminders,
  createProjectManagementTaskTimeLog,
  deleteProjectManagementTaskAttachment,
  deleteProjectManagementTaskReminder,
  deleteProjectManagementTaskTimeLog,
  downloadProjectManagementTaskAttachment,
  getProjectManagementMemberCandidates,
  getProjectManagementMilestones,
  getProjectManagementTask,
  getProjectManagementTaskActivities,
  getProjectManagementTaskAttachments,
  getProjectManagementTaskComments,
  getProjectManagementTaskFollowers,
  getProjectManagementTaskReminders,
  getProjectManagementTasks,
  getProjectManagementTaskTimeLogs,
  updateProjectManagementTask,
  uploadProjectManagementTaskAttachment,
  uploadProjectManagementTaskDraftAttachment,
} from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementTaskUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { isHttpError } from '../../../core/http/httpError';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { ProjectManagementMarkdownEditor } from '../collaboration/ProjectManagementMarkdownEditor';
import { ProjectManagementProgressBar } from '../components/ProjectManagementProgressBar';
import { projectManagementEnumLabel, useProjectManagementI18n } from '../projectManagementI18n';
import { getAllowedProjectManagementTaskStatuses } from '../state/projectManagementStatusTransitions';
import { readProjectManagementTaskConflict, taskDetailToForm } from '../state/projectManagementTaskDetailModel';

type EditorTab = 'details' | 'comments' | 'attachments' | 'reminders' | 'timeLogs' | 'activity';

const blank: ProjectManagementTaskUpsertRequest = {
  taskCode: '',
  title: '',
  status: 'Todo',
  priority: 'Medium',
  progressPercent: 0,
  weight: 1,
  workItemType: 'Requirement',
  requirementType: 'Feature',
  requirementSource: 'ProductPlan',
  riskLevel: 'None',
  mentionUserIds: [],
  followerUserIds: [],
};

export function ProjectWorkItemEditor({
  initialStartDate,
  onClose,
  onSaved,
  open,
  projectId,
  taskId,
}: {
  initialStartDate?: string;
  onClose: () => void;
  onSaved: () => void;
  open: boolean;
  projectId: string;
  taskId?: string;
}) {
  const { dateTime, format, t } = useProjectManagementI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const { hasPermission: canEditTask } = usePermission('project-management:task:edit');
  const { hasPermission: canManageAttachment } = usePermission('project-management:attachment:manage');
  const { hasPermission: canManageReminder } = usePermission('project-management:reminder:manage');
  const { hasPermission: canViewReminder } = usePermission('project-management:reminder:view');
  const [tab, setTab] = useState<EditorTab>('details');
  const [form, setForm] = useState(blank);
  const [baseline, setBaseline] = useState(JSON.stringify(blank));
  const [draftId, setDraftId] = useState<string>();
  const [comment, setComment] = useState('');
  const [commentMentionIds, setCommentMentionIds] = useState<string[]>([]);
  const [continueCreating, setContinueCreating] = useState(false);
  const [conflict, setConflict] = useState<ReturnType<typeof readProjectManagementTaskConflict>>();
  const [reminderAt, setReminderAt] = useState('');
  const [reminderScope, setReminderScope] = useState<'Self' | 'Assignee' | 'Participants' | 'Members'>('Self');
  const [reminderNote, setReminderNote] = useState('');
  const [timeStartedAt, setTimeStartedAt] = useState('');
  const [timeEndedAt, setTimeEndedAt] = useState('');
  const [timeNote, setTimeNote] = useState('');

  const task = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor', projectId, taskId], queryFn: ({ signal }) => getProjectManagementTask(taskId!, signal) });
  const members = useQuery({ enabled: open, queryKey: ['pm', 'editor-members', projectId], queryFn: ({ signal }) => getProjectManagementMemberCandidates({ projectId, pageIndex: 1, pageSize: 100 }, signal) });
  const milestones = useQuery({ enabled: open, queryKey: ['pm', 'editor-milestones', projectId], queryFn: ({ signal }) => getProjectManagementMilestones(projectId, signal) });
  const parents = useQuery({ enabled: open, queryKey: ['pm', 'editor-parents', projectId], queryFn: ({ signal }) => getProjectManagementTasks({ projectId, pageIndex: 1, pageSize: 200, viewKey: 'list', workItemType: 'Requirement', includeCompleted: true }, signal) });
  const attachments = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-attachments', taskId], queryFn: ({ signal }) => getProjectManagementTaskAttachments(taskId!, signal) });
  const comments = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-comments', taskId], queryFn: ({ signal }) => getProjectManagementTaskComments(taskId!, { pageIndex: 1, pageSize: 20 }, signal) });
  const followers = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-followers', taskId], queryFn: ({ signal }) => getProjectManagementTaskFollowers(taskId!, signal) });
  const reminders = useQuery({ enabled: open && Boolean(taskId) && canViewReminder && tab === 'reminders', queryKey: ['pm', 'editor-reminders', taskId], queryFn: ({ signal }) => getProjectManagementTaskReminders(taskId!, signal) });
  const timeLogs = useQuery({ enabled: open && Boolean(taskId) && tab === 'timeLogs', queryKey: ['pm', 'editor-time-logs', taskId], queryFn: ({ signal }) => getProjectManagementTaskTimeLogs(taskId!, signal) });
  const activities = useQuery({ enabled: open && Boolean(taskId) && tab === 'activity', queryKey: ['pm', 'editor-activities', taskId], queryFn: ({ signal }) => getProjectManagementTaskActivities(taskId!, { pageIndex: 1, pageSize: 30 }, signal) });

  useEffect(() => {
    if (!open) return;
    const next = task.data?.data
      ? taskDetailToForm(task.data.data)
      : { ...blank, startDate: initialStartDate, dueDate: initialStartDate };
    setForm(next);
    setBaseline(JSON.stringify(next));
    setDraftId(undefined);
    setConflict(undefined);
    setTab('details');
  }, [initialStartDate, open, task.data]);

  const save = useMutation({
    mutationFn: ({ overwriteVersionNo }: { overwriteVersionNo?: number } = {}) => {
      const request = overwriteVersionNo === undefined ? form : { ...form, versionNo: overwriteVersionNo };
      return taskId ? updateProjectManagementTask(taskId, request) : createProjectManagementTask(projectId, { ...request, workItemType: 'Requirement', draftId });
    },
    onSuccess: (result) => {
      const next = taskDetailToForm(result.data);
      message.success(taskId ? t('projectManagement.editor.savedUpdate') : t('projectManagement.editor.savedCreate'));
      setConflict(undefined);
      void queryClient.invalidateQueries({ queryKey: ['pm', 'editor'] });
      onSaved();
      if (continueCreating && !taskId) {
        setForm({ ...blank });
        setBaseline(JSON.stringify(blank));
        setDraftId(undefined);
        return;
      }
      setForm(next);
      setBaseline(JSON.stringify(next));
      onClose();
    },
    onError: (error) => {
      const parsed = readProjectManagementTaskConflict(error);
      if (parsed || (isHttpError(error) && error.status === 409)) {
        setConflict(parsed);
        message.error(t('projectManagement.editor.conflict'));
        return;
      }
      message.error(error instanceof Error ? error.message : t('projectManagement.editor.saveFailed'));
    },
  });

  const upload = useMutation({
    mutationFn: async (file: File) => {
      if (taskId) return uploadProjectManagementTaskAttachment(taskId, file);
      const draft = draftId ? { data: { id: draftId } } : await createProjectManagementTaskDraft(projectId, JSON.stringify(form));
      if (!draftId) setDraftId(draft.data.id);
      return uploadProjectManagementTaskDraftAttachment(draft.data.id, file);
    },
    onSuccess: () => { void attachments.refetch(); message.success(t('projectManagement.editor.uploaded')); },
    onError: () => message.error(t('projectManagement.editor.uploadFailed')),
  });

  const removeAttachment = useMutation({
    mutationFn: (item: { id: string; versionNo: number }) => deleteProjectManagementTaskAttachment(taskId!, item.id, item.versionNo),
    onSuccess: () => { void attachments.refetch(); message.success(t('projectManagement.editor.attachmentDeleted')); },
    onError: () => message.error(t('projectManagement.editor.attachmentDeleteFailed')),
  });

  const addComment = useMutation({
    mutationFn: () => createProjectManagementTaskComment(taskId!, { markdown: comment, mentionUserIds: commentMentionIds }),
    onSuccess: () => { setComment(''); setCommentMentionIds([]); void comments.refetch(); message.success(t('projectManagement.editor.commentPublished')); },
  });

  const follow = useMutation({
    mutationFn: (userId: string) => (taskId ? addProjectManagementTaskFollower(taskId, { userId }) : Promise.resolve(undefined)),
    onSuccess: () => { if (taskId) void followers.refetch(); },
  });

  const addReminder = useMutation({
    mutationFn: () => createProjectManagementTaskReminders(taskId!, {
      reminderAt: new Date(reminderAt).toISOString(),
      timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
      recipientScope: reminderScope,
      note: reminderNote || undefined,
      clientRequestId: crypto.randomUUID(),
    }),
    onSuccess: () => {
      setReminderAt('');
      setReminderNote('');
      void reminders.refetch();
      message.success(t('projectManagement.editor.reminder.created'));
    },
    onError: () => message.error(t('projectManagement.editor.reminder.failed')),
  });

  const cancelReminder = useMutation({
    mutationFn: (item: { id: string; versionNo: number }) => cancelProjectManagementTaskReminder(taskId!, item.id, item.versionNo),
    onSuccess: () => { void reminders.refetch(); message.success(t('projectManagement.editor.reminder.canceled')); },
    onError: () => message.error(t('projectManagement.editor.reminder.failed')),
  });

  const removeReminder = useMutation({
    mutationFn: (item: { id: string; versionNo: number }) => deleteProjectManagementTaskReminder(taskId!, item.id, item.versionNo),
    onSuccess: () => { void reminders.refetch(); message.success(t('projectManagement.editor.reminder.deleted')); },
    onError: () => message.error(t('projectManagement.editor.reminder.failed')),
  });

  const addTimeLog = useMutation({
    mutationFn: () => createProjectManagementTaskTimeLog(taskId!, {
      startedAt: new Date(timeStartedAt).toISOString(),
      endedAt: new Date(timeEndedAt).toISOString(),
      note: timeNote || undefined,
    }),
    onSuccess: () => {
      setTimeNote('');
      void timeLogs.refetch();
      void task.refetch();
      message.success(t('projectManagement.editor.timeLog.created'));
    },
    onError: () => message.error(t('projectManagement.editor.timeLog.failed')),
  });

  const removeTimeLog = useMutation({
    mutationFn: (item: { id: string; versionNo: number }) => deleteProjectManagementTaskTimeLog(taskId!, item.id, item.versionNo),
    onSuccess: () => {
      void timeLogs.refetch();
      void task.refetch();
      message.success(t('projectManagement.editor.timeLog.deleted'));
    },
    onError: () => message.error(t('projectManagement.editor.timeLog.failed')),
  });

  const dirty = JSON.stringify(form) !== baseline || Boolean(draftId);
  const update = (patch: Partial<ProjectManagementTaskUpsertRequest>) => setForm((current) => ({ ...current, ...patch }));
  const requestClose = () => {
    if (!dirty) { onClose(); return; }
    confirm({ title: t('projectManagement.editor.closeTitle'), content: t('projectManagement.editor.closeDescription'), confirmText: t('projectManagement.editor.discard'), onConfirm: onClose });
  };
  const candidateItems = useMemo(() => members.data?.data.items.filter((item) => item.isSelectable) ?? [], [members.data?.data.items]);
  const parentItems = parents.data?.data.items.filter((item) => item.id !== taskId) ?? [];
  const milestoneItems = milestones.data?.data.items ?? [];
  const followerIds = followers.data?.data.map((item) => item.userId) ?? form.followerUserIds ?? [];
  const labels = useMemo(() => Object.fromEntries(candidateItems.map((item) => [item.userId, item.displayName])), [candidateItems]);
  const enumLabels = (group: 'priority' | 'status' | 'workItemType' | 'risk' | 'requirementType' | 'requirementSource', values: string[]) =>
    Object.fromEntries(values.map((value) => [value, projectManagementEnumLabel(t, group, value)]));
  const statusOptions = getAllowedProjectManagementTaskStatuses(form.status ?? 'Todo');
  const tabs: Array<[EditorTab, string]> = [
    ['details', t('projectManagement.editor.tab.details')],
    ['comments', t('projectManagement.editor.tab.comments')],
    ['attachments', t('projectManagement.editor.tab.attachments')],
    ['reminders', t('projectManagement.editor.tab.reminders')],
    ['timeLogs', t('projectManagement.editor.tab.timeLogs')],
    ['activity', t('projectManagement.editor.tab.activity')],
  ];
  const footer = (
    <Stack alignItems="center" direction="row" justifyContent="space-between">
      <label className="pm-editor-checkbox"><input checked={continueCreating} onChange={(event) => setContinueCreating(event.target.checked)} type="checkbox" /> {t('projectManagement.editor.continueCreating')}</label>
      <Stack direction="row" spacing={1}>
        <button className="pm-workbench-command" onClick={requestClose} type="button">{t('projectManagement.editor.cancel')}</button>
        <button className="pm-primary-button" disabled={!form.title.trim() || save.isPending || !canEditTask} onClick={() => save.mutate({})} type="button">{t('projectManagement.editor.save')}</button>
      </Stack>
    </Stack>
  );

  return (
    <ResponsiveModal bodyClassName="pm-work-item-editor-body" className="pm-work-item-editor" footer={footer} maxWidth="96vw" mode="modal" onClose={requestClose} open={open} title={taskId ? t('projectManagement.editor.edit') : t('projectManagement.editor.create')}>
      {conflict ? <ConflictPanel conflict={conflict} onKeepLocal={() => setConflict(undefined)} onOverwrite={() => save.mutate({ overwriteVersionNo: conflict.serverValues.versionNo })} onReload={() => { const next = taskDetailToForm(conflict.serverValues); setForm(next); setBaseline(JSON.stringify(next)); setConflict(undefined); }} t={t} /> : null}
      <Box className="pm-editor-tabs">
        {tabs.map(([key, label]) => (
          <button className={tab === key ? 'pm-editor-tab is-active' : 'pm-editor-tab'} key={key} onClick={() => setTab(key)} type="button">{label}</button>
        ))}
      </Box>
      <Box className="pm-editor-grid">
        <Stack className="pm-editor-main" spacing={1.25}>
          {tab === 'details' ? (
            <>
              <input aria-label={t('projectManagement.editor.titleAria')} className="pm-editor-title" maxLength={256} onChange={(event) => update({ title: event.target.value })} placeholder={t('projectManagement.editor.titlePlaceholder')} value={form.title} />
              <Typography color="text.secondary" variant="caption">{form.title.length}/256</Typography>
              <ProjectManagementMarkdownEditor ariaLabel={t('projectManagement.editor.descriptionAria')} contentJson={form.contentJson} mentionCandidates={candidateItems} onChange={(value) => update({ description: value, markdown: value })} onContentJsonChange={(value) => update({ contentJson: value })} onMentionUserIdsChange={(value) => update({ mentionUserIds: value })} placeholder={t('projectManagement.editor.descriptionPlaceholder')} rows={14} value={form.description ?? ''} />
            </>
          ) : null}
          {tab === 'comments' ? (
            taskId ? (
              <Stack className="pm-editor-tab-panel" spacing={1}>
                {comments.data?.data.items.map((item) => (
                  <Box className="pm-comment" key={item.id}>
                    <Typography color="text.secondary" variant="caption">{item.authorDisplayName ?? item.authorUserId} · {dateTime(item.createdTime)}</Typography>
                    <Typography variant="body2">{item.markdown}</Typography>
                  </Box>
                ))}
                <ProjectManagementMarkdownEditor ariaLabel={t('projectManagement.editor.commentAria')} mentionCandidates={candidateItems} onChange={setComment} onMentionUserIdsChange={setCommentMentionIds} placeholder={t('projectManagement.editor.commentPlaceholder')} rows={4} value={comment} />
                <button className="pm-workbench-command" disabled={!comment.trim() || addComment.isPending} onClick={() => addComment.mutate()} type="button">{t('projectManagement.editor.publishComment')}</button>
              </Stack>
            ) : <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.saveFirst')}</Typography>
          ) : null}
          {tab === 'attachments' ? (
            <Stack className="pm-editor-tab-panel" spacing={1}>
              <Stack alignItems="center" direction="row" justifyContent="space-between">
                <Typography fontWeight={700}>{t('projectManagement.editor.attachment')}</Typography>
                {canManageAttachment ? (
                  <label className="pm-workbench-command">{t('projectManagement.editor.upload')}<input hidden onChange={(event) => { const file = event.target.files?.[0]; if (file) upload.mutate(file); event.currentTarget.value = ''; }} type="file" /></label>
                ) : null}
              </Stack>
              {attachments.data?.data.map((item) => (
                <Stack alignItems="center" direction="row" justifyContent="space-between" key={item.id} sx={{ p: 1, border: '1px solid #edf0f4', borderRadius: 1 }}>
                  <Typography variant="body2">{item.fileName} · {Math.ceil(item.fileSize / 1024)} KB</Typography>
                  <Stack direction="row" spacing={1}>
                    <button className="pm-workbench-command" onClick={() => void downloadProjectManagementTaskAttachment(item).then(({ blob, fileName }) => {
                      const url = URL.createObjectURL(blob);
                      const anchor = document.createElement('a');
                      anchor.href = url;
                      anchor.download = fileName;
                      anchor.click();
                      URL.revokeObjectURL(url);
                    })} type="button">{t('projectManagement.editor.download')}</button>
                    {canManageAttachment && taskId ? (
                      <button className="pm-workbench-command" onClick={() => removeAttachment.mutate(item)} type="button">{t('projectManagement.editor.deleteAttachment')}</button>
                    ) : null}
                  </Stack>
                </Stack>
              ))}
              {!taskId ? <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.afterSaveHint')}</Typography> : null}
            </Stack>
          ) : null}
          {tab === 'reminders' ? (
            taskId && canViewReminder ? (
              <Stack className="pm-editor-tab-panel" spacing={1}>
                {canManageReminder ? (
                  <Stack direction={{ xs: 'column', md: 'row' }} spacing={1}>
                    <input className="pm-editor-select" onChange={(event) => setReminderAt(event.target.value)} type="datetime-local" value={reminderAt} />
                    <select className="pm-editor-select" onChange={(event) => setReminderScope(event.target.value as typeof reminderScope)} value={reminderScope}>
                      {(['Self', 'Assignee', 'Participants', 'Members'] as const).map((scope) => <option key={scope} value={scope}>{t(`projectManagement.editor.reminder.scope.${scope}`)}</option>)}
                    </select>
                    <input className="pm-editor-select" onChange={(event) => setReminderNote(event.target.value)} placeholder={t('projectManagement.editor.reminder.note')} value={reminderNote} />
                    <button className="pm-primary-button" disabled={!reminderAt || addReminder.isPending} onClick={() => addReminder.mutate()} type="button">{t('projectManagement.editor.reminder.add')}</button>
                  </Stack>
                ) : null}
                {reminders.data?.data.length ? reminders.data.data.map((item) => (
                  <Stack alignItems="center" direction="row" justifyContent="space-between" key={item.id} sx={{ p: 1, border: '1px solid #edf0f4', borderRadius: 1 }}>
                    <Stack spacing={0.25}>
                      <Typography variant="body2">{dateTime(item.reminderAtUtc)} · {item.status}</Typography>
                      <Typography color="text.secondary" variant="caption">{item.note || '—'}</Typography>
                    </Stack>
                    {canManageReminder ? (
                      <Stack direction="row" spacing={1}>
                        {item.status === 'Pending' ? <button className="pm-workbench-command" onClick={() => cancelReminder.mutate(item)} type="button">{t('projectManagement.editor.reminder.cancel')}</button> : null}
                        <button className="pm-workbench-command" onClick={() => removeReminder.mutate(item)} type="button">{t('projectManagement.editor.reminder.delete')}</button>
                      </Stack>
                    ) : null}
                  </Stack>
                )) : <Typography color="text.secondary" variant="body2">{t('projectManagement.editor.reminder.empty')}</Typography>}
              </Stack>
            ) : <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.saveFirst')}</Typography>
          ) : null}
          {tab === 'timeLogs' ? (
            taskId ? (
              <Stack className="pm-editor-tab-panel" spacing={1}>
                <Typography fontWeight={700}>{format('projectManagement.editor.timeLog.summary', { estimate: form.estimateMinutes ?? 0, actual: task.data?.data.actualMinutes ?? 0 })}</Typography>
                {canEditTask ? (
                  <Stack direction={{ xs: 'column', md: 'row' }} spacing={1}>
                    <input className="pm-editor-select" onChange={(event) => setTimeStartedAt(event.target.value)} type="datetime-local" value={timeStartedAt} />
                    <input className="pm-editor-select" onChange={(event) => setTimeEndedAt(event.target.value)} type="datetime-local" value={timeEndedAt} />
                    <input className="pm-editor-select" onChange={(event) => setTimeNote(event.target.value)} placeholder={t('projectManagement.editor.timeLog.note')} value={timeNote} />
                    <button className="pm-primary-button" disabled={!timeStartedAt || !timeEndedAt || addTimeLog.isPending} onClick={() => addTimeLog.mutate()} type="button">{t('projectManagement.editor.timeLog.add')}</button>
                  </Stack>
                ) : null}
                {timeLogs.data?.data.length ? timeLogs.data.data.map((item) => (
                  <Stack alignItems="center" direction="row" justifyContent="space-between" key={item.id} sx={{ p: 1, border: '1px solid #edf0f4', borderRadius: 1 }}>
                    <Stack spacing={0.25}>
                      <Typography variant="body2">{item.minutes} {t('projectManagement.editor.timeLog.minutes')} · {dateTime(item.startedAt)} — {dateTime(item.endedAt)}</Typography>
                      <Typography color="text.secondary" variant="caption">{item.note || '—'}</Typography>
                    </Stack>
                    {canEditTask ? <button className="pm-workbench-command" onClick={() => removeTimeLog.mutate(item)} type="button">{t('projectManagement.editor.timeLog.delete')}</button> : null}
                  </Stack>
                )) : <Typography color="text.secondary" variant="body2">{t('projectManagement.editor.timeLog.empty')}</Typography>}
              </Stack>
            ) : <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.saveFirst')}</Typography>
          ) : null}
          {tab === 'activity' ? (
            taskId ? (
              <Stack className="pm-editor-tab-panel" spacing={1}>
                <ProjectManagementProgressBar dueDate={form.dueDate} progressPercent={form.progressPercent ?? 0} status={form.status} />
                {activities.data?.data.items.length ? activities.data.data.items.map((item) => (
                  <Stack direction="row" key={item.id} spacing={1} sx={{ py: 0.8, borderBottom: '1px solid #f0f2f6' }}>
                    <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: '#3b82f6', mt: 0.8 }} />
                    <Typography sx={{ flex: 1 }} variant="body2">{item.summaryText ? format(item.summaryText.key, item.summaryText.arguments) : item.summary ?? item.activityType}</Typography>
                    <Typography color="text.secondary" variant="caption">{dateTime(item.createdTime)}</Typography>
                  </Stack>
                )) : <Typography color="text.secondary" variant="body2">{t('projectManagement.editor.activity.empty')}</Typography>}
              </Stack>
            ) : <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.saveFirst')}</Typography>
          ) : null}
        </Stack>
        <Stack className="pm-editor-properties" spacing={1.1}>
          <Field label={t('projectManagement.editor.field.project')} value={t('projectManagement.editor.currentProject')} />
          <SelectField label={t('projectManagement.editor.field.workItemType')} labels={enumLabels('workItemType', ['Requirement', 'UserStory', 'Task', 'Bug'])} onChange={(value) => update({ workItemType: value })} options={['Requirement', 'UserStory', 'Task', 'Bug']} value={form.workItemType ?? 'Requirement'} t={t} />
          <SelectField label={t('projectManagement.editor.field.status')} labels={enumLabels('status', statusOptions)} onChange={(value) => update({ status: value })} options={statusOptions} value={form.status ?? 'Todo'} t={t} />
          <Stack spacing={0.25}>
            <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.field.progress')}</Typography>
            <Stack alignItems="center" direction="row" spacing={1}>
              <input max={100} min={0} onChange={(event) => update({ progressPercent: Number(event.target.value) })} style={{ flex: 1 }} type="range" value={form.progressPercent ?? 0} />
              <Typography variant="caption">{form.progressPercent ?? 0}%</Typography>
            </Stack>
            <ProjectManagementProgressBar dueDate={form.dueDate} progressPercent={form.progressPercent ?? 0} status={form.status} />
          </Stack>
          <NumberField label={t('projectManagement.editor.field.estimateMinutes')} onChange={(value) => update({ estimateMinutes: value })} value={form.estimateMinutes} />
          {taskId ? <Field label={t('projectManagement.editor.field.actualMinutes')} value={String(task.data?.data.actualMinutes ?? 0)} /> : null}
          <SelectField label={t('projectManagement.editor.field.assignee')} labels={labels} onChange={(value) => update({ assigneeUserId: value || undefined })} options={['', ...candidateItems.map((item) => item.userId)]} value={form.assigneeUserId ?? ''} t={t} />
          <SelectField label={t('projectManagement.editor.field.parent')} labels={Object.fromEntries(parentItems.map((item) => [item.id, `${item.taskCode} · ${item.title}`]))} onChange={(value) => update({ parentTaskId: value || undefined })} options={['', ...parentItems.map((item) => item.id)]} value={form.parentTaskId ?? ''} t={t} />
          <SelectField label={t('projectManagement.editor.field.milestone')} labels={Object.fromEntries(milestoneItems.map((item) => [item.id, item.milestoneName]))} onChange={(value) => update({ milestoneId: value || undefined })} options={['', ...milestoneItems.map((item) => item.id)]} value={form.milestoneId ?? ''} t={t} />
          <DateField label={t('projectManagement.editor.field.startDate')} onChange={(value) => update({ startDate: value || undefined })} value={form.startDate} />
          <DateField label={t('projectManagement.editor.field.dueDate')} onChange={(value) => update({ dueDate: value || undefined })} value={form.dueDate} />
          <SelectField label={t('projectManagement.editor.field.priority')} labels={enumLabels('priority', ['Low', 'Medium', 'High', 'Urgent'])} onChange={(value) => update({ priority: value })} options={['Low', 'Medium', 'High', 'Urgent']} value={form.priority ?? 'Medium'} t={t} />
          <SelectField label={t('projectManagement.editor.field.risk')} labels={enumLabels('risk', ['None', 'Low', 'Medium', 'High'])} onChange={(value) => update({ riskLevel: value })} options={['None', 'Low', 'Medium', 'High']} value={form.riskLevel ?? 'None'} t={t} />
          <SelectField label={t('projectManagement.editor.field.requirementType')} labels={enumLabels('requirementType', ['Feature', 'NonFunctional', 'Other'])} onChange={(value) => update({ requirementType: value || undefined })} options={['', 'Feature', 'NonFunctional', 'Other']} value={form.requirementType ?? ''} t={t} />
          <SelectField label={t('projectManagement.editor.field.requirementSource')} labels={enumLabels('requirementSource', ['ProductPlan', 'Customer', 'Internal', 'BugConversion', 'Other'])} onChange={(value) => update({ requirementSource: value || undefined })} options={['', 'ProductPlan', 'Customer', 'Internal', 'BugConversion', 'Other']} value={form.requirementSource ?? ''} t={t} />
          <SelectField label={t('projectManagement.editor.field.followers')} labels={labels} onChange={(value) => { if (value && !followerIds.includes(value)) { follow.mutate(value); update({ followerUserIds: [...followerIds, value] }); } }} options={['', ...candidateItems.map((item) => item.userId)]} value="" t={t} />
        </Stack>
      </Box>
    </ResponsiveModal>
  );
}

function ConflictPanel({ conflict, onKeepLocal, onOverwrite, onReload, t }: { conflict: NonNullable<ReturnType<typeof readProjectManagementTaskConflict>>; onKeepLocal: () => void; onOverwrite: () => void; onReload: () => void; t: (key: string) => string }) {
  return <Box className="pm-task-conflict" role="alert"><Typography fontWeight={700}>{t('projectManagement.editor.conflictDetected')}</Typography>{conflict.fieldConflicts.map((field) => <Typography key={field.field} variant="caption">{field.displayName}: {t('projectManagement.editor.server')} {String(field.serverValue ?? '—')} / {t('projectManagement.editor.local')} {String(field.localValue ?? '—')}</Typography>)}<Stack direction="row" spacing={1}><button className="pm-workbench-command" onClick={onReload} type="button">{t('projectManagement.editor.reload')}</button><button className="pm-workbench-command" onClick={onKeepLocal} type="button">{t('projectManagement.editor.keepLocal')}</button><button className="pm-primary-button" onClick={onOverwrite} type="button">{t('projectManagement.editor.overwrite')}</button></Stack></Box>;
}

function Field({ label, value }: { label: string; value: string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><Typography className="pm-editor-readonly" variant="body2">{value || '—'}</Typography></Stack>; }
function SelectField({ label, labels = {}, onChange, options, value, t }: { label: string; labels?: Record<string, string>; onChange: (value: string) => void; options: string[]; value: string; t: (key: string) => string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><select className="pm-editor-select" onChange={(event) => onChange(event.target.value)} value={value}>{options.map((option) => <option key={option} value={option}>{option ? labels[option] ?? option : t('projectManagement.workbench.unknown')}</option>)}</select></Stack>; }
function DateField({ label, onChange, value }: { label: string; onChange: (value: string) => void; value?: string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><input className="pm-editor-select" onChange={(event) => onChange(event.target.value)} type="date" value={value ? value.slice(0, 10) : ''} /></Stack>; }
function NumberField({ label, onChange, value }: { label: string; onChange: (value: number | undefined) => void; value?: number }) {
  return (
    <Stack spacing={0.25}>
      <Typography color="text.secondary" variant="caption">{label}</Typography>
      <input className="pm-editor-select" min={0} onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))} type="number" value={value ?? ''} />
    </Stack>
  );
}
