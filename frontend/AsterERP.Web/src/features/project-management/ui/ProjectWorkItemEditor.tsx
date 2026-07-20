import { Box, Stack, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode, type RefObject } from 'react';

import {
  addProjectManagementTaskParticipant,
  cancelProjectManagementTaskReminder,
  createProjectManagementTask,
  createProjectManagementTaskComment,
  createProjectManagementTaskReminders,
  createProjectManagementTaskTimeLog,
  deleteProjectManagementTaskReminder,
  deleteProjectManagementTaskTimeLog,
  downloadProjectManagementTaskAttachment,
  getProjectManagementMemberCandidates,
  getProjectManagementMilestones,
  getProjectManagementTask,
  getProjectManagementTaskComments,
  getProjectManagementTaskParticipantCandidates,
  getProjectManagementTaskParticipants,
  getProjectManagementTaskReminders,
  getProjectManagementTasks,
  getProjectManagementTaskTimeLogs,
  previewProjectManagementTaskAttachment,
  removeProjectManagementTaskParticipant,
  updateProjectManagementTask,
  uploadProjectManagementTaskAttachment,
} from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementMemberCandidate, ProjectManagementTaskAttachment, ProjectManagementTaskComment, ProjectManagementTaskParticipant, ProjectManagementTaskUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { isHttpError } from '../../../core/http/httpError';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { FilePreviewDialog } from '../../../shared/file-preview/FilePreviewDialog';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { ProjectManagementMarkdownEditor, ProjectManagementMarkdownContent } from '../collaboration/ProjectManagementMarkdownEditor';
import { ProjectManagementCountdown } from '../components/ProjectManagementCountdown';
import { projectManagementEnumLabel, useProjectManagementI18n } from '../projectManagementI18n';
import { getAllowedProjectManagementTaskStatuses } from '../state/projectManagementStatusTransitions';
import { readProjectManagementTaskConflict, taskDetailToForm } from '../state/projectManagementTaskDetailModel';

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
  focusComments = false,
  onClose,
  onSaved,
  open,
  projectId,
  taskId,
}: {
  initialStartDate?: string;
  focusComments?: boolean;
  onClose: () => void;
  onSaved: () => void;
  open: boolean;
  projectId: string;
  taskId?: string;
}) {
  const { commentDateTime, dateTime, format, t } = useProjectManagementI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const { hasPermission: canEditTask } = usePermission('project-management:task:edit');
  const { hasPermission: canAddComment } = usePermission('project-management:comment:add');
  const { hasPermission: canAssignTask } = usePermission('project-management:task:assign');
  const { hasPermission: canShareTask } = usePermission('project-management:task:share');
  const { hasPermission: canManageAttachment } = usePermission('project-management:attachment:manage');
  const { hasPermission: canManageReminder } = usePermission('project-management:reminder:manage');
  const { hasPermission: canViewReminder } = usePermission('project-management:reminder:view');
  const [remindersOpen, setRemindersOpen] = useState(false);
  const [timeLogsOpen, setTimeLogsOpen] = useState(false);
  const [form, setForm] = useState(blank);
  const [baseline, setBaseline] = useState(JSON.stringify(blank));
  const [draftId, setDraftId] = useState<string>();
  const [comment, setComment] = useState('');
  const [commentContentJson, setCommentContentJson] = useState<string>();
  const [commentMentionIds, setCommentMentionIds] = useState<string[]>([]);
  const [allowMentionedComment, setAllowMentionedComment] = useState(false);
  const [commentFiles, setCommentFiles] = useState<File[]>([]);
  const [continueCreating, setContinueCreating] = useState(false);
  const [participantPicker, setParticipantPicker] = useState('');
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
  const comments = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-comments', taskId], queryFn: ({ signal }) => getProjectManagementTaskComments(taskId!, { pageIndex: 1, pageSize: 20, sort: 'desc' }, signal) });
  const participants = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-participants', taskId], queryFn: ({ signal }) => getProjectManagementTaskParticipants(taskId!, signal) });
  const participantCandidates = useQuery({
    enabled: open && Boolean(taskId),
    queryKey: ['pm', 'editor-participant-candidates', taskId],
    queryFn: ({ signal }) => getProjectManagementTaskParticipantCandidates(taskId!, { pageIndex: 1, pageSize: 100 }, signal),
  });
  const reminders = useQuery({ enabled: open && Boolean(taskId) && canViewReminder && remindersOpen, queryKey: ['pm', 'editor-reminders', taskId], queryFn: ({ signal }) => getProjectManagementTaskReminders(taskId!, signal) });
  const timeLogs = useQuery({ enabled: open && Boolean(taskId) && timeLogsOpen, queryKey: ['pm', 'editor-time-logs', taskId], queryFn: ({ signal }) => getProjectManagementTaskTimeLogs(taskId!, signal) });

  useEffect(() => {
    if (!open) return;
    const next = task.data?.data
      ? taskDetailToForm(task.data.data)
      : { ...blank, startDate: initialStartDate, dueDate: initialStartDate };
    setForm(next);
    setBaseline(JSON.stringify(next));
    setDraftId(undefined);
    setConflict(undefined);
    setCommentFiles([]);
    setParticipantPicker('');
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

  const addComment = useMutation({
    mutationFn: async () => {
      const uploaded = commentFiles.length > 0
        ? await Promise.all(commentFiles.map((file) => uploadProjectManagementTaskAttachment(taskId!, file)))
        : [];
      const attachmentIds = uploaded.map((item) => item.data.id);
      return createProjectManagementTaskComment(taskId!, {
        markdown: comment,
        mentionUserIds: commentMentionIds,
        attachmentIds: attachmentIds.length > 0 ? attachmentIds : undefined,
        allowMentionedComment,
      });
    },
    onSuccess: () => {
      setComment('');
      setCommentContentJson(undefined);
      setCommentMentionIds([]);
      setAllowMentionedComment(false);
      setCommentFiles([]);
      void comments.refetch();
      message.success(t('projectManagement.editor.commentPublished'));
    },
    onError: (error) => message.error(error instanceof Error ? error.message : t('projectManagement.editor.commentPublishFailed')),
  });

  const resolveTaskVersionNo = useCallback(() => {
    const cached = queryClient.getQueryData<{ data: { versionNo: number } }>(['pm', 'editor', projectId, taskId]);
    return cached?.data.versionNo ?? task.data?.data.versionNo ?? form.versionNo ?? 0;
  }, [form.versionNo, projectId, queryClient, task.data?.data.versionNo, taskId]);

  const addParticipant = useMutation({
    mutationFn: (userId: string) => {
      if (!taskId) return Promise.reject(new Error(t('projectManagement.editor.saveFirst')));
      return addProjectManagementTaskParticipant(taskId, {
        userId,
        roleCode: 'Participant',
        versionNo: resolveTaskVersionNo(),
      });
    },
    onSuccess: async () => {
      setParticipantPicker('');
      await Promise.all([participants.refetch(), task.refetch()]);
      message.success(t('projectManagement.editor.participantAdded'));
    },
    onError: (error) => message.error(error instanceof Error ? error.message : t('projectManagement.editor.participantFailed')),
  });

  const removeParticipant = useMutation({
    mutationFn: (participant: ProjectManagementTaskParticipant) => {
      if (!taskId) return Promise.reject(new Error(t('projectManagement.editor.saveFirst')));
      return removeProjectManagementTaskParticipant(taskId, participant.id, resolveTaskVersionNo());
    },
    onSuccess: async () => {
      await Promise.all([participants.refetch(), task.refetch()]);
      message.success(t('projectManagement.editor.participantRemoved'));
    },
    onError: (error) => message.error(error instanceof Error ? error.message : t('projectManagement.editor.participantFailed')),
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
  const canEditThisTask = !taskId || (canEditTask && (task.data?.data.access?.canEdit ?? true));
  const canCommentThisTask = Boolean(taskId && canAddComment && (task.data?.data.access?.canComment ?? canEditThisTask));
  const isReadOnlyGrant = Boolean(taskId && task.data?.data.access?.isReadOnlyGrant);
  const collaborationRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open || !focusComments || !taskId) return;
    const frame = window.requestAnimationFrame(() => collaborationRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    return () => window.cancelAnimationFrame(frame);
  }, [focusComments, open, taskId]);
  const participantItems = participants.data?.data ?? [];
  const participantCandidateItems = participantCandidates.data?.data.items ?? [];
  const participantLabels = useMemo(
    () => Object.fromEntries(participantCandidateItems.map((item) => [item.userId, item.displayName || item.userName])),
    [participantCandidateItems],
  );
  const participantIds = useMemo(() => new Set(participantItems.map((item) => item.userId)), [participantItems]);
  const availableParticipantCandidates = useMemo(
    () => participantCandidateItems.filter((item) => !participantIds.has(item.userId)),
    [participantCandidateItems, participantIds],
  );
  const parentItems = parents.data?.data.items.filter((item) => item.id !== taskId) ?? [];
  const milestoneItems = milestones.data?.data.items ?? [];
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
    <Stack alignItems="center" className="pm-editor-footer" direction="row" justifyContent="space-between" width="100%">
      <label className="pm-editor-checkbox"><input checked={continueCreating} onChange={(event) => setContinueCreating(event.target.checked)} type="checkbox" /> {t('projectManagement.editor.continueCreating')}</label>
      <Stack className="pm-editor-footer-actions" direction="row" spacing={2}>
        <button className="pm-workbench-command" onClick={requestClose} type="button">{t('projectManagement.editor.cancel')}</button>
        <button className="pm-primary-button" disabled={!form.title.trim() || save.isPending || !canEditThisTask} onClick={() => save.mutate({})} type="button">{t('projectManagement.editor.save')}</button>
      </Stack>
    </Stack>
  );

  return (
    <ResponsiveModal bodyClassName="pm-work-item-editor-body" className="pm-work-item-editor" closeOnEscape={!preview.open} footer={footer} maxWidth="96vw" mode="modal" onClose={requestClose} open={open} title={taskId ? t('projectManagement.editor.edit') : t('projectManagement.editor.create')}>
      {conflict ? <ConflictPanel conflict={conflict} onKeepLocal={() => setConflict(undefined)} onOverwrite={() => save.mutate({ overwriteVersionNo: conflict.serverValues.versionNo })} onReload={() => { const next = taskDetailToForm(conflict.serverValues); setForm(next); setBaseline(JSON.stringify(next)); setConflict(undefined); }} t={t} /> : null}
      <Box className="pm-editor-grid">
        <Box className="pm-editor-main">
          <div className="pm-editor-title-field">
            <input aria-label={t('projectManagement.editor.titleAria')} className="pm-editor-title" disabled={!canEditThisTask} maxLength={256} onChange={(event) => update({ title: event.target.value })} placeholder={t('projectManagement.editor.titlePlaceholder')} value={form.title} />
            <span aria-live="polite" className={`pm-editor-counter${form.title.length >= 256 ? ' is-at-limit' : form.title.length >= 230 ? ' is-near-limit' : ''}`}>{form.title.length}/256</span>
          </div>
          {isReadOnlyGrant ? <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.sharedReadOnly')}</Typography> : null}
          {taskId ? <CollaborationPanel
            canManageAttachment={canManageAttachment && canEditThisTask}
            canComment={canCommentThisTask}
            canShareTask={canShareTask && canEditThisTask}
            commentDateTime={commentDateTime}
            comments={comments.data?.data.items ?? []}
            comment={comment}
            commentContentJson={commentContentJson}
            commentFiles={commentFiles}
            allowMentionedComment={allowMentionedComment}
            format={format}
            isPublishing={addComment.isPending}
            onCommentChange={setComment}
            onCommentContentJsonChange={setCommentContentJson}
            onCommentFilesChange={setCommentFiles}
            onCommentMentionsChange={setCommentMentionIds}
            onAllowMentionedCommentChange={setAllowMentionedComment}
            onDownload={(item) => void downloadAttachment(item)}
            onPreview={(item) => void previewAttachment(item)}
            onPublish={() => addComment.mutate()}
            mentionCandidates={candidateItems}
            onMentionSearch={async (keyword) => (await getProjectManagementMemberCandidates({ keyword, pageIndex: 1, pageSize: 20 })).data.items}
            collaborationRef={collaborationRef}
            t={t}
          /> : <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.saveFirst')}</Typography>}
        </Box>
        <fieldset className="pm-editor-properties" disabled={!canEditThisTask} style={{ border: 0, margin: 0, minWidth: 0, padding: 0 }}>
        <Stack spacing={0}>
          <Box className="pm-editor-property-group">
            <Typography className="pm-editor-property-group__title" component="h3">{t('projectManagement.editor.group.basic')}</Typography>
            <SelectField label={t('projectManagement.editor.field.workItemType')} labels={enumLabels('workItemType', ['Requirement', 'UserStory', 'Task', 'Bug'])} onChange={(value) => update({ workItemType: value })} options={['Requirement', 'UserStory', 'Task', 'Bug']} value={form.workItemType ?? 'Requirement'} t={t} />
            <SelectField label={t('projectManagement.editor.field.status')} labels={enumLabels('status', statusOptions)} onChange={(value) => update({ status: value })} options={statusOptions} value={form.status ?? 'Todo'} t={t} />
            <Stack spacing={0.25}>
              <Typography className="pm-editor-field-label" component="span">{t('projectManagement.editor.field.progress')}</Typography>
              <Stack alignItems="center" direction="row" spacing={1}>
                <input max={100} min={0} onChange={(event) => update({ progressPercent: Number(event.target.value) })} style={{ flex: 1 }} type="range" value={form.progressPercent ?? 0} />
                <Typography variant="caption">{form.progressPercent ?? 0}%</Typography>
              </Stack>
            </Stack>
            <SelectField label={t('projectManagement.editor.field.assignee')} labels={labels} onChange={(value) => update({ assigneeUserId: value || undefined })} options={['', ...candidateItems.map((item) => item.userId)]} value={form.assigneeUserId ?? ''} t={t} />
            <ParticipantsField
              canManage={Boolean(taskId) && canAssignTask}
              candidates={availableParticipantCandidates}
              isAdding={addParticipant.isPending}
              labels={participantLabels}
              onAdd={(userId) => addParticipant.mutate(userId)}
              onRemove={(participant) => removeParticipant.mutate(participant)}
              participants={participantItems}
              pickerValue={participantPicker}
              onPickerChange={setParticipantPicker}
              showSaveHint={!taskId}
              t={t}
            />
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
        </fieldset>
      </Box>
      <FilePreviewDialog error={preview.error} file={preview.file} loading={preview.loading} onClose={() => setPreview({ loading: false, open: false })} open={preview.open} previewFile={preview.previewFile} />
    </ResponsiveModal>
  );
}

function CollaborationPanel({
  allowMentionedComment,
  canComment,
  canManageAttachment,
  canShareTask,
  commentDateTime,
  comments,
  comment,
  commentContentJson,
  commentFiles,
  collaborationRef,
  format,
  isPublishing,
  mentionCandidates,
  onCommentChange,
  onCommentContentJsonChange,
  onCommentFilesChange,
  onAllowMentionedCommentChange,
  onCommentMentionsChange,
  onDownload,
  onPreview,
  onPublish,
  onMentionSearch,
  t,
}: {
  allowMentionedComment: boolean;
  canComment: boolean;
  canManageAttachment: boolean;
  canShareTask: boolean;
  commentDateTime: (value?: string | Date | null) => string;
  comments: ProjectManagementTaskComment[];
  comment: string;
  commentContentJson?: string;
  commentFiles: File[];
  collaborationRef: RefObject<HTMLDivElement | null>;
  format: (key: string, values?: Record<string, string | number>) => string;
  isPublishing: boolean;
  mentionCandidates: ProjectManagementMemberCandidate[];
  onCommentChange: (value: string) => void;
  onCommentContentJsonChange: (value: string) => void;
  onCommentFilesChange: (files: File[]) => void;
  onAllowMentionedCommentChange: (value: boolean) => void;
  onCommentMentionsChange: (value: string[]) => void;
  onDownload: (item: ProjectManagementTaskAttachment) => void;
  onPreview: (item: ProjectManagementTaskAttachment) => void;
  onPublish: () => void;
  onMentionSearch: (keyword: string) => Promise<ProjectManagementMemberCandidate[]>;
  t: (key: string) => string;
}) {
  const appendCommentFiles = (files: FileList | null | undefined) => {
    if (!files?.length) return;
    onCommentFilesChange(mergeCommentFiles(commentFiles, Array.from(files)));
  };

  const removeCommentFile = (index: number) => {
    onCommentFilesChange(commentFiles.filter((_, fileIndex) => fileIndex !== index));
  };

  return (
    <Box className="pm-collaboration" ref={collaborationRef}>
      <Typography className="pm-collaboration__title" component="h3" fontWeight={700}>{t('projectManagement.editor.comment')}</Typography>
      {canComment ? <Stack className="pm-comment-composer" spacing={0.75}>
        <ProjectManagementMarkdownEditor ariaLabel={t('projectManagement.editor.commentAria')} contentJson={commentContentJson} density="compact" mentionCandidates={mentionCandidates} onChange={onCommentChange} onContentJsonChange={onCommentContentJsonChange} onMentionSearch={onMentionSearch} onMentionUserIdsChange={onCommentMentionsChange} placeholder={t('projectManagement.editor.commentPlaceholder')} rows={2} showToolbar={false} value={comment} />
        {canShareTask ? (
          <label className="pm-editor-checkbox">
            <input checked={allowMentionedComment} onChange={(event) => onAllowMentionedCommentChange(event.target.checked)} type="checkbox" />
            {t('projectManagement.editor.allowMentionedComment')}
          </label>
        ) : null}
        {commentFiles.length > 0 ? (
          <div className="pm-comment-composer__pending-files">
            {commentFiles.map((file, index) => (
              <AttachmentChip
                file={file}
                key={`${file.name}-${file.size}-${index}`}
                onRemove={() => removeCommentFile(index)}
                pending
                t={t}
              />
            ))}
          </div>
        ) : null}
        <Stack alignItems="center" className="pm-comment-composer__actions" direction="row" justifyContent="space-between" width="100%">
          {canManageAttachment ? (
            <label className="pm-workbench-command">
              {commentFiles.length > 0 ? t('projectManagement.editor.attachFile') : t('projectManagement.editor.attachFile')}
              <input hidden multiple onChange={(event) => { appendCommentFiles(event.target.files); event.currentTarget.value = ''; }} type="file" />
            </label>
          ) : <span />}
          <button className="pm-primary-button" disabled={!comment.trim() || isPublishing} onClick={onPublish} type="button">{t('projectManagement.editor.publishComment')}</button>
        </Stack>
      </Stack> : null}
      {!canComment ? <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.commentReadOnly')}</Typography> : null}
      <Box className="pm-comment-feed">
        {comments.length === 0 ? <Typography className="pm-comment-empty" color="text.secondary" variant="body2">{t('projectManagement.editor.comment.empty')}</Typography> : null}
        {comments.map((item) => {
          const authorName = item.authorDisplayName ?? item.authorUserId;
          const attachments = item.attachments ?? [];
          return (
            <Box className="pm-comment" key={item.id}>
              <Stack alignItems="flex-start" className="pm-comment__header" direction="row" spacing={1}>
                <CommentAvatar name={authorName} />
                <Stack className="pm-comment__meta" spacing={0.25}>
                  <Typography className="pm-comment__author" fontWeight={650} variant="body2">{authorName}</Typography>
                  <Typography className="pm-comment__time" color="text.secondary" component="time" dateTime={item.createdTime} variant="caption">
                    {commentDateTime(item.createdTime)}
                    {item.editedTime ? ` · ${format('projectManagement.editor.comment.editedAt', { time: commentDateTime(item.editedTime) })}` : ''}
                  </Typography>
                </Stack>
              </Stack>
              <ProjectManagementMarkdownContent className="pm-comment__body" mentions={item.mentions} value={item.markdown} />
              {attachments.length > 0 ? (
                <div className="pm-comment__attachments">
                  {attachments.map((attachment) => (
                    <AttachmentChip
                      attachment={attachment}
                      key={attachment.id}
                      onDownload={onDownload}
                      onPreview={onPreview}
                      t={t}
                    />
                  ))}
                </div>
              ) : null}
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}

const AVATAR_PALETTE = ['#2563eb', '#7c3aed', '#0891b2', '#059669', '#d97706', '#dc2626', '#4f46e5', '#0d9488'];

function CommentAvatar({ name }: { name: string }) {
  const initial = (name.trim()[0] ?? '?').toUpperCase();
  const colorIndex = Math.abs(Array.from(name).reduce((sum, char) => sum + char.charCodeAt(0), 0)) % AVATAR_PALETTE.length;
  return <span aria-hidden className="pm-comment__avatar" style={{ backgroundColor: AVATAR_PALETTE[colorIndex] }}>{initial}</span>;
}

function mergeCommentFiles(existing: File[], incoming: File[]) {
  const merged = [...existing];
  incoming.forEach((file) => {
    if (!merged.some((item) => item.name === file.name && item.size === file.size)) merged.push(file);
  });
  return merged;
}

function formatAttachmentSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.ceil(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function resolveAttachmentKind(fileName: string) {
  const extension = fileName.split('.').pop()?.toLowerCase() ?? '';
  if (['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg', 'bmp'].includes(extension)) return { label: 'IMG', tone: 'image' as const };
  if (extension === 'pdf') return { label: 'PDF', tone: 'pdf' as const };
  if (['doc', 'docx', 'txt', 'md', 'rtf'].includes(extension)) return { label: 'DOC', tone: 'doc' as const };
  if (['xls', 'xlsx', 'csv'].includes(extension)) return { label: 'XLS', tone: 'sheet' as const };
  if (['zip', 'rar', '7z', 'tar', 'gz'].includes(extension)) return { label: 'ZIP', tone: 'archive' as const };
  return { label: extension.slice(0, 3).toUpperCase() || 'FILE', tone: 'other' as const };
}

function AttachmentChip({
  attachment,
  file,
  onDownload,
  onPreview,
  onRemove,
  pending = false,
  t,
}: {
  attachment?: ProjectManagementTaskAttachment;
  file?: File;
  onDownload?: (item: ProjectManagementTaskAttachment) => void;
  onPreview?: (item: ProjectManagementTaskAttachment) => void;
  onRemove?: () => void;
  pending?: boolean;
  t: (key: string) => string;
}) {
  const fileName = attachment?.fileName ?? file?.name ?? '';
  const fileSize = attachment?.fileSize ?? file?.size ?? 0;
  const kind = resolveAttachmentKind(fileName);
  return (
    <div className={`pm-attachment-chip${pending ? ' pm-attachment-chip--pending' : ''}`}>
      <span className={`pm-attachment-chip__icon pm-attachment-chip__icon--${kind.tone}`}>{kind.label}</span>
      <div className="pm-attachment-chip__meta">
        <span className="pm-attachment-chip__name" title={fileName}>{fileName}</span>
        <span className="pm-attachment-chip__size">{formatAttachmentSize(fileSize)}</span>
      </div>
      <div className="pm-attachment-chip__actions">
        {pending && onRemove ? <button className="pm-workbench-command" onClick={onRemove} type="button">{t('projectManagement.editor.removeFile')}</button> : null}
        {!pending && attachment && attachment.previewSupported && onPreview ? <button className="pm-workbench-command" onClick={() => onPreview(attachment)} type="button">{t('projectManagement.editor.preview')}</button> : null}
        {!pending && attachment && onDownload ? <button className="pm-workbench-command" onClick={() => onDownload(attachment)} type="button">{t('projectManagement.editor.download')}</button> : null}
      </div>
    </div>
  );
}

function CollapsibleEditorGroup({ children, onToggle, open, title }: { children: ReactNode; onToggle: () => void; open: boolean; title: string }) {
  return <Box className="pm-editor-property-group pm-editor-property-group--collapsible"><button className="pm-editor-property-group__toggle" onClick={onToggle} type="button"><span>{title}</span><span>{open ? '−' : '+'}</span></button>{open ? <Stack spacing={0.75}>{children}</Stack> : null}</Box>;
}

function ConflictPanel({ conflict, onKeepLocal, onOverwrite, onReload, t }: { conflict: NonNullable<ReturnType<typeof readProjectManagementTaskConflict>>; onKeepLocal: () => void; onOverwrite: () => void; onReload: () => void; t: (key: string) => string }) {
  return <Box className="pm-task-conflict" role="alert"><Typography fontWeight={700}>{t('projectManagement.editor.conflictDetected')}</Typography>{conflict.fieldConflicts.map((field) => <Typography key={field.field} variant="caption">{field.displayName}: {t('projectManagement.editor.server')} {String(field.serverValue ?? '—')} / {t('projectManagement.editor.local')} {String(field.localValue ?? '—')}</Typography>)}<Stack direction="row" spacing={1}><button className="pm-workbench-command" onClick={onReload} type="button">{t('projectManagement.editor.reload')}</button><button className="pm-workbench-command" onClick={onKeepLocal} type="button">{t('projectManagement.editor.keepLocal')}</button><button className="pm-primary-button" onClick={onOverwrite} type="button">{t('projectManagement.editor.overwrite')}</button></Stack></Box>;
}

function Field({ label, value }: { label: string; value: string }) { return <Stack spacing={0.25}><Typography className="pm-editor-field-label" component="span">{label}</Typography><Typography className="pm-editor-readonly" variant="body2">{value || '—'}</Typography></Stack>; }

function ParticipantsField({
  canManage,
  candidates,
  isAdding,
  labels,
  onAdd,
  onPickerChange,
  onRemove,
  participants,
  pickerValue,
  showSaveHint,
  t,
}: {
  canManage: boolean;
  candidates: Array<{ displayName: string; userId: string; userName: string }>;
  isAdding: boolean;
  labels: Record<string, string>;
  onAdd: (userId: string) => void;
  onPickerChange: (value: string) => void;
  onRemove: (participant: ProjectManagementTaskParticipant) => void;
  participants: ProjectManagementTaskParticipant[];
  pickerValue: string;
  showSaveHint: boolean;
  t: (key: string) => string;
}) {
  return (
    <Stack spacing={0.5}>
      <Typography className="pm-editor-field-label" component="span">{t('projectManagement.editor.field.followers')}</Typography>
      {participants.length > 0 ? (
        <Stack className="pm-editor-participant-list" direction="row" flexWrap="wrap" useFlexGap spacing={0.5}>
          {participants.map((participant) => (
            <span className="pm-editor-participant-chip" key={participant.id}>
              <span>{labels[participant.userId] ?? participant.userId}</span>
              {canManage ? (
                <button aria-label={t('projectManagement.editor.participantRemoved')} className="pm-editor-participant-chip__remove" onClick={() => onRemove(participant)} type="button">×</button>
              ) : null}
            </span>
          ))}
        </Stack>
      ) : (
        <Typography className="pm-editor-readonly" variant="body2">—</Typography>
      )}
      {showSaveHint ? <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.afterSaveHint')}</Typography> : null}
      {canManage ? (
        <select
          className="pm-editor-select"
          disabled={isAdding || candidates.length === 0}
          onChange={(event) => {
            const value = event.target.value;
            onPickerChange(value);
            if (!value) return;
            onAdd(value);
            onPickerChange('');
          }}
          value={pickerValue}
        >
          <option value="">{t('projectManagement.editor.addParticipant')}</option>
          {candidates.map((candidate) => (
            <option key={candidate.userId} value={candidate.userId}>{candidate.displayName || candidate.userName}</option>
          ))}
        </select>
      ) : null}
    </Stack>
  );
}

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
