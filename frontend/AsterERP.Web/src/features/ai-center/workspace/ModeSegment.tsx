import { useI18n } from '../../../core/i18n/I18nProvider';

import type { WorkMode } from './aiChatWorkspaceTypes';

interface ModeSegmentProps {
  onChange: (value: WorkMode) => void;
  value: WorkMode;
}

const modes: Array<{ labelKey: string; value: WorkMode }> = [
  { labelKey: 'ai.chat.workMode.ask', value: 'Ask' },
  { labelKey: 'ai.chat.workMode.plan', value: 'Plan' },
  { labelKey: 'ai.chat.workMode.agent', value: 'Agent' }
];

export function ModeSegment({ value, onChange }: ModeSegmentProps) {
  const { translate } = useI18n();

  return (
    <div className="ai-mode-segment" role="tablist" aria-label={translate('ai.chat.workMode.label')}>
      {modes.map((mode) => (
        <button
          aria-selected={value === mode.value}
          className={value === mode.value ? 'ai-mode-segment__item--active' : ''}
          key={mode.value}
          role="tab"
          type="button"
          onClick={() => onChange(mode.value)}
        >
          {translate(mode.labelKey)}
        </button>
      ))}
    </div>
  );
}
