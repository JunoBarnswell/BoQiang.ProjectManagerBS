import { Box, Stack, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState, type ReactNode } from 'react';

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
  previewProjectManagementTaskAttachment,
  updateProjectManagementTask,
  uploadProjectManagementTaskAttachment,
  uploadProjectManagementTaskDraftAttachment,
} from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementMemberCandidate, ProjectManagementTaskAttachment, ProjectManagementTaskComment, ProjectManagementTaskUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { isHttpError } from '../../../core/http/httpError';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { FilePreviewDialog } from '../../../shared/file-preview/FilePreviewDialog';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { ProjectManagementMarkdownEditor, ProjectManagementMarkdownContent } from '../collaboration/ProjectManagementMarkdownEditor';
import { ProjectManagementProgressBar } from '../components/ProjectManagementProgressBar';
import { ProjectManagementCountdown } from '../components/ProjectManagementCountdown';
import { projectManagementEnumLabel, useProjectManagementI18n } from '../projectManagementI18n';
import { getAllowedProjectManagementTaskStatuses } from '../state/projectManagementStatusTransitions';
import { readProjectManagementTaskConflict, taskDetailToForm } from '../state/projectManagementTaskDetailModel';

type CollaborationFilter = 'all' | 'comments' | 'attachments' | 'activity';

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
  const [collaborationFilter, setCollaborationFilter] = useState<CollaborationFilter>('all');
  const [remindersOpen, setRemindersOpen] = useState(false);
  const [timeLogsOpen, setTimeLogsOpen] = useState(false);
  const [form, setForm] = useState(blank);
  const [baseline, setBaseline] = useState(JSON.stringify(blank));
  const [draftId, setDraftId] = useState<string>();
  const [comment, setComment] = useState('');
  const [commentContentJson, setCommentContentJson] = useState<string>();
  const [commentMentionIds, setCommentMentionIds] = useState<string[]>([]);
  const [commentFile, setCommentFile] = useState<File>();
  const [continueCreating, setContinueCreating] = useState(false);
  const [conflict, setConflict] = useState<ReturnType<typeof readProjectManagementTaskConflict>>();
  const [reminderAt, setReminderAt] = useState('');
  const [reminderScope, setReminderScope] = useState<'Self' | 'Assignee' | 'Participants' | 'Members'>('Self');
  const [reminderNote, setReminderNote] = useState('');
  const [timeStartedAt, setTimeStartedAt] = useState('');
  const [timeEndedAt, setTimeEndedAt] = useState('');
  const [timeNote, setTimeNote] = useState('');
  const [preview, setPreview] = useState<{ error?: string; file?: { fileName: string; extension: string }; loading: boolean; open: boolean; previewFile?: File }>({ loading: false, open: false });

  const task = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor', projectId, taskId], queryFn: ({ signal }) => getProjectManagementTask(taskId!, signal) });
  const members = useQuery({ enabled: open, queryKey: ['pm', 'editor-members', projectId], queryFn: ({ signal }) => getProjectManagementMemberCandidates({ projectId, pageIndex: 1, pageSize: 100 }, signal) });
  const milestones = useQuery({ enabled: open, queryKey: ['pm', 'editor-milestones', projectId], queryFn: ({ signal }) => getProjectManagementMilestones(projectId, signal) });
  const parents = useQuery({ enabled: open, queryKey: ['pm', 'editor-parents', projectId], queryFn: ({ signal }) => getProjectManagementTasks({ projectId, pageIndex: 1, pageSize: 200, viewKey: 'list', workItemType: 'Requirement', includeCompleted: true }, signal) });
  const attachments = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-attachments', taskId], queryFn: ({ signal }) => getProjectManagementTaskAttachments(taskId!, signal) });
  const comments = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-comments', taskId], queryFn: ({ signal }) => getProjectManagementTaskComments(taskId!, { pageIndex: 1, pageSize: 20 }, signal) });
  const followers = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-followers', taskId], queryFn: ({ signal }) => getProjectManagementTaskFollowers(taskId!, signal) });
  const reminders = useQuery({ enabled: open && Boolean(taskId) && canViewReminder && remindersOpen, queryKey: ['pm', 'editor-reminders', taskId], queryFn: ({ signal }) => getProjectManagementTaskReminders(taskId!, signal) });
  const timeLogs = useQuery({ enabled: open && Boolean(taskId) && timeLogsOpen, queryKey: ['pm', 'editor-time-logs', taskId], queryFn: ({ signal }) => getProjectManagementTaskTimeLogs(taskId!, signal) });
  const activities = useQuery({ enabled: open && Boolean(taskId) && (collaborationFilter === 'all' || collaborationFilter === 'activity'), queryKey: ['pm', 'editor-activities', taskId], queryFn: ({ signal }) => getProjectManagementTaskActivities(taskId!, { pageIndex: 1, pageSize: 30 }, signal) });

  useEffect(() => {
    if (!open) return;
    const next = task.data?.data
      ? taskDetailToForm(task.data.data)
      : { ...blank, startDate: initialStartDate, dueDate: initialStartDate };
    setForm(next);
    setBaseline(JSON.stringify(next));
    setDraftId(undefined);
    setConflict(undefined);
    setCollaborationFilter('all');
    setCommentFile(undefined);
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
    onError: (error) => message.error(error instanceof Error
      ? error.message
      : t(taskId ? 'projectManagement.editor.uploadFailed' : 'projectManagement.editor.draftUploadFailed')),
  });

  const removeAttachment = useMutation({
    mutationFn: (item: { id: string; versionNo: number }) => deleteProjectManagementTaskAttachment(taskId!, item.id, item.versionNo),
    onSuccess: () => { void attachments.refetch(); message.success(t('projectManagement.editor.attachmentDeleted')); },
    onError: () => message.error(t('projectManagement.editor.attachmentDeleteFailed')),
  });

  const addComment = useMutation({
    mutationFn: async () => {
      const attachment = commentFile ? await uploadProjectManagementTaskAttachment(taskId!, commentFile) : undefined;
      return createProjectManagementTaskComment(taskId!, { markdown: comment, mentionUserIds: commentMentionIds, attachmentId: attachment?.data.id });
    },
    onSuccess: () => { setComment(''); setCommentContentJson(undefined); setCommentMentionIds([]); setCommentFile(undefined); void comments.refetch(); void attachments.refetch(); message.success(t('projectManagement.editor.commentPublished')); },
    onError: (error) => message.error(error instanceof Error ? error.message : t('projectManagement.editor.commentPublishFailed')),
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
  const downloadAttachment = async (item: ProjectManagementTaskAttachment) => {
    const { blob, fileName } = await downloadProjectManagementTaskAttachment(item);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  };
  const previewAttachment = async (item: ProjectManagementTaskAttachment) => {
    if (!item.previewSupported) {
      message.warning(t('projectManagement.editor.previewUnsupported'));
      return;
    }
    setPreview({ file: { fileName: item.fileName, extension: item.fileName.split('.').pop() ?? '' }, loading: true, open: true });
    try {
      const { blob, fileName } = await previewProjectManagementTaskAttachment(item);
      setPreview({ file: { fileName, extension: fileName.split('.').pop() ?? '' }, loading: false, open: true, previewFile: new File([blob], fileName, { type: blob.type || item.contentType }) });
    } catch (error) {
      setPreview({ error: error instanceof Error ? error.message : t('projectManagement.editor.previewFailed'), file: { fileName: item.fileName, extension: item.fileName.split('.').pop() ?? '' }, loading: false, open: true });
    }
  };
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
      <Box className="pm-editor-grid">
        <Stack className="pm-editor-main" spacing={1.25}>
          <input aria-label={t('projectManagement.editor.titleAria')} className="pm-editor-title" maxLength={256} onChange={(event) => update({ title: event.target.value })} placeholder={t('projectManagement.editor.titlePlaceholder')} value={form.title} />
          <Typography color="text.secondary" variant="caption">{form.title.length}/256</Typography>
          <ProjectManagementMarkdownEditor ariaLabel={t('projectManagement.editor.descriptionAria')} contentJson={form.contentJson} mentionCandidates={candidateItems} onChange={(value) => update({ description: value, markdown: value })} onContentJsonChange={(value) => update({ contentJson: value })} onMentionUserIdsChange={(value) => update({ mentionUserIds: value })} placeholder={t('projectManagement.editor.descriptionPlaceholder')} rows={10} value={form.description ?? ''} />
          {taskId ? <CollaborationPanel
            activities={activities.data?.data.items ?? []}
            attachments={attachments.data?.data ?? []}
            canManageAttachment={canManageAttachment}
            comments={comments.data?.data.items ?? []}
            comment={comment}
            commentContentJson={commentContentJson}
            commentFile={commentFile}
            filter={collaborationFilter}
            isPublishing={addComment.isPending}
            onCommentChange={setComment}
            onCommentContentJsonChange={setCommentContentJson}
            onCommentFileChange={setCommentFile}
            onCommentMentionsChange={setCommentMentionIds}
            onDeleteAttachment={(item) => removeAttachment.mutate(item)}
            onDownload={(item) => void downloadAttachment(item)}
            onFilterChange={setCollaborationFilter}
            onPreview={(item) => void previewAttachment(item)}
            onPublish={() => addComment.mutate()}
            onUpload={(file) => upload.mutate(file)}
            mentionCandidates={candidateItems}
            t={t}
            dateTime={dateTime}
          /> : <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.saveFirst')}</Typography>}
        </Stack>
        <Stack className="pm-editor-properties" spacing={0}>
          <Box className="pm-editor-property-group">
            <Typography className="pm-editor-property-group__title" component="h3">{t('projectManagement.editor.group.basic')}</Typography>
            <Field label={t('projectManagement.editor.field.project')} value={t('projectManagement.editor.currentProject')} />
            <SelectField label={t('projectManagement.editor.field.workItemType')} labels={enumLabels('workItemType', ['Requirement', 'UserStory', 'Task', 'Bug'])} onChange={(value) => update({ workItemType: value })} options={['Requirement', 'UserStory', 'Task', 'Bug']} value={form.workItemType ?? 'Requirement'} t={t} />
            <SelectField label={t('projectManagement.editor.field.status')} labels={enumLabels('status', statusOptions)} onChange={(value) => update({ status: value })} options={statusOptions} value={form.status ?? 'Todo'} t={t} />
            <Stack spacing={0.25}>
              <Typography className="pm-editor-field-label" component="span">{t('projectManagement.editor.field.progress')}</Typography>
              <Stack alignItems="center" direction="row" spacing={1}>
                <input max={100} min={0} onChange={(event) => update({ progressPercent: Number(event.target.value) })} style={{ flex: 1 }} type="range" value={form.progressPercent ?? 0} />
                <Typography variant="caption">{form.progressPercent ?? 0}%</Typography>
              </Stack>
              <ProjectManagementProgressBar dueDate={form.dueDate} progressPercent={form.progressPercent ?? 0} status={form.status} />
            </Stack>
            <SelectField label={t('projectManagement.editor.field.assignee')} labels={labels} onChange={(value) => update({ assigneeUserId: value || undefined })} options={['', ...candidateItems.map((item) => item.userId)]} value={form.assigneeUserId ?? ''} t={t} />
            <SelectField label={t('projectManagement.editor.field.followers')} labels={labels} onChange={(value) => { if (value && !followerIds.includes(value)) { follow.mutate(value); update({ followerUserIds: [...followerIds, value] }); } }} options={['', ...candidateItems.map((item) => item.userId)]} value="" t={t} />
          </Box>
          <Box className="pm-editor-property-group">
            <Typography className="pm-editor-property-group__title" component="h3">{t('projectManagement.editor.group.schedule')}</Typography>
            <NumberField label={t('projectManagement.editor.field.estimateMinutes')} onChange={(value) => update({ estimateMinutes: value })} value={form.estimateMinutes} />
            {taskId ? <Field label={t('projectManagement.editor.field.actualMinutes')} value={String(task.data?.data.actualMinutes ?? 0)} /> : null}
            <DateField label={t('projectManagement.editor.field.startDate')} onChange={(value) => update({ startDate: value || undefined })} value={form.startDate} />
            <DateField label={t('projectManagement.editor.field.dueDate')} onChange={(value) => update({ dueDate: value || undefined })} value={form.dueDate} />
            <ProjectManagementCountdown dueDate={form.dueDate} status={form.status} />
            <SelectField label={t('projectManagement.editor.field.parent')} labels={Object.fromEntries(parentItems.map((item) => [item.id, `${item.taskCode} · ${item.title}`]))} onChange={(value) => update({ parentTaskId: value || undefined })} options={['', ...parentItems.map((item) => item.id)]} value={form.parentTaskId ?? ''} t={t} />
            <SelectField label={t('projectManagement.editor.field.milestone')} labels={Object.fromEntries(milestoneItems.map((item) => [item.id, item.milestoneName]))} onChange={(value) => update({ milestoneId: value || undefined })} options={['', ...milestoneItems.map((item) => item.id)]} value={form.milestoneId ?? ''} t={t} />
          </Box>
          <Box className="pm-editor-property-group">
            <Typography className="pm-editor-property-group__title" component="h3">{t('projectManagement.editor.group.classification')}</Typography>
            <SelectField label={t('projectManagement.editor.field.priority')} labels={enumLabels('priority', ['Low', 'Medium', 'High', 'Urgent'])} onChange={(value) => update({ priority: value })} options={['Low', 'Medium', 'High', 'Urgent']} value={form.priority ?? 'Medium'} t={t} />
            <SelectField label={t('projectManagement.editor.field.risk')} labels={enumLabels('risk', ['None', 'Low', 'Medium', 'High'])} onChange={(value) => update({ riskLevel: value })} options={['None', 'Low', 'Medium', 'High']} value={form.riskLevel ?? 'None'} t={t} />
            <SelectField label={t('projectManagement.editor.field.requirementType')} labels={enumLabels('requirementType', ['Feature', 'NonFunctional', 'Other'])} onChange={(value) => update({ requirementType: value || undefined })} options={['', 'Feature', 'NonFunctional', 'Other']} value={form.requirementType ?? ''} t={t} />
            <SelectField label={t('projectManagement.editor.field.requirementSource')} labels={enumLabels('requirementSource', ['ProductPlan', 'Customer', 'Internal', 'BugConversion', 'Other'])} onChange={(value) => update({ requirementSource: value || undefined })} options={['', 'ProductPlan', 'Customer', 'Internal', 'BugConversion', 'Other']} value={form.requirementSource ?? ''} t={t} />
          </Box>
          {taskId ? <CollapsibleEditorGroup open={timeLogsOpen} onToggle={() => setTimeLogsOpen((value) => !value)} title={t('projectManagement.editor.timeLog.title')}>
            <Typography fontWeight={700} variant="body2">{format('projectManagement.editor.timeLog.summary', { estimate: form.estimateMinutes ?? 0, actual: task.data?.data.actualMinutes ?? 0 })}</Typography>
            {canEditTask ? <Stack spacing={0.75}>
              <input className="pm-editor-select" onChange={(event) => setTimeStartedAt(event.target.value)} type="datetime-local" value={timeStartedAt} />
              <input className="pm-editor-select" onChange={(event) => setTimeEndedAt(event.target.value)} type="datetime-local" value={timeEndedAt} />
              <input className="pm-editor-select" onChange={(event) => setTimeNote(event.target.value)} placeholder={t('projectManagement.editor.timeLog.note')} value={timeNote} />
              <button className="pm-primary-button" disabled={!timeStartedAt || !timeEndedAt || addTimeLog.isPending} onClick={() => addTimeLog.mutate()} type="button">{t('projectManagement.editor.timeLog.add')}</button>
            </Stack> : null}
            {timeLogs.data?.data.map((item) => <Stack alignItems="center" className="pm-editor-list-item" direction="row" justifyContent="space-between" key={item.id}><Typography variant="caption">{item.minutes} {t('projectManagement.editor.timeLog.minutes')} · {dateTime(item.startedAt)}</Typography>{canEditTask ? <button className="pm-workbench-command" onClick={() => removeTimeLog.mutate(item)} type="button">{t('projectManagement.editor.timeLog.delete')}</button> : null}</Stack>)}
          </CollapsibleEditorGroup> : null}
          {taskId && canViewReminder ? <CollapsibleEditorGroup open={remindersOpen} onToggle={() => setRemindersOpen((value) => !value)} title={t('projectManagement.editor.reminder.title')}>
            {canManageReminder ? <Stack spacing={0.75}>
              <input className="pm-editor-select" onChange={(event) => setReminderAt(event.target.value)} type="datetime-local" value={reminderAt} />
              <select className="pm-editor-select" onChange={(event) => setReminderScope(event.target.value as typeof reminderScope)} value={reminderScope}>{(['Self', 'Assignee', 'Participants', 'Members'] as const).map((scope) => <option key={scope} value={scope}>{t(`projectManagement.editor.reminder.scope.${scope}`)}</option>)}</select>
              <input className="pm-editor-select" onChange={(event) => setReminderNote(event.target.value)} placeholder={t('projectManagement.editor.reminder.note')} value={reminderNote} />
              <button className="pm-primary-button" disabled={!reminderAt || addReminder.isPending} onClick={() => addReminder.mutate()} type="button">{t('projectManagement.editor.reminder.add')}</button>
            </Stack> : null}
            {reminders.data?.data.map((item) => <Stack alignItems="center" className="pm-editor-list-item" direction="row" justifyContent="space-between" key={item.id}><Typography variant="caption">{dateTime(item.reminderAtUtc)} · {item.status}</Typography>{canManageReminder ? <button className="pm-workbench-command" onClick={() => removeReminder.mutate(item)} type="button">{t('projectManagement.editor.reminder.delete')}</button> : null}</Stack>)}
          </CollapsibleEditorGroup> : null}
        </Stack>
      </Box>
      <FilePreviewDialog error={preview.error} file={preview.file} loading={preview.loading} onClose={() => setPreview({ loading: false, open: false })} open={preview.open} previewFile={preview.previewFile} />
    </ResponsiveModal>
  );
}

function CollaborationPanel({
  activities,
  attachments,
  canManageAttachment,
  comments,
  comment,
  commentContentJson,
  commentFile,
  dateTime,
  filter,
  isPublishing,
  mentionCandidates,
  onCommentChange,
  onCommentContentJsonChange,
  onCommentFileChange,
  onCommentMentionsChange,
  onDeleteAttachment,
  onDownload,
  onFilterChange,
  onPreview,
  onPublish,
  onUpload,
  t,
}: {
  activities: Array<{ id: string; activityType: string; createdTime: string; summary?: string }>;
  attachments: ProjectManagementTaskAttachment[];
  canManageAttachment: boolean;
  comments: ProjectManagementTaskComment[];
  comment: string;
  commentContentJson?: string;
  commentFile?: File;
  dateTime: (value?: string | Date | null) => string;
  filter: CollaborationFilter;
  isPublishing: boolean;
  mentionCandidates: ProjectManagementMemberCandidate[];
  onCommentChange: (value: string) => void;
  onCommentContentJsonChange: (value: string) => void;
  onCommentFileChange: (file?: File) => void;
  onCommentMentionsChange: (value: string[]) => void;
  onDeleteAttachment: (item: { id: string; versionNo: number }) => void;
  onDownload: (item: ProjectManagementTaskAttachment) => void;
  onFilterChange: (filter: CollaborationFilter) => void;
  onPreview: (item: ProjectManagementTaskAttachment) => void;
  onPublish: () => void;
  onUpload: (file: File) => void;
  t: (key: string) => string;
}) {
  const taskAttachments = attachments.filter((item) => !item.commentId);
  return (
    <Box className="pm-collaboration">
      <Stack alignItems="center" className="pm-collaboration__header" direction="row" justifyContent="space-between">
        <Typography component="h3" fontWeight={700}>{t('projectManagement.editor.collaboration')}</Typography>
        <div className="pm-collaboration__filters">
          {(['all', 'comments', 'attachments', 'activity'] as const).map((value) => <button className={filter === value ? 'is-active' : ''} key={value} onClick={() => onFilterChange(value)} type="button">{t(`projectManagement.editor.feed.${value}`)}</button>)}
        </div>
      </Stack>
      {(filter === 'all' || filter === 'comments') ? comments.map((item) => <Box className="pm-comment" key={item.id}>
        <Typography color="text.secondary" variant="caption">{item.authorDisplayName ?? item.authorUserId} · {dateTime(item.createdTime)}</Typography>
        <ProjectManagementMarkdownContent className="pm-comment__body" mentions={item.mentions} value={item.markdown} />
        {item.attachment ? <AttachmentCard attachment={item.attachment} canDelete={false} onDownload={onDownload} onPreview={onPreview} t={t} /> : null}
      </Box>) : null}
      {(filter === 'all' || filter === 'attachments') ? <Stack spacing={0.75}>
        <Stack alignItems="center" direction="row" justifyContent="space-between">
          <Typography fontWeight={700} variant="body2">{t('projectManagement.editor.attachment')}</Typography>
          {canManageAttachment ? <label className="pm-workbench-command">{t('projectManagement.editor.upload')}<input hidden onChange={(event) => { const file = event.target.files?.[0]; if (file) onUpload(file); event.currentTarget.value = ''; }} type="file" /></label> : null}
        </Stack>
        {taskAttachments.map((item) => <AttachmentCard attachment={item} canDelete={canManageAttachment} key={item.id} onDelete={onDeleteAttachment} onDownload={onDownload} onPreview={onPreview} t={t} />)}
      </Stack> : null}
      {(filter === 'all' || filter === 'activity') ? activities.map((item) => <Box className="pm-overview-activity-row" key={item.id}><span className="pm-overview-dot" /><Typography className="pm-overview-activity-row__summary" component="span" variant="body2">{item.summary ?? item.activityType}</Typography><Typography className="pm-overview-activity-row__time" component="span" variant="caption">{dateTime(item.createdTime)}</Typography></Box>) : null}
      <Stack className="pm-comment-composer" spacing={0.75}>
        <ProjectManagementMarkdownEditor ariaLabel={t('projectManagement.editor.commentAria')} contentJson={commentContentJson} mentionCandidates={mentionCandidates} onChange={onCommentChange} onContentJsonChange={onCommentContentJsonChange} onMentionUserIdsChange={onCommentMentionsChange} placeholder={t('projectManagement.editor.commentPlaceholder')} rows={3} value={comment} />
        <Stack alignItems="center" direction="row" justifyContent="space-between">
          {canManageAttachment ? <label className="pm-workbench-command">{commentFile?.name ?? t('projectManagement.editor.attachFile')}<input hidden onChange={(event) => { onCommentFileChange(event.target.files?.[0]); event.currentTarget.value = ''; }} type="file" /></label> : <span />}
          <button className="pm-primary-button" disabled={!comment.trim() || isPublishing} onClick={onPublish} type="button">{t('projectManagement.editor.publishComment')}</button>
        </Stack>
      </Stack>
    </Box>
  );
}

function AttachmentCard({ attachment, canDelete, onDelete, onDownload, onPreview, t }: { attachment: ProjectManagementTaskAttachment; canDelete: boolean; onDelete?: (item: { id: string; versionNo: number }) => void; onDownload: (item: ProjectManagementTaskAttachment) => void; onPreview: (item: ProjectManagementTaskAttachment) => void; t: (key: string) => string }) {
  return <Stack alignItems="center" className="pm-attachment-card" direction="row" justifyContent="space-between"><Typography variant="body2">{attachment.fileName} · {Math.ceil(attachment.fileSize / 1024)} KB</Typography><Stack direction="row" spacing={0.5}>{attachment.previewSupported ? <button className="pm-workbench-command" onClick={() => onPreview(attachment)} type="button">{t('projectManagement.editor.preview')}</button> : null}<button className="pm-workbench-command" onClick={() => onDownload(attachment)} type="button">{t('projectManagement.editor.download')}</button>{canDelete && onDelete ? <button className="pm-workbench-command" onClick={() => onDelete(attachment)} type="button">{t('projectManagement.editor.deleteAttachment')}</button> : null}</Stack></Stack>;
}

function CollapsibleEditorGroup({ children, onToggle, open, title }: { children: ReactNode; onToggle: () => void; open: boolean; title: string }) {
  return <Box className="pm-editor-property-group pm-editor-property-group--collapsible"><button className="pm-editor-property-group__toggle" onClick={onToggle} type="button"><span>{title}</span><span>{open ? '−' : '+'}</span></button>{open ? <Stack spacing={0.75}>{children}</Stack> : null}</Box>;
}

function ConflictPanel({ conflict, onKeepLocal, onOverwrite, onReload, t }: { conflict: NonNullable<ReturnType<typeof readProjectManagementTaskConflict>>; onKeepLocal: () => void; onOverwrite: () => void; onReload: () => void; t: (key: string) => string }) {
  return <Box className="pm-task-conflict" role="alert"><Typography fontWeight={700}>{t('projectManagement.editor.conflictDetected')}</Typography>{conflict.fieldConflicts.map((field) => <Typography key={field.field} variant="caption">{field.displayName}: {t('projectManagement.editor.server')} {String(field.serverValue ?? '—')} / {t('projectManagement.editor.local')} {String(field.localValue ?? '—')}</Typography>)}<Stack direction="row" spacing={1}><button className="pm-workbench-command" onClick={onReload} type="button">{t('projectManagement.editor.reload')}</button><button className="pm-workbench-command" onClick={onKeepLocal} type="button">{t('projectManagement.editor.keepLocal')}</button><button className="pm-primary-button" onClick={onOverwrite} type="button">{t('projectManagement.editor.overwrite')}</button></Stack></Box>;
}

function Field({ label, value }: { label: string; value: string }) { return <Stack spacing={0.25}><Typography className="pm-editor-field-label" component="span">{label}</Typography><Typography className="pm-editor-readonly" variant="body2">{value || '—'}</Typography></Stack>; }
function SelectField({ label, labels = {}, onChange, options, value, t }: { label: string; labels?: Record<string, string>; onChange: (value: string) => void; options: string[]; value: string; t: (key: string) => string }) { return <Stack spacing={0.25}><Typography className="pm-editor-field-label" component="span">{label}</Typography><select className="pm-editor-select" onChange={(event) => onChange(event.target.value)} value={value}>{options.map((option) => <option key={option} value={option}>{option ? labels[option] ?? option : t('projectManagement.workbench.unknown')}</option>)}</select></Stack>; }
function DateField({ label, onChange, value }: { label: string; onChange: (value: string) => void; value?: string }) { return <Stack spacing={0.25}><Typography className="pm-editor-field-label" component="span">{label}</Typography><input className="pm-editor-select" onChange={(event) => onChange(event.target.value)} type="date" value={value ? value.slice(0, 10) : ''} /></Stack>; }
function NumberField({ label, onChange, value }: { label: string; onChange: (value: number | undefined) => void; value?: number }) {
  return (
    <Stack spacing={0.25}>
      <Typography className="pm-editor-field-label" component="span">{label}</Typography>
      <input className="pm-editor-select" min={0} onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))} type="number" value={value ?? ''} />
    </Stack>
  );
}
