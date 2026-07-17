import { useI18n } from '../../core/i18n/I18nProvider';

import { AiAgentProfilesPage } from './AiAgentProfilesPage';
import { AiCenterTabs, type AiCenterTabItem } from './AiCenterTabs';
import { AiKnowledgePage } from './AiKnowledgePage';
import { AiModelConfigsPage } from './AiModelConfigsPage';
import { AiModelProvidersPage } from './AiModelProvidersPage';
import { AiPromptTemplatesPage } from './AiPromptTemplatesPage';
import { AiToolMatrixPage } from './AiToolMatrixPage';
import { KnowledgeGraphPage } from './knowledge-graph';

interface AiCapabilityCenterPageProps {
  allowedTabs?: string[];
  defaultTab?: string;
}

export function AiCapabilityCenterPage({ allowedTabs, defaultTab = 'agents' }: AiCapabilityCenterPageProps) {
  const { translate } = useI18n();
  const tabs: AiCenterTabItem[] = [
    { key: 'agents', label: translate('ai.capability.agents'), description: translate('ai.capability.agentsDescription'), content: <AiAgentProfilesPage /> },
    { key: 'models', label: translate('ai.capability.models'), description: translate('ai.capability.modelsDescription'), content: <AiModelConfigsPage /> },
    { key: 'providers', label: translate('ai.capability.providers'), description: translate('ai.capability.providersDescription'), content: <AiModelProvidersPage /> },
    { key: 'prompts', label: translate('ai.capability.prompts'), description: translate('ai.capability.promptsDescription'), content: <AiPromptTemplatesPage /> },
    { key: 'knowledge', label: translate('ai.capability.knowledge'), description: translate('ai.capability.knowledgeDescription'), content: <AiKnowledgePage /> },
    { key: 'knowledge-graph', label: translate('ai.capability.knowledgeGraph'), description: translate('ai.capability.knowledgeGraphDescription'), content: <KnowledgeGraphPage /> },
    { key: 'tools', label: translate('ai.capability.tools'), description: translate('ai.capability.toolsDescription'), content: <AiToolMatrixPage /> }
  ];
  const visibleTabs = allowedTabs?.length ? tabs.filter((item) => allowedTabs.includes(item.key)) : tabs;

  return (
    <AiCenterTabs
      defaultTab={defaultTab}
      description={translate('ai.capability.description')}
      eyebrow={translate('ai.eyebrow')}
      items={visibleTabs.length ? visibleTabs : tabs}
      title={translate('ai.capability.title')}
    />
  );
}
