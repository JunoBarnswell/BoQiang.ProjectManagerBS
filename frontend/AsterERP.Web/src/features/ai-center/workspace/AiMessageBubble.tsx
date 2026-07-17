import { Bot, Play } from 'lucide-react';

import type { AiWorkflowOverviewDto } from '.././api/aiCenter.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { AiMarkdownContent } from '../../../shared/components/ai-chat/AiMarkdownContent';
import { WorkflowDraftPreviewCard } from '../workflow/WorkflowDraftPreviewCard';

import type { AiMessageDraft } from './aiChatWorkspaceTypes';

const messageStatusClassMap: Record<string, string> = {
  Active: 'ai-status-badge ai-status-badge--success',
  Archived: 'ai-status-badge',
  Cancelled: 'ai-status-badge',
  Completed: 'ai-status-badge ai-status-badge--success',
  Disabled: 'ai-status-badge',
  Enabled: 'ai-status-badge ai-status-badge--success',
  Failed: 'ai-status-badge ai-status-badge--danger',
  Running: 'ai-status-badge ai-status-badge--running',
  Succeeded: 'ai-status-badge ai-status-badge--success'
};

const messageStatusKeyMap: Record<string, string> = {
  Cancelled: 'ai.runLogs.option.cancelled',
  Completed: 'page.workflowMonitoring.status.completed',
  Disabled: 'common.disabled',
  Enabled: 'common.enabled',
  Failed: 'ai.toolExecutions.status.failed',
  Running: 'page.workflowMonitoring.status.running',
  Succeeded: 'ai.toolExecutions.status.succeeded',
  Unknown: 'ai.toolExecutions.status.unknown'
};

function formatMessageStatusLabel(status: string | null | undefined, translate: (key: string) => string): string {
  if (!status) {
    return translate('ai.toolExecutions.status.unknown');
  }

  const translatedKey = messageStatusKeyMap[status];
  return translatedKey ? translate(translatedKey) : status;
}

function renderMessageStatusBadge(status: string | null | undefined, translate: (key: string) => string) {
  const value = status || 'Unknown';
  return <span className={messageStatusClassMap[value] ?? 'ai-status-badge'}>{formatMessageStatusLabel(status, translate)}</span>;
}

interface AiMessageBubbleProps {
  message: AiMessageDraft;
  onExecutePlan?: () => void;
  workflowOverview?: AiWorkflowOverviewDto | null;
}

export function AiMessageBubble({ message, workflowOverview, onExecutePlan }: AiMessageBubbleProps) {
  const { translate } = useI18n();
  const isUser = message.role === 'user';
  const workflowArtifacts = !isUser && message.runId
    ? (workflowOverview?.draftArtifacts ?? []).filter((artifact) => artifact.runId === message.runId)
    : [];

  let displayContent = message.content || (message.pending ? translate('ai.messageBubble.pending') : '');
  let isPlanJson = false;

  if (!isUser && message.content.trim().startsWith('{')) {
    try {
      const parsed = JSON.parse(message.content.trim());
      if (parsed && typeof parsed === 'object' && typeof parsed.title === 'string') {
        isPlanJson = true;
        displayContent = `## ${parsed.title}\n\n`;
        if (parsed.goal) displayContent += `**${translate('ai.messageBubble.plan.goal')}:** ${parsed.goal}\n\n`;
        if (parsed.overview) displayContent += `**${translate('ai.messageBubble.plan.overview')}:** ${parsed.overview}\n\n`;
        if (parsed.executionStrategy) displayContent += `**${translate('ai.messageBubble.plan.executionStrategy')}:** ${parsed.executionStrategy}\n\n`;
        if (Array.isArray(parsed.risks) && parsed.risks.length > 0) displayContent += `**${translate('ai.messageBubble.plan.risks')}:** ${parsed.risks.join(' / ')}\n\n`;
        if (Array.isArray(parsed.assumptions) && parsed.assumptions.length > 0) displayContent += `**${translate('ai.messageBubble.plan.assumptions')}:** ${parsed.assumptions.join(' / ')}\n\n`;
        if (parsed.planMarkdown) displayContent += `---\n${parsed.planMarkdown}`;
      }
    } catch {
      if (message.content.includes('"title"') || message.content.includes('"planMarkdown"')) {
        isPlanJson = true;
        displayContent = message.content
          .replace(/^\{\s*/, '')
          .replace(/\s*\}\s*$/, '')
          .replace(/\\n/g, '\n')
          .replace(/\\"/g, '"')
          .replace(/\\\\/g, '\\');
      }
    }
  }

  return (
    <article className={`ai-message ${isUser ? 'ai-message--user' : 'ai-message--assistant'}`}>
      <div className="ai-message-avatar">{isUser ? translate('ai.messageBubble.user') : <Bot size={17} />}</div>
      <div className="ai-message-body">
        <div className="ai-message-meta">
          <span>{isUser ? translate('ai.messageBubble.user') : translate('ai.messageBubble.assistant')}</span>
          {renderMessageStatusBadge(message.pending ? 'Running' : message.status, translate)}
        </div>
        {message.reasoningContent ? (
          <details className="ai-reasoning-panel">
            <summary>{translate('ai.messageBubble.reasoning')}</summary>
            <AiMarkdownContent content={message.reasoningContent} />
          </details>
        ) : null}
        <AiMarkdownContent content={displayContent} />
        {isPlanJson && !message.pending && onExecutePlan ? (
          <div style={{ marginTop: 12 }}>
            <button className="primary-button" type="button" onClick={onExecutePlan}>
              <Play size={14} style={{ marginRight: 4 }} />
              {translate('ai.messageBubble.executePlan')}
            </button>
          </div>
        ) : null}
        {workflowArtifacts.map((artifact) => (
          <WorkflowDraftPreviewCard
            key={artifact.id}
            artifact={artifact}
            simulation={(workflowOverview?.simulationReports ?? []).find((item) => item.draftArtifactId === artifact.id)}
            validation={(workflowOverview?.validationReports ?? []).find((item) => item.draftArtifactId === artifact.id)}
          />
        ))}
      </div>
    </article>
  );
}
