import { Box, Stack, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';

import { addProjectManagementTaskFollower, createProjectManagementTask, createProjectManagementTaskComment, createProjectManagementTaskDraft, getProjectManagementMemberCandidates, getProjectManagementMilestones, getProjectManagementTask, getProjectManagementTaskAttachments, getProjectManagementTaskComments, getProjectManagementTaskFollowers, getProjectManagementTasks, updateProjectManagementTask, uploadProjectManagementTaskAttachment, uploadProjectManagementTaskDraftAttachment } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementTaskUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { isHttpError } from '../../../core/http/httpError';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { ProjectManagementMarkdownEditor } from '../collaboration/ProjectManagementMarkdownEditor';
import { projectManagementEnumLabel, useProjectManagementI18n } from '../projectManagementI18n';
import { readProjectManagementTaskConflict, taskDetailToForm } from '../state/projectManagementTaskDetailModel';

const blank: ProjectManagementTaskUpsertRequest = { taskCode: '', title: '', status: 'Todo', priority: 'Medium', progressPercent: 0, weight: 1, workItemType: 'Requirement', requirementType: 'Feature', requirementSource: 'ProductPlan', riskLevel: 'None', mentionUserIds: [], followerUserIds: [] };

export function ProjectWorkItemEditor({ onClose, onSaved, open, projectId, taskId }: { onClose: () => void; onSaved: () => void; open: boolean; projectId: string; taskId?: string }) {
  const { dateTime, t } = useProjectManagementI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [form, setForm] = useState(blank);
  const [baseline, setBaseline] = useState(JSON.stringify(blank));
  const [draftId, setDraftId] = useState<string>();
  const [comment, setComment] = useState('');
  const [commentMentionIds, setCommentMentionIds] = useState<string[]>([]);
  const [continueCreating, setContinueCreating] = useState(false);
  const [conflict, setConflict] = useState<ReturnType<typeof readProjectManagementTaskConflict>>();
  const task = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor', projectId, taskId], queryFn: ({ signal }) => getProjectManagementTask(taskId!, signal) });
  const members = useQuery({ enabled: open, queryKey: ['pm', 'editor-members', projectId], queryFn: ({ signal }) => getProjectManagementMemberCandidates({ projectId, pageIndex: 1, pageSize: 100 }, signal) });
  const milestones = useQuery({ enabled: open, queryKey: ['pm', 'editor-milestones', projectId], queryFn: ({ signal }) => getProjectManagementMilestones(projectId, signal) });
  const parents = useQuery({ enabled: open, queryKey: ['pm', 'editor-parents', projectId], queryFn: ({ signal }) => getProjectManagementTasks({ projectId, pageIndex: 1, pageSize: 200, viewKey: 'list', workItemType: 'Requirement', includeCompleted: true }, signal) });
  const attachments = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-attachments', taskId], queryFn: ({ signal }) => getProjectManagementTaskAttachments(taskId!, signal) });
  const comments = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-comments', taskId], queryFn: ({ signal }) => getProjectManagementTaskComments(taskId!, { pageIndex: 1, pageSize: 20 }, signal) });
  const followers = useQuery({ enabled: open && Boolean(taskId), queryKey: ['pm', 'editor-followers', taskId], queryFn: ({ signal }) => getProjectManagementTaskFollowers(taskId!, signal) });

  useEffect(() => {
    if (!open) return;
    const next = task.data?.data ? taskDetailToForm(task.data.data) : { ...blank };
    setForm(next); setBaseline(JSON.stringify(next)); setDraftId(undefined); setConflict(undefined);
  }, [open, task.data]);

  const save = useMutation({
    mutationFn: ({ overwriteVersionNo }: { overwriteVersionNo?: number } = {}) => {
      const request = overwriteVersionNo === undefined ? form : { ...form, versionNo: overwriteVersionNo };
      return taskId ? updateProjectManagementTask(taskId, request) : createProjectManagementTask(projectId, { ...request, workItemType: 'Requirement', draftId });
    },
    onSuccess: (result) => {
      const next = taskDetailToForm(result.data);
      message.success(taskId ? t('projectManagement.editor.savedUpdate') : t('projectManagement.editor.savedCreate'));
      setConflict(undefined); void queryClient.invalidateQueries({ queryKey: ['pm', 'editor'] }); onSaved();
      if (continueCreating && !taskId) { setForm({ ...blank }); setBaseline(JSON.stringify(blank)); setDraftId(undefined); return; }
      setForm(next); setBaseline(JSON.stringify(next)); onClose();
    },
    onError: (error) => {
      const parsed = readProjectManagementTaskConflict(error);
      if (parsed || (isHttpError(error) && error.status === 409)) { setConflict(parsed); message.error(t('projectManagement.editor.conflict')); return; }
      message.error(error instanceof Error ? error.message : t('projectManagement.editor.saveFailed'));
    }
  });
  const upload = useMutation({
    mutationFn: async (file: File) => {
      if (taskId) return uploadProjectManagementTaskAttachment(taskId, file);
      const draft = draftId ? { data: { id: draftId } } : await createProjectManagementTaskDraft(projectId, JSON.stringify(form));
      if (!draftId) setDraftId(draft.data.id);
      return uploadProjectManagementTaskDraftAttachment(draft.data.id, file);
    },
    onSuccess: () => { void attachments.refetch(); message.success(t('projectManagement.editor.uploaded')); },
    onError: () => message.error(t('projectManagement.editor.uploadFailed'))
  });
  const addComment = useMutation({ mutationFn: () => createProjectManagementTaskComment(taskId!, { markdown: comment, mentionUserIds: commentMentionIds }), onSuccess: () => { setComment(''); setCommentMentionIds([]); void comments.refetch(); message.success(t('projectManagement.editor.commentPublished')); } });
  const follow = useMutation({ mutationFn: (userId: string) => taskId ? addProjectManagementTaskFollower(taskId, { userId }) : Promise.resolve(undefined), onSuccess: () => { if (taskId) void followers.refetch(); } });
  const dirty = JSON.stringify(form) !== baseline || Boolean(draftId);
  const update = (patch: Partial<ProjectManagementTaskUpsertRequest>) => setForm((current) => ({ ...current, ...patch }));
  const requestClose = () => { if (!dirty) { onClose(); return; } confirm({ title: t('projectManagement.editor.closeTitle'), content: t('projectManagement.editor.closeDescription'), confirmText: t('projectManagement.editor.discard'), onConfirm: onClose }); };
  const candidateItems = useMemo(() => members.data?.data.items.filter((item) => item.isSelectable) ?? [], [members.data?.data.items]);
  const parentItems = parents.data?.data.items.filter((item) => item.id !== taskId) ?? [];
  const milestoneItems = milestones.data?.data.items ?? [];
  const followerIds = followers.data?.data.map((item) => item.userId) ?? form.followerUserIds ?? [];
  const labels = useMemo(() => Object.fromEntries(candidateItems.map((item) => [item.userId, item.displayName])), [candidateItems]);
  const enumLabels = (group: 'priority' | 'status' | 'workItemType' | 'risk' | 'requirementType' | 'requirementSource', values: string[]) => Object.fromEntries(values.map((value) => [value, projectManagementEnumLabel(t, group, value)]));
  const footer = <Stack alignItems="center" direction="row" justifyContent="space-between"><label className="pm-editor-checkbox"><input checked={continueCreating} onChange={(event) => setContinueCreating(event.target.checked)} type="checkbox" /> {t('projectManagement.editor.continueCreating')}</label><Stack direction="row" spacing={1}><button className="pm-workbench-command" onClick={requestClose} type="button">{t('projectManagement.editor.cancel')}</button><button className="pm-primary-button" disabled={!form.title.trim() || save.isPending} onClick={() => save.mutate({})} type="button">{t('projectManagement.editor.save')}</button></Stack></Stack>;

  return <ResponsiveModal bodyClassName="pm-work-item-editor-body" className="pm-work-item-editor" footer={footer} maxWidth="96vw" mode="modal" onClose={requestClose} open={open} title={taskId ? t('projectManagement.editor.edit') : t('projectManagement.editor.create')}>
    {conflict ? <ConflictPanel conflict={conflict} onKeepLocal={() => setConflict(undefined)} onOverwrite={() => save.mutate({ overwriteVersionNo: conflict.serverValues.versionNo })} onReload={() => { const next = taskDetailToForm(conflict.serverValues); setForm(next); setBaseline(JSON.stringify(next)); setConflict(undefined); }} t={t} /> : null}
    <Box className="pm-editor-grid"><Stack className="pm-editor-main" spacing={1.25}>
      <input aria-label={t('projectManagement.editor.titleAria')} className="pm-editor-title" maxLength={256} onChange={(event) => update({ title: event.target.value })} placeholder={t('projectManagement.editor.titlePlaceholder')} value={form.title} />
      <Typography color="text.secondary" variant="caption">{form.title.length}/256</Typography>
      <ProjectManagementMarkdownEditor ariaLabel={t('projectManagement.editor.descriptionAria')} contentJson={form.contentJson} mentionCandidates={candidateItems} onChange={(value) => update({ description: value, markdown: value })} onContentJsonChange={(value) => update({ contentJson: value })} onMentionUserIdsChange={(value) => update({ mentionUserIds: value })} placeholder={t('projectManagement.editor.descriptionPlaceholder')} rows={14} value={form.description ?? ''} />
      <Stack className="pm-editor-collaboration" spacing={1}>
        <Stack alignItems="center" direction="row" justifyContent="space-between"><Typography fontWeight={700}>{t('projectManagement.editor.attachment')}</Typography><label className="pm-workbench-command">{t('projectManagement.editor.upload')}<input hidden onChange={(event) => { const file = event.target.files?.[0]; if (file) upload.mutate(file); event.currentTarget.value = ''; }} type="file" /></label></Stack>
        {attachments.data?.data.map((item) => <Typography color="text.secondary" key={item.id} variant="caption">{item.fileName} · {Math.ceil(item.fileSize / 1024)} KB</Typography>)}
        {taskId ? <><Typography fontWeight={700}>{t('projectManagement.editor.comment')}</Typography>{comments.data?.data.items.map((item) => <Box className="pm-comment" key={item.id}><Typography color="text.secondary" variant="caption">{item.authorDisplayName ?? item.authorUserId} · {dateTime(item.createdTime)}</Typography><Typography variant="body2">{item.markdown}</Typography></Box>)}<ProjectManagementMarkdownEditor ariaLabel={t('projectManagement.editor.commentAria')} mentionCandidates={candidateItems} onChange={setComment} onMentionUserIdsChange={setCommentMentionIds} placeholder={t('projectManagement.editor.commentPlaceholder')} rows={4} value={comment} /><button className="pm-workbench-command" disabled={!comment.trim() || addComment.isPending} onClick={() => addComment.mutate()} type="button">{t('projectManagement.editor.publishComment')}</button></> : <Typography color="text.secondary" variant="caption">{t('projectManagement.editor.afterSaveHint')}</Typography>}
      </Stack>
    </Stack><Stack className="pm-editor-properties" spacing={1.1}>
      <Field label={t('projectManagement.editor.field.project')} value={t('projectManagement.editor.currentProject')} />
      <SelectField label={t('projectManagement.editor.field.workItemType')} labels={enumLabels('workItemType', ['Requirement', 'UserStory', 'Task', 'Bug'])} onChange={(value) => update({ workItemType: value })} options={['Requirement', 'UserStory', 'Task', 'Bug']} value={form.workItemType ?? 'Requirement'} t={t} />
      <SelectField label={t('projectManagement.editor.field.status')} labels={enumLabels('status', ['Todo', 'InProgress', 'Blocked', 'Done', 'Closed'])} onChange={(value) => update({ status: value })} options={['Todo', 'InProgress', 'Blocked', 'Done', 'Closed']} value={form.status ?? 'Todo'} t={t} />
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
    </Stack></Box>
  </ResponsiveModal>;
}

function ConflictPanel({ conflict, onKeepLocal, onOverwrite, onReload, t }: { conflict: NonNullable<ReturnType<typeof readProjectManagementTaskConflict>>; onKeepLocal: () => void; onOverwrite: () => void; onReload: () => void; t: (key: string) => string }) {
  return <Box className="pm-task-conflict" role="alert"><Typography fontWeight={700}>{t('projectManagement.editor.conflictDetected')}</Typography>{conflict.fieldConflicts.map((field) => <Typography key={field.field} variant="caption">{field.displayName}: {t('projectManagement.editor.server')} {String(field.serverValue ?? '—')} / {t('projectManagement.editor.local')} {String(field.localValue ?? '—')}</Typography>)}<Stack direction="row" spacing={1}><button className="pm-workbench-command" onClick={onReload} type="button">{t('projectManagement.editor.reload')}</button><button className="pm-workbench-command" onClick={onKeepLocal} type="button">{t('projectManagement.editor.keepLocal')}</button><button className="pm-primary-button" onClick={onOverwrite} type="button">{t('projectManagement.editor.overwrite')}</button></Stack></Box>;
}

function Field({ label, value }: { label: string; value: string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><Typography className="pm-editor-readonly" variant="body2">{value || '—'}</Typography></Stack>; }
function SelectField({ label, labels = {}, onChange, options, value, t }: { label: string; labels?: Record<string, string>; onChange: (value: string) => void; options: string[]; value: string; t: (key: string) => string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><select className="pm-editor-select" onChange={(event) => onChange(event.target.value)} value={value}>{options.map((option) => <option key={option} value={option}>{option ? labels[option] ?? option : t('projectManagement.workbench.unknown')}</option>)}</select></Stack>; }
function DateField({ label, onChange, value }: { label: string; onChange: (value: string) => void; value?: string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><input className="pm-editor-select" onChange={(event) => onChange(event.target.value)} type="date" value={value ? value.slice(0, 10) : ''} /></Stack>; }
