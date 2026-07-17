import { IconButton } from '@mui/material';

import { AppIcon } from '../../../../../shared/icons/AppIcon';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseCanvasValidationResult } from '../../../types/canvas.types';

interface ValidationPopUpProps {
  validation: FlowiseCanvasValidationResult | null;
  translate: (key: string) => string;
  onClose: () => void;
}

export function ValidationPopUp({ validation, translate, onClose }: ValidationPopUpProps) {
  const issues = validation?.issues ?? [];

  return (
    <section className="flowise-chat-validation-popup" role="dialog" aria-modal="false">
      <header>
        <strong>{translate(flowiseI18nKeys.canvas.validation)}</strong>
        <IconButton size="small" onClick={onClose}>
          <AppIcon name="x" />
        </IconButton>
      </header>
      {issues.length === 0 ? (
        <p>{translate(flowiseI18nKeys.messages.noValidationIssues)}</p>
      ) : (
        <ul>
          {issues.map((issue, index) => (
            <li key={`${issue.code}-${issue.nodeId ?? issue.edgeId ?? index}`} className={`flowise-chat-validation-popup__issue flowise-chat-validation-popup__issue--${issue.severity}`}>
              <strong>{issue.code}</strong>
              <span>{issue.message}</span>
              {issue.nodeId || issue.edgeId ? <small>{issue.nodeId ?? issue.edgeId}</small> : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
