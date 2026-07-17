import { CheckCircle2, GripVertical, Pencil, Play, Plus, RefreshCw, SkipForward } from 'lucide-react';

import type { AiTaskPlanDto, AiTaskPlanItemDto } from '.././api/aiCenter.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { AiMarkdownContent } from '../../../shared/components/ai-chat/AiMarkdownContent';
import { parseJsonArray, parsePlanMetadata } from '../task-plan/taskPlanUtils';

import type { WorkMode } from './aiChatWorkspaceTypes';

const taskPlanStatusClassMap: Record<string, string> = {
  Approved: 'ai-status-badge ai-status-badge--success',
  Blocked: 'ai-status-badge ai-status-badge--danger',
  Cancelled: 'ai-status-badge',
  Draft: 'ai-status-badge',
  Failed: 'ai-status-badge ai-status-badge--danger',
  PartialCompleted: 'ai-status-badge ai-status-badge--success',
  Pending: 'ai-status-badge ai-status-badge--running',
  PendingConfirmation: 'ai-status-badge ai-status-badge--running',
  PlanReady: 'ai-status-badge ai-status-badge--running',
  Running: 'ai-status-badge ai-status-badge--running',
  Skipped: 'ai-status-badge',
  Succeeded: 'ai-status-badge ai-status-badge--success',
  WaitingUser: 'ai-status-badge ai-status-badge--running'
};

const taskOwnerTypeKeyMap: Record<string, string> = {
  Agent: 'ai.runLogs.option.agent',
  Tool: 'ai.toolMatrix.field.toolCode',
  User: 'layout.user.fallback'
};

const taskStatusKeyMap: Record<string, string> = {
  Approved: 'page.workflowMonitoring.status.completed',
  Blocked: 'ai.taskPlan.blocked',
  Cancelled: 'page.workflowNotifications.option.taskStatus.cancelled',
  Draft: 'page.workflowModels.status.draft',
  Failed: 'ai.toolExecutions.status.failed',
  PartialCompleted: 'page.workflowMonitoring.status.completed',
  Pending: 'home.work.status.pending',
  PendingConfirmation: 'ai.toolExecutions.status.pendingConfirmation',
  Running: 'page.workflowMonitoring.status.running',
  Skipped: 'ai.taskPlan.skipped',
  Succeeded: 'ai.toolExecutions.status.succeeded',
  WaitingUser: 'ai.taskPlan.waitingTitle',
  Unknown: 'ai.toolExecutions.status.unknown'
};

function formatTaskLabel(
  value: string | undefined,
  translate: (key: string) => string,
  keyMap: Record<string, string>,
  fallbackKey: string
): string {
  if (!value) {
    return translate(fallbackKey);
  }

  const translatedKey = keyMap[value];
  return translatedKey ? translate(translatedKey) : value;
}

function renderTaskStatusBadge(status: string, translate: (key: string) => string) {
  return <span className={taskPlanStatusClassMap[status] ?? 'ai-status-badge'}>{formatTaskLabel(status, translate, taskStatusKeyMap, 'ai.toolExecutions.status.unknown')}</span>;
}

interface TaskPlanPanelProps {
  editingTaskDraft: { description: string; title: string };
  editingTaskId: string | null;
  expandedTaskIds: Record<string, boolean>;
  isStreaming: boolean;
  onAddTask: () => void;
  onApprove: (plan: AiTaskPlanDto) => void;
  onCancelEdit: () => void;
  onEditTask: (item: AiTaskPlanItemDto) => void;
  onExecute: (plan: AiTaskPlanDto) => void;
  onMoveTask: (plan: AiTaskPlanDto, item: AiTaskPlanItemDto, direction: -1 | 1) => void;
  onPatchTask: (item: AiTaskPlanItemDto, status: string, userText?: string) => void;
  onReplan: () => void;
  onSaveEdit: (item: AiTaskPlanItemDto) => void;
  onTaskActionDraftChange: (taskId: string, value: string) => void;
  onTaskDraftChange: (draft: { description: string; title: string }) => void;
  onToggleTask: (taskId: string) => void;
  plan: AiTaskPlanDto | null;
  taskActionDrafts: Record<string, string>;
  workMode: WorkMode;
}

