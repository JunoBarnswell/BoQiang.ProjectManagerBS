import { useMemo } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';

import { AiCenterTabs, type AiCenterTabItem } from './AiCenterTabs';
import { AiFailureAnalysisPage } from './AiFailureAnalysisPage';
import { AiRunLogsPage } from './AiRunLogsPage';
import { AiToolExecutionsPage } from './AiToolExecutionsPage';
import { AiUsageStatisticsPage } from './AiUsageStatisticsPage';

export function AiObservabilityPage() {
  const { translate } = useI18n();
  const tabs = useMemo<AiCenterTabItem[]>(
    () => [
      { key: 'overview', label: translate('ai.observability.tabs.overview'), content: <AiUsageStatisticsPage /> },
      { key: 'runs', label: translate('ai.observability.tabs.runs'), content: <AiRunLogsPage /> },
      { key: 'tools', label: translate('ai.observability.tabs.tools'), content: <AiToolExecutionsPage /> },
      { key: 'failures', label: translate('ai.observability.tabs.failures'), content: <AiFailureAnalysisPage /> }
    ],
    [translate]
  );

  return <AiCenterTabs defaultTab="overview" items={tabs} />;
}
