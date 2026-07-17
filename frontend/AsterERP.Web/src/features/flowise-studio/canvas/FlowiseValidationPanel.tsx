import { useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import type { FlowiseCanvasValidationResult } from '../types/canvas.types';

interface FlowiseValidationPanelProps {
  result?: FlowiseCanvasValidationResult | null;
}

export function FlowiseValidationPanel({ result }: FlowiseValidationPanelProps) {
  const { translate } = useI18n();
  const issues = result?.issues ?? [];

  return (
    <section className="flowise-validation-panel">
      <h3>
        <AppIcon name="check" /> {translate(flowiseI18nKeys.canvas.validation)}
      </h3>
      {issues.length === 0 ? (
        <p>{result?.valid === false ? translate(flowiseI18nKeys.messages.noRows) : translate(flowiseI18nKeys.messages.noValidationIssues)}</p>
      ) : (
        <ul>
          {issues.map((issue) => (
            <li className={`flowise-validation-panel__issue flowise-validation-panel__issue--${issue.severity}`} key={`${issue.code}-${issue.nodeId ?? issue.edgeId ?? issue.message}`}>
              <strong>{issue.code}</strong>
              <span>{issue.message}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
