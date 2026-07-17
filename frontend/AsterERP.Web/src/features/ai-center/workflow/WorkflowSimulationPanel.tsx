import { Route } from 'lucide-react';

import type { AiWorkflowSimulationReportDto } from '.././api/aiCenter.api';
import { useI18n } from '../../../core/i18n/I18nProvider';

import { formatWorkflowTime, stringifyCompact } from './workflowUtils';

export function WorkflowSimulationPanel({ report }: { report?: AiWorkflowSimulationReportDto | null }) {
  const { translate } = useI18n();
  if (!report) {
    return <div className="ai-empty-state">{translate('ai.workflowSupport.simulation.empty')}</div>;
  }

  return (
    <div className="ai-workflow-panel">
      <header>
        <Route size={15} />
        <strong>{report.succeeded ? translate('ai.workflowSupport.simulation.completed') : translate('ai.workflowSupport.simulation.notReachedEnd')}</strong>
        <span>{formatWorkflowTime(report.createdTime)}</span>
      </header>
      <div className="ai-workflow-vars">{Object.entries(report.variables).map(([key, value]) => <span key={key}>{key}: {stringifyCompact(value)}</span>)}</div>
      <ol className="ai-workflow-simulation">
        {report.steps.map((step) => (
          <li key={`${step.sortOrder}-${step.nodeId}`}>
            <b>{step.nodeName}</b>
            <span>{step.summary}</span>
            {step.condition ? <small>{step.condition}</small> : null}
          </li>
        ))}
      </ol>
    </div>
  );
}
