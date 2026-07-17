import type {
  WorkflowAttachmentDto,
  WorkflowCommentDto,
  WorkflowSubmittedFormFieldDto,
  WorkflowTaskActionRequest,
  WorkflowTaskDetailDto,
  WorkflowTaskListItemDto,
  WorkflowTimelineItemDto
} from '../../api/workflow/workflows.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../../shared/icons/AppIcon';
import { ResponsiveModal } from '../../shared/responsive/ResponsiveModal';

export type WorkflowApprovalAction = 'complete' | 'delegate' | 'reject' | 'resolve' | 'transfer';

export interface WorkflowApprovalActionState extends WorkflowTaskActionRequest {
  variablesJson: string;
}

interface WorkflowApprovalDrawerProps {
  actionState: WorkflowApprovalActionState;
  actionTitle: string;
  actionType: WorkflowApprovalAction;
  detail?: WorkflowTaskDetailDto | null;
  loading: boolean;
  open: boolean;
  submitting: boolean;
  task?: WorkflowTaskListItemDto | null;
  userOptions: Array<{ label: string; value: string }>;
  onClose: () => void;
  onDownloadAttachment: (attachment: WorkflowAttachmentDto) => void;
  onOpenProcess: (processInstanceId: string) => void;
  onSubmit: () => void;
  onUploadAttachment: (file: File) => void;
  onValueChange: (name: keyof WorkflowApprovalActionState, value: string) => void;
  uploadingAttachment: boolean;
}

export function WorkflowApprovalDrawer({
  actionState,
  actionTitle,
  actionType,
  detail,
  loading,
  onClose,
  onDownloadAttachment,
  onOpenProcess,
  onSubmit,
  onUploadAttachment,
  onValueChange,
  open,
  submitting,
  task,
  uploadingAttachment,
  userOptions
}: WorkflowApprovalDrawerProps) {
  const { translate } = useI18n();
  const currentTask = detail?.task ?? task ?? null;
  const requiresTargetUser = actionType === 'delegate' || actionType === 'transfer';
  const canSubmit = !submitting && (!requiresTargetUser || Boolean(actionState.targetUserId));

  return (
    <ResponsiveModal
      footer={(
        <>
          <button className="bg-white border border-gray-300 text-gray-700 px-4 py-1.5 rounded text-sm hover:bg-gray-50" disabled={submitting} type="button" onClick={onClose}>
            {translate('workflow.drawer.cancel')}
          </button>
          <button className="bg-primary-600 text-white px-4 py-1.5 rounded text-sm hover:bg-primary-700 disabled:opacity-60 disabled:cursor-not-allowed" disabled={!canSubmit} type="button" onClick={onSubmit}>
            {submitting ? translate('workflow.drawer.submitting') : translate('workflow.drawer.submit')}
          </button>
        </>
      )}
      mode="drawer"
      open={open}
      title={actionTitle}
      onClose={onClose}
    >
      <div className="grid gap-4 text-sm">
        <TaskSummary task={currentTask} onOpenProcess={onOpenProcess} />

        {loading ? (
          <div className="rounded border border-gray-200 bg-white px-4 py-8 text-center text-gray-500">{translate('table.loadingDefault')}</div>
        ) : (
          <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,1fr)_300px] gap-4 items-start">
            <section className="grid gap-4 min-w-0">
              <SubmittedFormPanel fields={detail?.submittedForm.fields ?? []} />
              <AttachmentPanel
                attachments={detail?.attachments ?? []}
                uploading={uploadingAttachment}
                onDownloadAttachment={onDownloadAttachment}
                onUploadAttachment={onUploadAttachment}
              />
              <TimelinePanel timeline={detail?.timeline ?? []} />
            </section>

            <aside className="grid gap-4 min-w-0">
              <ActionPanel
                actionState={actionState}
                requiresTargetUser={requiresTargetUser}
                userOptions={userOptions}
                onValueChange={onValueChange}
              />
              <CommentPanel comments={detail?.comments ?? []} />
            </aside>
          </div>
        )}
      </div>
    </ResponsiveModal>
  );
}

function TaskSummary({ onOpenProcess, task }: { onOpenProcess: (processInstanceId: string) => void; task: WorkflowTaskListItemDto | null }) {
  const { translate } = useI18n();

  return (
    <section className="rounded border border-gray-200 bg-white p-4 shadow-sm">
      <div className="workflow-panel-header mb-3">
        <div>
          <div className="workflow-panel-title">{task?.name ?? task?.id ?? translate('workflow.drawer.taskFallback')}</div>
          <div className="workflow-panel-subtitle">{task?.processName ?? task?.processDefinitionId ?? '-'}</div>
        </div>
        {task?.processInstanceId ? (
          <button className="workflow-icon-button" title={translate('workflow.drawer.openProcessTrail')} type="button" onClick={() => onOpenProcess(task.processInstanceId ?? '')}>
            <AppIcon name="git-branch" />
          </button>
        ) : null}
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <SummaryItem label={translate('workflow.drawer.businessType')} value={task?.businessType} />
        <SummaryItem label={translate('workflow.drawer.businessKey')} value={task?.businessKey ?? task?.processInstanceId} />
        <SummaryItem label={translate('workflow.drawer.starter')} value={task?.starterUserName} />
        <SummaryItem label={translate('workflow.drawer.assignee')} value={task?.assigneeName ?? task?.assignee ?? task?.candidateNames.join('、')} />
        <SummaryItem label={translate('workflow.drawer.createdAt')} value={formatDateTime(task?.createdAt)} />
        <SummaryItem label={translate('workflow.drawer.dueAt')} value={formatDateTime(task?.dueAt)} />
      </div>
    </section>
  );
}

