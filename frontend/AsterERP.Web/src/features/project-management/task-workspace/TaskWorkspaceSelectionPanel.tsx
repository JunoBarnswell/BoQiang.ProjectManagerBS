import type {
  ProjectManagementTaskDetail,
  ProjectManagementTaskAttachment,
  ProjectManagementTaskComment,
  ProjectManagementTaskCommentUpsertRequest,
  ProjectManagementTaskReminder,
  ProjectManagementTaskReminderCreateRequest,
  ProjectManagementMember,
  ProjectManagementTaskUpsertRequest,
} from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../../shared/auth/PermissionGuard';
import { ProjectManagementMarkdownContent } from '../collaboration/projectManagementMarkdown';
import { ProjectManagementMarkdownEditor } from '../collaboration/ProjectManagementMarkdownEditor';

import { TaskWorkspaceReminderPanel } from './TaskWorkspaceReminderPanel';
import type { TaskDetailSection } from './taskDetailDrawerModel';

interface TaskWorkspaceSelectionPanelProps {
  activeSection?: TaskDetailSection;
  attachments: ProjectManagementTaskAttachment[];
  attachmentsError: boolean;
  attachmentDownloadError?: string;
  attachmentDownloadErrorId?: string;
  attachmentDownloadingId?: string;
  attachmentPreviewError?: string;
  attachmentPreviewErrorId?: string;
  attachmentPreviewingId?: string;
  attachmentUploadError?: string;
  attachmentUploadProgress: number;
  attachmentUploading: boolean;
  comments: ProjectManagementTaskComment[];
  commentsTotal: number;
  commentsError: boolean;
  commentForm: ProjectManagementTaskCommentUpsertRequest;
  commentSubmitting: boolean;
  commentEditing: ProjectManagementTaskComment | null;
  commentEditForm: ProjectManagementTaskCommentUpsertRequest;
  commentEditSubmitting: boolean;
  commentPageIndex: number;
  commentPageSize: number;
  creating: boolean;
  form: ProjectManagementTaskUpsertRequest;
  onCancel: () => void;
  onCommentChange: (next: ProjectManagementTaskCommentUpsertRequest) => void;
  onCommentSubmit: () => void;
  onCommentEditChange: (next: ProjectManagementTaskCommentUpsertRequest) => void;
  onCommentEditStart: (comment: ProjectManagementTaskComment) => void;
  onCommentEditCancel: () => void;
  onCommentEditSubmit: () => void;
  onCommentDelete: (comment: ProjectManagementTaskComment) => void;
  onCommentPageChange: (pageIndex: number) => void;
  onFormChange: (next: ProjectManagementTaskUpsertRequest) => void;
  onSubmit: () => void;
  onUpload: (file: File) => void;
  onCancelUpload: () => void;
  onRetryUpload: () => void;
  onRetryAttachments: () => void;
  onDownloadAttachment: (attachment: ProjectManagementTaskAttachment) => void;
  onRetryDownloadAttachment: (attachment: ProjectManagementTaskAttachment) => void;
  onPreviewAttachment: (attachment: ProjectManagementTaskAttachment) => void;
  onRetryPreviewAttachment: (attachment: ProjectManagementTaskAttachment) => void;
  reminders: ProjectManagementTaskReminder[];
  remindersError: boolean;
  remindersLoading: boolean;
  reminderCreating: boolean;
  reminderMembers: ProjectManagementMember[];
  onCreateReminder: (request: ProjectManagementTaskReminderCreateRequest) => void;
  onCancelReminder: (reminder: ProjectManagementTaskReminder) => void;
  onDeleteReminder: (reminder: ProjectManagementTaskReminder) => void;
  saving: boolean;
  selectedTask?: ProjectManagementTaskDetail;
}

