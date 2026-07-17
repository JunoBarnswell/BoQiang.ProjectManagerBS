import { AlertTriangle, CheckCircle2 } from 'lucide-react';

import type { AiWorkflowValidationReportDto } from '.././api/aiCenter.api';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';

import { formatWorkflowTime } from './workflowUtils';

export function WorkflowValidationPanel({ report }: { report?: AiWorkflowValidationReportDto | null }) {
  const { translate } = useI18n();
  if (!report) {
    return <div className="ai-empty-state">{translate('ai.workflowSupport.validation.empty')}</div>;
  }

  return (
    <div className="ai-workflow-panel">
      <header>
        {report.isValid ? <CheckCircle2 size={15} /> : <AlertTriangle size={15} />}
        <strong>{report.isValid ? translate('ai.workflowSupport.validation.passed') : formatMessage(translate('ai.workflowSupport.validation.issues'), { errorCount: report.errorCount, warningCount: report.warningCount })}</strong>
        <span>{formatWorkflowTime(report.createdTime)}</span>
      </header>
      <div className="ai-workflow-issue-list">
        {report.issues.map((issue, index) => (
          <article key={`${issue.errorCode}-${issue.nodeId}-${index}`} className={`ai-workflow-issue ai-workflow-issue--${issue.severity.toLowerCase()}`}>
            <b>{issue.severity}</b>
            <span>{issue.message}</span>
            {issue.nodeId || issue.edgeId ? <small>{issue.nodeId ?? issue.edgeId}</small> : null}
          </article>
        ))}
        {report.issues.length === 0 ? <div className="ai-empty-state">{translate('ai.workflowSupport.validation.noIssues')}</div> : null}
      </div>
    </div>
  );
}
