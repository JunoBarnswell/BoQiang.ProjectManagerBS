import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Stack as MuiStack, Typography as MuiTypography } from '@mui/material';
import { useEffect, useState } from 'react';

import { addProjectManagementTaskFollower, createProjectManagementTask, createProjectManagementTaskComment, createProjectManagementTaskDraft, getProjectManagementMemberCandidates, getProjectManagementMilestones, getProjectManagementTask, getProjectManagementTaskAttachments, getProjectManagementTaskComments, getProjectManagementTaskFollowers, getProjectManagementTasks, updateProjectManagementTask, uploadProjectManagementTaskAttachment, uploadProjectManagementTaskDraftAttachment } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementTaskUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { isHttpError } from '../../../core/http/httpError';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { ProjectManagementMarkdownEditor } from '../collaboration/ProjectManagementMarkdownEditor';
import { readProjectManagementTaskConflict, taskDetailToForm } from '../state/projectManagementTaskDetailModel';

const Stack = MuiStack as any;
const Typography = MuiTypography as any;
const blank: ProjectManagementTaskUpsertRequest = { taskCode: '', title: '', status: 'Todo', priority: 'Medium', progressPercent: 0, weight: 1, workItemType: 'Requirement', requirementType: 'Feature', requirementSource: 'ProductPlan', riskLevel: 'None', mentionUserIds: [], followerUserIds: [] };