export function TaskWorkspaceSelectionPanel({
  activeSection = 'basic',
  attachments,
  attachmentsError,
  attachmentDownloadError,
  attachmentDownloadErrorId,
  attachmentDownloadingId,
  attachmentPreviewError,
  attachmentPreviewErrorId,
  attachmentPreviewingId,
  attachmentUploadError,
  attachmentUploadProgress,
  attachmentUploading,
  comments,
  commentsTotal,
  commentsError,
  commentForm,
  commentSubmitting,
  commentEditing,
  commentEditForm,
  commentEditSubmitting,
  commentPageIndex,
  commentPageSize,
  creating,
  form,
  onCancel,
  onCommentChange,
  onCommentSubmit,
  onCommentEditChange,
  onCommentEditStart,
  onCommentEditCancel,
  onCommentEditSubmit,
  onCommentDelete,
  onCommentPageChange,
  onFormChange,
  onSubmit,
  onUpload,
  onCancelUpload,
  onRetryUpload,
  onRetryAttachments,
  onDownloadAttachment,
  onRetryDownloadAttachment,
  onPreviewAttachment,
  onRetryPreviewAttachment,
  reminders,
  remindersError,
  remindersLoading,
  reminderCreating,
  reminderMembers,
  onCreateReminder,
  onCancelReminder,
  onDeleteReminder,
  saving,
  selectedTask,
}: TaskWorkspaceSelectionPanelProps) {
  if (!creating && !selectedTask) return null;
  if (!creating && selectedTask && !['basic', 'comments', 'attachments', 'reminders'].includes(activeSection)) return null;
  const actionPermission = creating ? 'project-management:task:add' : 'project-management:task:edit';

  return (
    <aside className="mb-4 space-y-4 rounded-lg border border-gray-200 p-4">
      {(creating || activeSection === 'basic') ? <section>
        <div className="mb-3 text-sm font-semibold">{creating ? '新建任务' : `编辑任务 · ${selectedTask?.taskCode}`}</div>
        <div className="grid gap-2 md:grid-cols-5">
          <input aria-label="任务编码" onChange={(event) => onFormChange({ ...form, taskCode: event.target.value })} placeholder="任务编码" value={form.taskCode} />
          <input aria-label="任务标题" onChange={(event) => onFormChange({ ...form, title: event.target.value })} placeholder="任务标题" value={form.title} />
          <select aria-label="任务状态" onChange={(event) => onFormChange({ ...form, status: event.target.value })} value={form.status}>
            {['Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled'].map((status) => <option key={status}>{status}</option>)}
          </select>
          <select aria-label="任务优先级" onChange={(event) => onFormChange({ ...form, priority: event.target.value })} value={form.priority}>
            {['Low', 'Medium', 'High', 'Urgent'].map((priority) => <option key={priority}>{priority}</option>)}
          </select>
          <input aria-label="任务进度" max={100} min={0} onChange={(event) => onFormChange({ ...form, progressPercent: Number(event.target.value) })} type="number" value={form.progressPercent ?? 0} />
          <input aria-label="开始日期" onChange={(event) => onFormChange({ ...form, startDate: event.target.value || undefined })} type="date" value={toDateInputValue(form.startDate)} />
          <input aria-label="截止日期" onChange={(event) => onFormChange({ ...form, dueDate: event.target.value || undefined })} type="date" value={toDateInputValue(form.dueDate)} />
        </div>
        <div className="mt-2"><ProjectManagementMarkdownEditor
          ariaLabel="任务 Markdown 描述"
          onChange={(markdown) => onFormChange({ ...form, description: undefined, markdown: markdown || undefined })}
          placeholder="任务描述，支持安全 Markdown"
          rows={5}
          value={form.markdown ?? form.description ?? ''}
        /></div>
        <div className="mt-3 flex gap-2">
          <PermissionButton code={actionPermission} disabled={!form.taskCode.trim() || !form.title.trim() || saving} onClick={onSubmit}>{saving ? '保存中…' : creating ? '创建任务' : '保存修改'}</PermissionButton>
          <button type="button" onClick={onCancel}>取消</button>
        </div>
      </section> : null}
      {!creating && selectedTask ? <TaskCollaborationPanel
        activeSection={activeSection}
        attachments={attachments}
        attachmentsError={attachmentsError}
        attachmentDownloadError={attachmentDownloadError}
        attachmentDownloadErrorId={attachmentDownloadErrorId}
        attachmentDownloadingId={attachmentDownloadingId}
        attachmentPreviewError={attachmentPreviewError}
        attachmentPreviewErrorId={attachmentPreviewErrorId}
        attachmentPreviewingId={attachmentPreviewingId}
        attachmentUploadError={attachmentUploadError}
        attachmentUploadProgress={attachmentUploadProgress}
        attachmentUploading={attachmentUploading}
        comments={comments}
        commentsTotal={commentsTotal}
        commentsError={commentsError}
        commentForm={commentForm}
        commentSubmitting={commentSubmitting}
        commentEditing={commentEditing}
        commentEditForm={commentEditForm}
        commentEditSubmitting={commentEditSubmitting}
        commentPageIndex={commentPageIndex}
        commentPageSize={commentPageSize}
        onCommentEditChange={onCommentEditChange}
        onCommentEditStart={onCommentEditStart}
        onCommentEditCancel={onCommentEditCancel}
        onCommentEditSubmit={onCommentEditSubmit}
        onCommentDelete={onCommentDelete}
        onCommentPageChange={onCommentPageChange}
        onCommentChange={onCommentChange}
        onCommentSubmit={onCommentSubmit}
        reminders={reminders}
        remindersError={remindersError}
        remindersLoading={remindersLoading}
        reminderCreating={reminderCreating}
        reminderMembers={reminderMembers}
        onCreateReminder={onCreateReminder}
        onCancelReminder={onCancelReminder}
        onDeleteReminder={onDeleteReminder}
        onUpload={onUpload}
        onCancelUpload={onCancelUpload}
        onRetryUpload={onRetryUpload}
        onRetryAttachments={onRetryAttachments}
        onDownloadAttachment={onDownloadAttachment}
        onRetryDownloadAttachment={onRetryDownloadAttachment}
        onPreviewAttachment={onPreviewAttachment}
        onRetryPreviewAttachment={onRetryPreviewAttachment}
      /> : null}
    </aside>
  );
}