function SubmittedFormPanel({ fields }: { fields: WorkflowSubmittedFormFieldDto[] }) {
  const { translate } = useI18n();

  return (
    <section className="rounded border border-gray-200 bg-white p-4 shadow-sm min-w-0">
      <div className="workflow-panel-title mb-3">{translate('workflow.drawer.submittedForm')}</div>
      {fields.length === 0 ? (
        <EmptyState text={translate('workflow.drawer.noSubmittedFields')} />
      ) : (
        <div className="grid gap-2">
          {fields.map((field) => (
            <div key={field.field} className="grid grid-cols-1 sm:grid-cols-[150px_minmax(0,1fr)] gap-2 border-b border-gray-100 pb-2 last:border-b-0 last:pb-0">
              <div className="min-w-0">
                <div className="text-gray-700 break-all">{field.label || field.field}</div>
                <div className="text-xs text-gray-400 break-all">{field.field}</div>
              </div>
              <div className="rounded border border-gray-100 bg-gray-50 px-3 py-2 text-gray-900 break-all whitespace-pre-wrap">
                {formatValue(field.value)}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function AttachmentPanel({
  attachments,
  onDownloadAttachment,
  onUploadAttachment,
  uploading
}: {
  attachments: WorkflowAttachmentDto[];
  onDownloadAttachment: (attachment: WorkflowAttachmentDto) => void;
  onUploadAttachment: (file: File) => void;
  uploading: boolean;
}) {
  const { translate } = useI18n();

  return (
    <section className="rounded border border-gray-200 bg-white p-4 shadow-sm min-w-0">
      <div className="mb-3 flex items-center justify-between gap-3">
        <div className="workflow-panel-title">{translate('workflow.drawer.attachments')}</div>
        <label className={`workflow-icon-button shrink-0 ${uploading ? 'opacity-60 cursor-wait' : ''}`} title={translate('workflow.drawer.uploadAttachment')}>
          <AppIcon name={uploading ? 'spinner-gap' : 'paperclip'} className={uploading ? 'animate-spin' : undefined} />
          <input
            className="hidden"
            disabled={uploading}
            type="file"
            onChange={(event) => {
              const file = event.target.files?.[0];
              event.target.value = '';
              if (file) {
                onUploadAttachment(file);
              }
            }}
          />
        </label>
      </div>
      {attachments.length === 0 ? (
        <EmptyState text={translate('workflow.drawer.noAttachments')} />
      ) : (
        <div className="grid gap-2">
          {attachments.map((attachment) => (
            <div key={attachment.id} className="flex items-start justify-between gap-3 rounded border border-gray-100 bg-gray-50 px-3 py-2">
              <div className="min-w-0">
                <div className="font-medium text-gray-900 break-all">{attachment.name || attachment.id}</div>
                <div className="mt-1 flex flex-wrap gap-x-3 gap-y-1 text-xs text-gray-500">
                  <span>{attachment.type || translate('workflow.drawer.attachment')}</span>
                  <span>{formatDateTime(attachment.createdAt)}</span>
                </div>
                {attachment.description ? <div className="mt-1 text-xs text-gray-500 break-all">{attachment.description}</div> : null}
              </div>
              <AttachmentAction attachment={attachment} onDownloadAttachment={onDownloadAttachment} />
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function AttachmentAction({
  attachment,
  onDownloadAttachment
}: {
  attachment: WorkflowAttachmentDto;
  onDownloadAttachment: (attachment: WorkflowAttachmentDto) => void;
}) {
  const { translate } = useI18n();

  if (attachment.downloadUrl) {
    return (
      <button className="workflow-icon-button shrink-0" title={translate('workflow.drawer.download')} type="button" onClick={() => onDownloadAttachment(attachment)}>
        <AppIcon name="download-simple" />
      </button>
    );
  }

  if (attachment.url) {
    return (
      <button className="workflow-icon-button shrink-0" title={translate('workflow.drawer.openLink')} type="button" onClick={() => window.open(attachment.url ?? '', '_blank', 'noopener,noreferrer')}>
        <AppIcon name="arrow-square-out" />
      </button>
    );
  }

  return (
    <button className="workflow-icon-button shrink-0 opacity-50 cursor-not-allowed" disabled title={translate('workflow.drawer.noDownload')} type="button">
      <AppIcon name="paperclip" />
    </button>
  );
}

function ActionPanel({
  actionState,
  onValueChange,
  requiresTargetUser,
  userOptions
}: {
  actionState: WorkflowApprovalActionState;
  requiresTargetUser: boolean;
  userOptions: Array<{ label: string; value: string }>;
  onValueChange: (name: keyof WorkflowApprovalActionState, value: string) => void;
}) {
  const { translate } = useI18n();

  return (
    <section className="rounded border border-gray-200 bg-white p-4 shadow-sm min-w-0">
      <div className="workflow-panel-title mb-3">{translate('workflow.drawer.actionPanel')}</div>
      <div className="grid gap-3">
        {requiresTargetUser ? (
          <label className="grid gap-1 text-sm text-gray-600">
            {translate('workflow.drawer.targetUser')}
            <select
              className="border border-gray-300 rounded bg-white px-3 py-2 text-sm focus:outline-none focus:border-primary-500"
              value={actionState.targetUserId ?? ''}
              onChange={(event) => onValueChange('targetUserId', event.target.value)}
            >
              <option value="">{translate('workflow.drawer.selectTargetUser')}</option>
              {userOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
        ) : null}
        <label className="grid gap-1 text-sm text-gray-600">
          {translate('workflow.drawer.comment')}
          <textarea
            className="min-h-28 border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-primary-500 resize-y"
            placeholder={translate('workflow.drawer.commentPlaceholder')}
            value={actionState.comment ?? ''}
            onChange={(event) => onValueChange('comment', event.target.value)}
          />
        </label>
        <details className="rounded border border-gray-200 bg-gray-50 px-3 py-2">
          <summary className="cursor-pointer select-none text-gray-700">{translate('workflow.drawer.advancedVariables')}</summary>
          <textarea
            className="mt-2 min-h-28 w-full border border-gray-300 rounded bg-white px-3 py-2 font-mono text-xs focus:outline-none focus:border-primary-500 resize-y"
            value={actionState.variablesJson}
            onChange={(event) => onValueChange('variablesJson', event.target.value)}
          />
        </details>
      </div>
    </section>
  );
}

function CommentPanel({ comments }: { comments: WorkflowCommentDto[] }) {
  const { translate } = useI18n();

  return (
    <section className="rounded border border-gray-200 bg-white p-4 shadow-sm min-w-0">
      <div className="workflow-panel-title mb-3">{translate('workflow.drawer.comments')}</div>
      {comments.length === 0 ? (
        <EmptyState text={translate('workflow.drawer.noComments')} />
      ) : (
        <div className="grid gap-3">
          {comments.map((comment) => (
            <div key={comment.id} className="border-b border-gray-100 pb-3 last:border-b-0 last:pb-0">
              <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
                <span>{comment.type || 'comment'}</span>
                <span>{comment.userId || '-'}</span>
                <span>{formatDateTime(comment.time)}</span>
              </div>
              <div className="mt-1 text-gray-800 break-all whitespace-pre-wrap">{comment.message || '-'}</div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function TimelinePanel({ timeline }: { timeline: WorkflowTimelineItemDto[] }) {
  const { translate } = useI18n();

  return (
    <section className="rounded border border-gray-200 bg-white p-4 shadow-sm min-w-0">
      <div className="workflow-panel-title mb-3">{translate('workflow.drawer.timeline')}</div>
      {timeline.length === 0 ? (
        <EmptyState text={translate('workflow.drawer.noTimeline')} />
      ) : (
        <div className="workflow-timeline">
          {timeline.map((item) => (
            <TimelineItem key={item.id} item={item} />
          ))}
        </div>
      )}
    </section>
  );
}

function TimelineItem({ item }: { item: WorkflowTimelineItemDto }) {
  return (
    <div className="workflow-timeline-item">
      <div className="workflow-timeline-time">{formatDateTime(item.createdAt)}</div>
      <div className="workflow-timeline-body">
        <strong>{item.title}</strong>
        <span>{item.kind} · {item.userName ?? item.userId ?? item.action ?? '-'}</span>
        {item.comment ? <p>{item.comment}</p> : null}
      </div>
    </div>
  );
}

function SummaryItem({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="rounded border border-gray-100 bg-gray-50 px-3 py-2 min-w-0">
      <div className="text-xs text-gray-500">{label}</div>
      <div className="mt-1 text-gray-900 break-all">{value || '-'}</div>
    </div>
  );
}

function EmptyState({ text }: { text: string }) {
  return <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-3 py-5 text-center text-gray-500">{text}</div>;
}

function formatDateTime(value?: string | null) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function formatValue(value: unknown) {
  if (value === null || value === undefined || value === '') {
    return '-';
  }

  if (typeof value === 'object') {
    return JSON.stringify(value, null, 2);
  }

  return String(value);
}
