import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { FlowiseApiKeysNativePage } from '../native/views/apikey';
import { FlowiseAssistantsNativePage } from '../native/views/assistants';
import { FlowiseSsoConfigNativePage } from '../native/views/auth/ssoConfig';
import { FlowiseNativeFlowListPage } from '../native/views/chatflows';
import { FlowiseCredentialsNativePage } from '../native/views/credentials';
import { FlowiseDatasetsNativePage } from '../native/views/datasets';
import { FlowiseDocumentStoresNativePage } from '../native/views/docstore';
import { FlowiseEvaluationsNativePage } from '../native/views/evaluations';
import { FlowiseEvaluatorsNativePage } from '../native/views/evaluators';
import { FlowiseLoginActivityNativePage } from '../native/views/login-activity';
import { FlowiseMarketplacesNativePage } from '../native/views/marketplaces';
import { FlowiseLogsNativePage } from '../native/views/serverlogs';
import { FlowiseToolsNativePage } from '../native/views/tools';
import { FlowiseVariablesNativePage } from '../native/views/variables';

export function FlowiseChatflowsPage() {
  return <FlowiseNativeFlowListPage title={flowiseI18nKeys.pages.chatflows} type="CHATFLOW" />;
}

export function FlowiseWorkflowsPage() {
  return <FlowiseNativeFlowListPage title={flowiseI18nKeys.pages.workflows} type="AGENTFLOW" />;
}

export function FlowiseAssistantsPage() {
  return <FlowiseAssistantsNativePage />;
}

export function FlowiseMarketplacesPage() {
  return <FlowiseMarketplacesNativePage />;
}

export function FlowiseToolsPage() {
  return <FlowiseToolsNativePage />;
}

export function FlowiseCredentialsPage() {
  return <FlowiseCredentialsNativePage />;
}

export function FlowiseVariablesPage() {
  return <FlowiseVariablesNativePage />;
}

export function FlowiseApiKeysPage() {
  return <FlowiseApiKeysNativePage />;
}

export function FlowiseDocumentStoresPage() {
  return <FlowiseDocumentStoresNativePage />;
}

export function FlowiseDatasetsPage() {
  return <FlowiseDatasetsNativePage />;
}

export function FlowiseEvaluatorsPage() {
  return <FlowiseEvaluatorsNativePage />;
}

export function FlowiseEvaluationsPage() {
  return <FlowiseEvaluationsNativePage />;
}

export function FlowiseSsoConfigPage() {
  return <FlowiseSsoConfigNativePage />;
}

export function FlowiseLoginActivityPage() {
  return <FlowiseLoginActivityNativePage />;
}

export function FlowiseLogsPage() {
  return <FlowiseLogsNativePage />;
}