function toDateInputValue(value?: string): string {
  return value?.slice(0, 10) ?? '';
}

function TaskCollaborationPanel({
  activeSection,
  attachments,
  attachmentsError,
  attachmentDownloadError,
  attachmentDownloadErrorId,
  attachmentDownloadingId,
  attachmentPreviewError,
  attachmentPreviewErrorId,
  attachmentPreviewingId,
  attachmentUploadError,
  attachmentUploadProgress,
  attachmentUploading,
  comments,
  commentsTotal,
  commentsError,
  commentForm,
  commentSubmitting,
  commentEditing,
  commentEditForm,
  commentEditSubmitting,
  commentPageIndex,
  commentPageSize,
  onCommentChange,
  onCommentSubmit,
  onCommentEditChange,
  onCommentEditStart,
  onCommentEditCancel,
  onCommentEditSubmit,
  onCommentDelete,
  onCommentPageChange,
  reminders,
  remindersError,
  remindersLoading,
  reminderCreating,
  reminderMembers,
  onCreateReminder,
  onCancelReminder,
  onDeleteReminder,
  onUpload,
  onCancelUpload,
  onRetryUpload,
  onRetryAttachments,
  onDownloadAttachment,
  onRetryDownloadAttachment,
  onPreviewAttachment,
  onRetryPreviewAttachment,
}: Omit<TaskWorkspaceSelectionPanelProps, 'creating' | 'form' | 'onCancel' | 'onFormChange' | 'onSubmit' | 'saving' | 'selectedTask'>) {
  return <div className="grid gap-4 lg:grid-cols-2">
    {activeSection === 'comments' ? <section>
      <div className="mb-2 flex items-center justify-between font-semibold"><span>任务评论</span><span className="text-xs font-normal text-gray-500">共 {commentsTotal} 条</span></div>
      {commentsError ? <div className="rounded bg-amber-50 p-2 text-sm text-amber-800">评论加载失败，请重新选择任务重试。</div> : <div className="mb-3 space-y-2">
        {comments.length === 0 ? <div className="text-sm text-gray-500">暂无评论</div> : comments.map((comment) => <article className="rounded border border-gray-100 p-2" key={comment.id}>
          {commentEditing?.id === comment.id ? <>
            <ProjectManagementMarkdownEditor ariaLabel={`编辑评论 ${comment.id}`} onChange={(markdown) => onCommentEditChange({ ...commentEditForm, markdown })} placeholder="支持安全 Markdown" rows={4} value={commentEditForm.markdown} />
            <div className="mt-2 flex gap-2"><PermissionButton code="project-management:comment:add" disabled={!commentEditForm.markdown.trim() || commentEditSubmitting} onClick={onCommentEditSubmit}>{commentEditSubmitting ? '保存中…' : '保存修改'}</PermissionButton><button type="button" onClick={onCommentEditCancel}>取消</button></div>
          </> : <>
            <ProjectManagementMarkdownContent className="text-sm" value={comment.markdown} />
            <div className="mt-1 flex items-center justify-between text-xs text-gray-500"><span>{comment.authorUserId} · {new Date(comment.createdTime).toLocaleString()}{comment.editedTime ? ' · 已编辑' : ''}</span><span className="flex gap-2"><button type="button" onClick={() => onCommentEditStart(comment)}>编辑</button><button className="text-red-600" type="button" onClick={() => onCommentDelete(comment)}>删除</button></span></div>
          </>}
        </article>)}
      </div>}
      {commentsTotal > commentPageSize ? <div className="mb-3 flex items-center justify-between text-xs"><button disabled={commentPageIndex <= 1} type="button" onClick={() => onCommentPageChange(commentPageIndex - 1)}>上一页</button><span>第 {commentPageIndex} 页</span><button disabled={commentPageIndex * commentPageSize >= commentsTotal} type="button" onClick={() => onCommentPageChange(commentPageIndex + 1)}>下一页</button></div> : null}
      <ProjectManagementMarkdownEditor ariaLabel="评论内容" onChange={(markdown) => onCommentChange({ markdown })} placeholder="支持安全 Markdown 评论" rows={4} value={commentForm.markdown} />
      <div className="mt-2"><PermissionButton code="project-management:comment:add" disabled={!commentForm.markdown.trim() || commentSubmitting} onClick={onCommentSubmit}>{commentSubmitting ? '发布中…' : '发布评论'}</PermissionButton></div>
    </section> : null}
    {activeSection === 'attachments' ? <section>
      <div className="mb-2 font-semibold">任务附件</div>
      {attachmentsError ? <div className="mb-2 rounded bg-amber-50 p-2 text-sm text-amber-800">附件列表加载失败。<button className="ml-2 underline" type="button" onClick={onRetryAttachments}>重试</button></div> : <div className="mb-2 space-y-2">
        {attachments.length === 0 ? <div className="text-sm text-gray-500">暂无附件</div> : attachments.map((attachment) => <div className="rounded border border-gray-100 p-2" key={attachment.id}>
          <div className="flex items-start justify-between gap-2"><div className="min-w-0"><div className="break-all text-sm font-medium text-gray-900">{attachment.fileName}</div><div className="mt-1 text-xs text-gray-500">{attachment.contentType} · {Math.ceil(attachment.fileSize / 1024)} KB · {new Date(attachment.createdTime).toLocaleString()}</div></div><div className="flex shrink-0 gap-2 text-xs">
            <button disabled={!attachment.previewSupported || attachmentPreviewingId === attachment.id} title={attachment.previewSupported ? '在线预览' : '当前格式不支持预览，请下载'} type="button" onClick={() => onPreviewAttachment(attachment)}>{attachmentPreviewingId === attachment.id ? '加载中…' : attachment.previewSupported ? '预览' : '不支持预览'}</button>
            <button disabled={attachmentDownloadingId === attachment.id} type="button" onClick={() => onDownloadAttachment(attachment)}>{attachmentDownloadingId === attachment.id ? '下载中…' : '下载'}</button>
          </div></div>
          {attachmentPreviewError && attachmentPreviewErrorId === attachment.id ? <div className="mt-2 flex items-center justify-between rounded bg-amber-50 p-2 text-xs text-amber-800"><span>{attachmentPreviewError}</span><button className="underline" type="button" onClick={() => onRetryPreviewAttachment(attachment)}>重试预览</button></div> : null}
          {attachmentDownloadError && attachmentDownloadErrorId === attachment.id ? <div className="mt-2 flex items-center justify-between rounded bg-amber-50 p-2 text-xs text-amber-800"><span>{attachmentDownloadError}</span><button className="underline" type="button" onClick={() => onRetryDownloadAttachment(attachment)}>重试下载</button></div> : null}
        </div>)}
      </div>}
      <PermissionGuard code="project-management:attachment:manage" fallback={null}><div className="rounded border border-dashed border-gray-300 p-3 text-sm text-gray-600" onDragOver={(event) => event.preventDefault()} onDrop={(event) => { event.preventDefault(); const file = event.dataTransfer.files[0]; if (file && !attachmentUploading) onUpload(file); }}><div>拖拽文件到此处，或选择文件</div><input aria-label="上传任务附件" disabled={attachmentUploading} onChange={(event) => { const file = event.target.files?.[0]; if (file && !attachmentUploading) onUpload(file); event.currentTarget.value = ''; }} type="file" />{attachmentUploading ? <div className="mt-2"><progress aria-label="附件上传进度" className="w-full" max={100} value={attachmentUploadProgress} /><div className="flex items-center justify-between text-xs"><span>{attachmentUploadProgress}%</span><button type="button" onClick={onCancelUpload}>取消上传</button></div></div> : null}{attachmentUploadError ? <div className="mt-2 flex items-center justify-between text-xs text-amber-700"><span>{attachmentUploadError}</span><button type="button" onClick={onRetryUpload}>重试上传</button></div> : null}</div></PermissionGuard>
    </section> : null}
    {activeSection === 'reminders' ? <TaskWorkspaceReminderPanel creating={reminderCreating} error={remindersError} loading={remindersLoading} members={reminderMembers} onCreate={onCreateReminder} onCancel={onCancelReminder} onDelete={onDeleteReminder} reminders={reminders} /> : null}
  </div>;
}
