import type {
  ProjectManagementTaskDetail,
  ProjectManagementTaskAttachment,
  ProjectManagementTaskComment,
  ProjectManagementTaskCommentUpsertRequest,
  ProjectManagementTaskReminder,
  ProjectManagementTaskReminderCreateRequest,
  ProjectManagementMember,
  ProjectManagementMemberCandidate,
  ProjectManagementMilestone,
  ProjectManagementTaskUpsertRequest,
  ProjectManagementTaskDraftAttachment,
} from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { PermissionGuard } from '../../../shared/auth/PermissionGuard';
import { ProjectManagementMarkdownContent } from '../collaboration/projectManagementMarkdown';
import { ProjectManagementMarkdownEditor } from '../collaboration/ProjectManagementMarkdownEditor';

import type { TaskDetailSection } from './taskDetailDrawerModel';
import { TaskWorkspaceReminderPanel } from './TaskWorkspaceReminderPanel';

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
  projectId?: string;
  memberCandidates?: ProjectManagementMemberCandidate[];
  milestones?: ProjectManagementMilestone[];
  draftAttachments?: ProjectManagementTaskDraftAttachment[];
  draftUploading?: boolean;
  onDraftUpload?: (file: File) => void;
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
  projectId,
  memberCandidates = [],
  milestones = [],
  draftAttachments = [],
  draftUploading = false,
  onDraftUpload,
}: TaskWorkspaceSelectionPanelProps) {
  if (!creating && !selectedTask) return null;
  if (!creating && selectedTask && !['basic', 'comments', 'attachments', 'reminders'].includes(activeSection)) return null;
  const actionPermission = creating ? 'project-management:task:add' : 'project-management:task:edit';

  return (
    <aside className="mb-4 space-y-4 rounded-lg border border-gray-200 p-4">
      {(creating || activeSection === 'basic') ? <section className="pm-editor-form">
        <div className="pm-editor-form__main">
          <div className="mb-3 text-sm font-semibold">{creating ? '新建/编辑需求' : `编辑需求 · ${selectedTask?.taskCode}`}</div>
          <label className="grid gap-1 text-sm"><span>标题 <em className="text-red-600">*</em><small className="float-right text-gray-500">{form.title.length}/256</small></span><input autoFocus aria-label="需求标题" maxLength={256} onChange={(event) => onFormChange({ ...form, title: event.target.value })} placeholder="输入需求标题" value={form.title} /></label>
          <label className="mt-3 grid gap-1 text-sm"><span>描述</span><ProjectManagementMarkdownEditor
            ariaLabel="需求富文本描述"
            onChange={(markdown) => onFormChange({ ...form, description: undefined, markdown: markdown || undefined })}
            onContentJsonChange={(contentJson) => onFormChange({ ...form, contentJson })}
            onMentionUserIdsChange={(mentionUserIds) => onFormChange({ ...form, mentionUserIds })}
            contentJson={form.contentJson}
            mentionCandidates={memberCandidates}
            placeholder="支持标题、粗体、斜体、列表、链接和 @成员"
            rows={10}
            value={form.markdown ?? form.description ?? ''}
          /></label>
          <div className="mt-4 rounded border border-gray-200 p-3"><div className="mb-2 text-sm font-semibold">协作</div><div className="text-xs text-gray-500">在描述中输入 @ 后选择成员，Mention 会以结构化节点保存；附件、评论和关注人会在保存后绑定。</div>{form.mentionUserIds?.length ? <div className="mt-2 flex flex-wrap gap-1">{form.mentionUserIds.map(userId => <span className="rounded bg-blue-50 px-2 py-1 text-xs text-blue-700" key={userId}>已提及成员 · {memberCandidates.find(item => item.userId === userId)?.displayName ?? userId}</span>)}</div> : null}</div>
        </div>
        <div className="pm-editor-form__properties">
          <label>项目<input aria-label="项目" disabled value={projectId ?? selectedTask?.projectId ?? ''} /></label>
          <label>工作项类型<select aria-label="工作项类型" onChange={(event) => onFormChange({ ...form, workItemType: event.target.value })} value={form.workItemType ?? 'Task'}>{[['Epic', '史诗'], ['Story', '用户故事'], ['Requirement', '功能需求'], ['Task', '任务'], ['Bug', '缺陷']].map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
          <label>状态<select aria-label="需求状态" onChange={(event) => onFormChange({ ...form, status: event.target.value })} value={form.status}>{[['Todo', '未开始'], ['InProgress', '进行中'], ['Blocked', '已阻塞'], ['Done', '已完成'], ['Cancelled', '已关闭']].map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
          <label>负责人<select aria-label="需求负责人" onChange={(event) => onFormChange({ ...form, assigneeUserId: event.target.value || undefined })} value={form.assigneeUserId ?? ''}><option value="">未分配</option>{memberCandidates.filter(item => item.isSelectable).map(item => <option key={item.userId} value={item.userId}>{item.displayName || item.userName}</option>)}</select></label>
          <label>父工作项<input aria-label="父工作项" placeholder="输入父工作项编号" value={form.parentTaskId ?? ''} onChange={(event) => onFormChange({ ...form, parentTaskId: event.target.value || undefined })} /></label>
          <label>里程碑<select aria-label="里程碑" onChange={(event) => onFormChange({ ...form, milestoneId: event.target.value || undefined })} value={form.milestoneId ?? ''}><option value="">未设置</option>{milestones.map(item => <option key={item.id} value={item.id}>{item.milestoneName}</option>)}</select></label>
          <label>开始时间<input aria-label="开始时间" onChange={(event) => onFormChange({ ...form, startDate: event.target.value || undefined })} type="date" value={toDateInputValue(form.startDate)} /></label>
          <label>结束时间<input aria-label="结束时间" onChange={(event) => onFormChange({ ...form, dueDate: event.target.value || undefined })} type="date" value={toDateInputValue(form.dueDate)} /></label>
          <label>优先级<select aria-label="优先级" onChange={(event) => onFormChange({ ...form, priority: event.target.value })} value={form.priority}>{[['Low', '低'], ['Medium', '中'], ['High', '高'], ['Urgent', '紧急']].map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
          <label>风险<select aria-label="风险等级" onChange={(event) => onFormChange({ ...form, riskLevel: event.target.value })} value={form.riskLevel ?? 'None'}>{[['None', '无'], ['Low', '低'], ['Medium', '中'], ['High', '高'], ['Closed', '已关闭']].map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
          <label>需求类型<input aria-label="需求类型" value={form.requirementType ?? ''} onChange={(event) => onFormChange({ ...form, requirementType: event.target.value || undefined })} placeholder="用户故事/功能需求" /></label>
          <label>需求来源<input aria-label="需求来源" value={form.requirementSource ?? ''} onChange={(event) => onFormChange({ ...form, requirementSource: event.target.value || undefined })} placeholder="产品计划/客户反馈" /></label>
          <label>故事点<input aria-label="故事点" min={0} type="number" value={form.storyPoints ?? ''} onChange={(event) => onFormChange({ ...form, storyPoints: event.target.value ? Number(event.target.value) : undefined })} /></label>
          <fieldset className="pm-editor-form__followers"><legend>关注人</legend><div>{memberCandidates.filter(item => item.isSelectable).slice(0, 20).map(item => <label className="flex items-center gap-1 text-xs" key={`follower-${item.userId}`}><input checked={Boolean(form.followerUserIds?.includes(item.userId))} onChange={(event) => onFormChange({ ...form, followerUserIds: event.target.checked ? [...new Set([...(form.followerUserIds ?? []), item.userId])] : (form.followerUserIds ?? []).filter(id => id !== item.userId) })} type="checkbox" />{item.displayName || item.userName}</label>)}</div></fieldset>
          <label>任务编码<input aria-label="需求编号" onChange={(event) => onFormChange({ ...form, taskCode: event.target.value })} placeholder="例如 DEMO-1" value={form.taskCode} /></label>
        </div>
        <div className="pm-editor-form__footer"><label className="text-sm"><input type="checkbox" /> 继续创建下一个</label><div className="flex gap-2"><button type="button" onClick={onCancel}>取消</button><PermissionButton code={actionPermission} disabled={!form.taskCode.trim() || !form.title.trim() || saving} onClick={onSubmit}>{saving ? '保存中…' : creating ? '创建' : '保存'}</PermissionButton></div></div>
        {creating && onDraftUpload ? <div className="mt-3 rounded border border-dashed border-gray-300 p-3 text-sm"><div className="font-medium">草稿附件</div><div className="mt-1 text-xs text-gray-500">需求提交前附件保存在 Draft Session，创建成功后事务性绑定。</div>{draftAttachments.map(item => <div className="mt-1 text-xs text-gray-600" key={item.id}>{item.fileName} · {Math.ceil(item.fileSize / 1024)} KB</div>)}<input className="mt-2" disabled={draftUploading} onChange={(event) => { const file = event.target.files?.[0]; if (file) onDraftUpload(file); event.currentTarget.value = ''; }} type="file" /></div> : null}
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
        memberCandidates={memberCandidates}
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
  memberCandidates,
}: Omit<TaskWorkspaceSelectionPanelProps, 'creating' | 'form' | 'onCancel' | 'onFormChange' | 'onSubmit' | 'saving' | 'selectedTask'>) {
  return <div className="grid gap-4 lg:grid-cols-2">
    {activeSection === 'comments' ? <section>
      <div className="mb-2 flex items-center justify-between font-semibold"><span>任务评论</span><span className="text-xs font-normal text-gray-500">共 {commentsTotal} 条</span></div>
      {commentsError ? <div className="rounded bg-amber-50 p-2 text-sm text-amber-800">评论加载失败，请重新选择任务重试。</div> : <div className="mb-3 space-y-2">
        {comments.length === 0 ? <div className="text-sm text-gray-500">暂无评论</div> : comments.map((comment) => <article className="rounded border border-gray-100 p-2" key={comment.id}>
          {commentEditing?.id === comment.id ? <>
            <ProjectManagementMarkdownEditor ariaLabel={`编辑评论 ${comment.id}`} onChange={(markdown) => onCommentEditChange({ ...commentEditForm, markdown })} onMentionUserIdsChange={(mentionUserIds) => onCommentEditChange({ ...commentEditForm, mentionUserIds })} mentionCandidates={memberCandidates} placeholder="支持安全 Markdown" rows={4} value={commentEditForm.markdown} />
            <div className="mt-2 flex gap-2"><PermissionButton code="project-management:comment:add" disabled={!commentEditForm.markdown.trim() || commentEditSubmitting} onClick={onCommentEditSubmit}>{commentEditSubmitting ? '保存中…' : '保存修改'}</PermissionButton><button type="button" onClick={onCommentEditCancel}>取消</button></div>
          </> : <>
            <ProjectManagementMarkdownContent className="text-sm" value={comment.markdown} />
            <div className="mt-1 flex items-center justify-between text-xs text-gray-500"><span>{comment.authorDisplayName ?? '用户别名暂不可用'} · {new Date(comment.createdTime).toLocaleString()}{comment.editedTime ? ' · 已编辑' : ''}</span><span className="flex gap-2"><button type="button" onClick={() => onCommentEditStart(comment)}>编辑</button><button className="text-red-600" type="button" onClick={() => onCommentDelete(comment)}>删除</button></span></div>
          </>}
        </article>)}
      </div>}
      {commentsTotal > commentPageSize ? <div className="mb-3 flex items-center justify-between text-xs"><button disabled={commentPageIndex <= 1} type="button" onClick={() => onCommentPageChange(commentPageIndex - 1)}>上一页</button><span>第 {commentPageIndex} 页</span><button disabled={commentPageIndex * commentPageSize >= commentsTotal} type="button" onClick={() => onCommentPageChange(commentPageIndex + 1)}>下一页</button></div> : null}
      <ProjectManagementMarkdownEditor ariaLabel="评论内容" onChange={(markdown) => onCommentChange({ ...commentForm, markdown })} onMentionUserIdsChange={(mentionUserIds) => onCommentChange({ ...commentForm, mentionUserIds })} mentionCandidates={memberCandidates} placeholder="支持安全 Markdown 评论" rows={4} value={commentForm.markdown} />
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