export function TaskPlanPanel({
  editingTaskDraft,
  editingTaskId,
  expandedTaskIds,
  isStreaming,
  plan,
  workMode,
  onAddTask,
  onApprove,
  onCancelEdit,
  onEditTask,
  onExecute,
  onMoveTask,
  onPatchTask,
  onReplan,
  onSaveEdit,
  onTaskActionDraftChange,
  onTaskDraftChange,
  onToggleTask,
  taskActionDrafts
}: TaskPlanPanelProps) {
  const { translate } = useI18n();
  if (!plan) {
    return (
      <div className="ai-task-empty">
        <p>{workMode === 'Plan' ? translate('ai.taskPlan.empty.plan') : translate('ai.taskPlan.empty.ask')}</p>
        <button className="ghost-button" type="button" onClick={onAddTask}>
          <Plus size={14} />
          {translate('ai.taskPlan.addTask')}
        </button>
      </div>
    );
  }

  const orderedItems = [...plan.items].sort((left, right) => left.sortOrder - right.sortOrder);
  const progress = plan.progress?.percent ?? 0;
  const risks = parseJsonArray(plan.risksJson);
  const metadata = parsePlanMetadata(plan.metadataJson);

  return (
    <div className="ai-task-plan">
      <header className="ai-task-plan-header">
        <div>
          <strong>{plan.title}</strong>
          <span>{metadata.overview || plan.goal}</span>
        </div>
        {renderTaskStatusBadge(plan.status, translate)}
      </header>
      <div className="ai-task-progress">
        <div>
          <span style={{ width: `${progress}%` }} />
        </div>
        <strong>{progress}%</strong>
      </div>
      {risks.length > 0 ? <p className="ai-task-risk">{risks.slice(0, 2).join(' / ')}</p> : null}
      {metadata.planMarkdown ? (
        <details className="ai-task-plan-doc" open>
          <summary>{translate('ai.taskPlan.planMarkdown')}</summary>
          <AiMarkdownContent content={metadata.planMarkdown} />
        </details>
      ) : null}
      <div className="ai-task-actions">
        <PermissionButton
          className="ghost-button"
          code="ai:task-plan:approve"
          disabled={isStreaming || !['Draft', 'PlanReady'].includes(plan.status)}
          type="button"
          onClick={() => onApprove(plan)}
        >
          <CheckCircle2 size={14} />
          {translate('ai.taskPlan.approve')}
        </PermissionButton>
        <PermissionButton
          className="ghost-button"
          code="ai:task-plan:execute"
          disabled={isStreaming || !['Approved', 'PartialCompleted'].includes(plan.status)}
          type="button"
          onClick={() => onExecute(plan)}
        >
          <Play size={14} />
          {translate('ai.taskPlan.execute')}
        </PermissionButton>
        <PermissionButton className="ghost-button" code="ai:task-plan:edit" disabled={isStreaming || !['Draft', 'PlanReady'].includes(plan.status)} type="button" onClick={onReplan}>
          <RefreshCw size={14} />
          {translate('ai.taskPlan.replan')}
        </PermissionButton>
        <PermissionButton className="ghost-button" code="ai:task-plan:edit" disabled={isStreaming || !['Draft', 'PlanReady'].includes(plan.status)} type="button" onClick={onAddTask}>
          <Plus size={14} />
          {translate('ai.taskPlan.addTask')}
        </PermissionButton>
      </div>
      <div className="ai-task-list">
        {orderedItems.map((item, index) => {
          const expanded = Boolean(expandedTaskIds[item.id]);
          const editing = editingTaskId === item.id;
          const waitingUser = item.status === 'WaitingUser';
          const actionDraft = taskActionDrafts[item.id] ?? item.resultSummary ?? '';
          return (
            <article className={`ai-task-item ai-task-item--${item.status.toLowerCase()}`} key={item.id} draggable={!isStreaming}>
              <button className="ai-task-grip" title={translate('ai.taskPlan.sort')} type="button" onClick={() => onMoveTask(plan, item, index === 0 ? 1 : -1)}>
                <GripVertical size={14} />
              </button>
              <button className="ai-task-main" type="button" onClick={() => onToggleTask(item.id)}>
                <span>
                  <b>{item.priority}</b>
                  {item.title}
                </span>
                <small>
                  {formatTaskLabel(item.ownerType, translate, taskOwnerTypeKeyMap, 'layout.user.fallback')} / {formatTaskLabel(item.status, translate, taskStatusKeyMap, 'ai.toolExecutions.status.unknown')}
                </small>
              </button>
              <button className="icon-button" title={translate('ai.taskPlan.editTask')} type="button" onClick={() => onEditTask(item)}>
                <Pencil size={14} />
              </button>
              <button className="icon-button" title={translate('ai.taskPlan.markComplete')} type="button" onClick={() => onPatchTask(item, 'Succeeded')}>
                <CheckCircle2 size={14} />
              </button>
              <button className="icon-button" title={translate('ai.taskPlan.skipTask')} type="button" onClick={() => onPatchTask(item, 'Skipped')}>
                <SkipForward size={14} />
              </button>
              <button className="icon-button" title={translate('ai.taskPlan.retryTask')} type="button" onClick={() => onPatchTask(item, 'Pending')}>
                <RefreshCw size={14} />
              </button>
              {waitingUser && !editing ? (
                <div className="ai-task-user-action">
                  <div>
                    <strong>{translate('ai.taskPlan.waitingTitle')}</strong>
                    <span>{translate('ai.taskPlan.waitingDescription')}</span>
                  </div>
                  <textarea
                    disabled={isStreaming}
                    placeholder={translate('ai.taskPlan.waitingPlaceholder')}
                    value={actionDraft}
                    onChange={(event) => onTaskActionDraftChange(item.id, event.target.value)}
                  />
                  <div>
                    <PermissionButton
                      className="primary-button"
                      code="ai:task-plan:edit"
                      disabled={isStreaming}
                      type="button"
                      onClick={() => onPatchTask(item, 'Succeeded', actionDraft)}
                    >
                      <CheckCircle2 size={14} />
                      {translate('ai.taskPlan.confirmComplete')}
                    </PermissionButton>
                    <PermissionButton
                      className="ghost-button"
                      code="ai:task-plan:skip"
                      disabled={isStreaming}
                      type="button"
                      onClick={() => onPatchTask(item, 'Skipped', actionDraft)}
                    >
                      <SkipForward size={14} />
                      {translate('ai.taskPlan.skipTemporarily')}
                    </PermissionButton>
                  </div>
                </div>
              ) : null}
              {(expanded || waitingUser) ? (
                <div className="ai-task-detail">
                  {editing ? (
                    <>
                      <input
                        value={editingTaskDraft.title}
                        onChange={(event) => onTaskDraftChange({ ...editingTaskDraft, title: event.target.value })}
                      />
                      <textarea
                        value={editingTaskDraft.description}
                        onChange={(event) => onTaskDraftChange({ ...editingTaskDraft, description: event.target.value })}
                      />
                      <div>
                        <button className="primary-button" type="button" onClick={() => onSaveEdit(item)}>
                          {translate('ai.taskPlan.saveDraft')}
                        </button>
                        <button className="ghost-button" type="button" onClick={onCancelEdit}>
                          {translate('ai.taskPlan.cancel')}
                        </button>
                      </div>
                    </>
                  ) : (
                    <>
                      <p>{item.description || translate('ai.taskPlan.noDescription')}</p>
                      {parseJsonArray(item.acceptanceCriteriaJson).length > 0 ? <span>{translate('ai.taskPlan.acceptance')}: {parseJsonArray(item.acceptanceCriteriaJson).join(' / ')}</span> : null}
                      {item.resultSummary ? <span>{translate('ai.taskPlan.summary')}: {item.resultSummary}</span> : null}
                      {item.result ? <span>{translate('ai.taskPlan.result')}: {item.result}</span> : null}
                      {item.blockedReason ? <span>{translate('ai.taskPlan.blocked')}: {item.blockedReason}</span> : null}
                      {item.skipReason ? <span>{translate('ai.taskPlan.skipped')}: {item.skipReason}</span> : null}
                      {item.errorMessage ? <span>{translate('ai.taskPlan.error')}: {item.errorMessage}</span> : null}
                    </>
                  )}
                </div>
              ) : null}
            </article>
          );
        })}
      </div>
    </div>
  );
}