export function ProjectWorkItemEditor({ onClose, onSaved, open, projectId, taskId }: { onClose: () => void; onSaved: () => void; open: boolean; projectId: string; taskId?: string }) {
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
    setForm(next);
    setBaseline(JSON.stringify(next));
    setDraftId(undefined);
    setConflict(undefined);
  }, [open, task.data]);

  const save = useMutation({
    mutationFn: ({ overwriteVersionNo }: { overwriteVersionNo?: number } = {}) => {
      const request = overwriteVersionNo === undefined ? form : { ...form, versionNo: overwriteVersionNo };
      return taskId ? updateProjectManagementTask(taskId, request) : createProjectManagementTask(projectId, { ...request, draftId });
    },
    onSuccess: (result) => {
      const next = taskDetailToForm(result.data);
      message.success(taskId ? '需求已更新' : '需求已创建');
      setConflict(undefined);
      void queryClient.invalidateQueries({ queryKey: ['pm', 'editor'] });
      onSaved();
      if (continueCreating && !taskId) { setForm({ ...blank }); setBaseline(JSON.stringify(blank)); setDraftId(undefined); return; }
      setForm(next); setBaseline(JSON.stringify(next)); onClose();
    },
    onError: (error) => {
      const parsed = readProjectManagementTaskConflict(error);
      if (parsed || (isHttpError(error) && error.status === 409)) { setConflict(parsed); message.error('保存发生并发冲突，本地草稿已保留'); return; }
      message.error(error instanceof Error ? error.message : '保存失败');
    },
  });
  const upload = useMutation({
    mutationFn: async (file: File) => {
      if (taskId) return uploadProjectManagementTaskAttachment(taskId, file);
      const draft = draftId ? { data: { id: draftId } } : await createProjectManagementTaskDraft(projectId, JSON.stringify(form));
      if (!draftId) setDraftId(draft.data.id);
      return uploadProjectManagementTaskDraftAttachment(draft.data.id, file);
    },
    onSuccess: () => { void attachments.refetch(); message.success('附件已上传'); },
    onError: () => message.error('附件上传失败，草稿仍保留'),
  });
  const addComment = useMutation({ mutationFn: () => createProjectManagementTaskComment(taskId!, { markdown: comment, mentionUserIds: commentMentionIds }), onSuccess: () => { setComment(''); setCommentMentionIds([]); void comments.refetch(); message.success('评论已发布'); } });
  const follow = useMutation({ mutationFn: (userId: string) => taskId ? addProjectManagementTaskFollower(taskId, { userId }) : Promise.resolve(undefined), onSuccess: () => { if (taskId) void followers.refetch(); } });
  const dirty = JSON.stringify(form) !== baseline || Boolean(draftId);
  const update = (patch: Partial<ProjectManagementTaskUpsertRequest>) => setForm((current) => ({ ...current, ...patch }));
  const requestClose = () => { if (!dirty) { onClose(); return; } confirm({ title: '关闭需求编辑', content: '存在未保存修改或草稿附件，是否放弃当前编辑？', confirmText: '放弃修改', onConfirm: onClose }); };
  const candidateItems = members.data?.data.items.filter((item) => item.isSelectable) ?? [];
  const parentItems = parents.data?.data.items.filter((item) => item.id !== taskId) ?? [];
  const milestoneItems = milestones.data?.data.items ?? [];
  const followerIds = followers.data?.data.map((item) => item.userId) ?? form.followerUserIds ?? [];
  const labels = Object.fromEntries(candidateItems.map((item) => [item.userId, item.displayName]));

  return <ResponsiveModal bodyClassName="pm-work-item-editor-body" className="pm-work-item-editor" footer={<Stack alignItems="center" direction="row" justifyContent="space-between"><label className="pm-editor-checkbox"><input checked={continueCreating} onChange={(event) => setContinueCreating(event.target.checked)} type="checkbox" /> 保存后继续创建</label><Stack direction="row" spacing={1}><button className="pm-workbench-command" onClick={requestClose} type="button">取消</button><button className="pm-primary-button" disabled={!form.title.trim() || save.isPending} onClick={() => save.mutate({})} type="button">保存</button></Stack></Stack>} maxWidth="96vw" mode="modal" onClose={requestClose} open={open} title={taskId ? '编辑需求' : '新建需求'}>
    {conflict ? <ConflictPanel conflict={conflict} onKeepLocal={() => setConflict(undefined)} onOverwrite={() => save.mutate({ overwriteVersionNo: conflict.serverValues.versionNo })} onReload={() => { if (!conflict.serverValues) return; const next = taskDetailToForm(conflict.serverValues); setForm(next); setBaseline(JSON.stringify(next)); setConflict(undefined); }} /> : null}
    <Box className="pm-editor-grid"><Stack className="pm-editor-main" spacing={1.25}><input aria-label="需求标题" className="pm-editor-title" maxLength={256} onChange={(event) => update({ title: event.target.value })} placeholder="输入需求标题" value={form.title} /><Typography color="text.secondary" variant="caption">{form.title.length}/256</Typography><ProjectManagementMarkdownEditor ariaLabel="需求描述" contentJson={form.contentJson} mentionCandidates={candidateItems} onChange={(value) => update({ description: value, markdown: value })} onContentJsonChange={(value) => update({ contentJson: value })} onMentionUserIdsChange={(value) => update({ mentionUserIds: value })} placeholder="描述需求背景、目标和验收标准；输入 @ 可提及项目成员" rows={14} value={form.description ?? ''} /><Stack className="pm-editor-collaboration" spacing={1}><Stack alignItems="center" direction="row" justifyContent="space-between"><Typography fontWeight={700}>附件</Typography><label className="pm-workbench-command">上传附件<input hidden onChange={(event) => { const file = event.target.files?.[0]; if (file) upload.mutate(file); event.currentTarget.value = ''; }} type="file" /></label></Stack>{attachments.data?.data.map((item) => <Typography color="text.secondary" key={item.id} variant="caption">{item.fileName} · {Math.ceil(item.fileSize / 1024)} KB</Typography>)}{taskId ? <><Typography fontWeight={700}>评论</Typography>{comments.data?.data.items.map((item) => <Box className="pm-comment" key={item.id}><Typography color="text.secondary" variant="caption">{item.authorDisplayName ?? item.authorUserId} · {new Date(item.createdTime).toLocaleString()}</Typography><Typography variant="body2">{item.markdown}</Typography></Box>)}<ProjectManagementMarkdownEditor ariaLabel="新增评论" mentionCandidates={candidateItems} onChange={setComment} onMentionUserIdsChange={setCommentMentionIds} placeholder="添加评论，输入 @ 提及成员" rows={4} value={comment} /><button className="pm-workbench-command" disabled={!comment.trim() || addComment.isPending} onClick={() => addComment.mutate()} type="button">发表评论</button></> : <Typography color="text.secondary" variant="caption">保存后可发表评论与管理关注人。</Typography>}</Stack></Stack><Stack className="pm-editor-properties" spacing={1.1}><Field label="项目" value="当前项目" /><SelectField label="工作项类型" onChange={(value) => update({ workItemType: value })} options={['Requirement', 'UserStory', 'Task', 'Bug']} value={form.workItemType ?? 'Requirement'} /><SelectField label="状态" onChange={(value) => update({ status: value })} options={['Todo', 'InProgress', 'Blocked', 'Done', 'Closed']} value={form.status ?? 'Todo'} /><SelectField label="负责人" labels={labels} onChange={(value) => update({ assigneeUserId: value || undefined })} options={['', ...candidateItems.map((item) => item.userId)]} value={form.assigneeUserId ?? ''} /><SelectField label="父工作项" labels={Object.fromEntries(parentItems.map((item) => [item.id, `${item.taskCode} · ${item.title}`]))} onChange={(value) => update({ parentTaskId: value || undefined })} options={['', ...parentItems.map((item) => item.id)]} value={form.parentTaskId ?? ''} /><SelectField label="里程碑" labels={Object.fromEntries(milestoneItems.map((item) => [item.id, item.milestoneName]))} onChange={(value) => update({ milestoneId: value || undefined })} options={['', ...milestoneItems.map((item) => item.id)]} value={form.milestoneId ?? ''} /><DateField label="开始时间" onChange={(value) => update({ startDate: value || undefined })} value={form.startDate} /><DateField label="结束时间" onChange={(value) => update({ dueDate: value || undefined })} value={form.dueDate} /><SelectField label="优先级" onChange={(value) => update({ priority: value })} options={['Low', 'Medium', 'High', 'Urgent']} value={form.priority ?? 'Medium'} /><SelectField label="风险" onChange={(value) => update({ riskLevel: value })} options={['None', 'Low', 'Medium', 'High']} value={form.riskLevel ?? 'None'} /><SelectField label="需求类型" onChange={(value) => update({ requirementType: value || undefined })} options={['', 'Feature', 'NonFunctional', 'Other']} value={form.requirementType ?? ''} /><SelectField label="需求来源" onChange={(value) => update({ requirementSource: value || undefined })} options={['', 'ProductPlan', 'Customer', 'Internal', 'BugConversion', 'Other']} value={form.requirementSource ?? ''} /><SelectField label="关注人" labels={labels} onChange={(value) => { if (value && !followerIds.includes(value)) { follow.mutate(value); update({ followerUserIds: [...followerIds, value] }); } }} options={['', ...candidateItems.map((item) => item.userId)]} value="" /></Stack></Box>
  </ResponsiveModal>;
}

function ConflictPanel({ conflict, onKeepLocal, onOverwrite, onReload }: { conflict: NonNullable<ReturnType<typeof readProjectManagementTaskConflict>>; onKeepLocal: () => void; onOverwrite: () => void; onReload: () => void }) { return <Box className="pm-task-conflict" role="alert"><Typography fontWeight={700}>检测到并发修改，本地草稿已保留</Typography>{conflict.fieldConflicts.map((field) => <Typography key={field.field} variant="caption">{field.displayName}：服务器 {String(field.serverValue ?? '—')} / 本地 {String(field.localValue ?? '—')}</Typography>)}<Stack direction="row" spacing={1}><button className="pm-workbench-command" onClick={onReload} type="button">加载服务器值</button><button className="pm-workbench-command" onClick={onKeepLocal} type="button">保留本地草稿</button><button className="pm-primary-button" onClick={onOverwrite} type="button">覆盖保存</button></Stack></Box>; }
function Field({ label, value }: { label: string; value: string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><Typography className="pm-editor-readonly" variant="body2">{value || '—'}</Typography></Stack>; }
function SelectField({ label, labels = {}, onChange, options, value }: { label: string; labels?: Record<string, string>; onChange: (value: string) => void; options: string[]; value: string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><select className="pm-editor-select" onChange={(event) => onChange(event.target.value)} value={value}>{options.map((option) => <option key={option} value={option}>{option ? labels[option] ?? option : '未设置'}</option>)}</select></Stack>; }
function DateField({ label, onChange, value }: { label: string; onChange: (value: string) => void; value?: string }) { return <Stack spacing={0.25}><Typography color="text.secondary" variant="caption">{label}</Typography><input className="pm-editor-select" onChange={(event) => onChange(event.target.value)} type="date" value={value ? value.slice(0, 10) : ''} /></Stack>; }
