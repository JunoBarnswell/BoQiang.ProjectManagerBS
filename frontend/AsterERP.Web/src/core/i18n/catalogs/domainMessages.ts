import { applicationConsoleMessagesEnUS } from './applicationConsoleMessages.en-US';
import { applicationConsoleMessagesZhCN } from './applicationConsoleMessages.zh-CN';
import { domainAiCapabilityMessagesEnUS, domainAiCapabilityMessagesZhCN } from './domain/aiCapabilityMessages';
import { domainAiKnowledgeSecurityMessagesEnUS, domainAiKnowledgeSecurityMessagesZhCN } from './domain/aiKnowledgeSecurityMessages';
import { domainAiWorkbenchMessagesEnUS, domainAiWorkbenchMessagesZhCN } from './domain/aiWorkbenchMessages';
import { domainDashboardMessagesEnUS, domainDashboardMessagesZhCN } from './domain/dashboardMessages';
import { domainPlatformMessagesEnUS, domainPlatformMessagesZhCN } from './domain/platformMessages';
import { domainPrintMessagesEnUS, domainPrintMessagesZhCN } from './domain/printMessages';
import { domainSharedDomainMessagesEnUS, domainSharedDomainMessagesZhCN } from './domain/sharedDomainMessages';
import { domainSystemIdentityMessagesEnUS, domainSystemIdentityMessagesZhCN } from './domain/systemIdentityMessages';
import { domainSystemInfrastructureMessagesEnUS, domainSystemInfrastructureMessagesZhCN } from './domain/systemInfrastructureMessages';
import { domainSystemOperationsMessagesEnUS, domainSystemOperationsMessagesZhCN } from './domain/systemOperationsMessages';
import { domainWorkflowBusinessMessagesEnUS, domainWorkflowBusinessMessagesZhCN } from './domain/workflowBusinessMessages';
import { domainWorkflowNotificationsMessagesEnUS, domainWorkflowNotificationsMessagesZhCN } from './domain/workflowNotificationsMessages';
import { domainWorkflowPagesMessagesEnUS, domainWorkflowPagesMessagesZhCN } from './domain/workflowPagesMessages';
import { domainWorkflowProcessMessagesEnUS, domainWorkflowProcessMessagesZhCN } from './domain/workflowProcessMessages';
import { domainWorkflowTasksMessagesEnUS, domainWorkflowTasksMessagesZhCN } from './domain/workflowTasksMessages';
import { lowCodeStudioMessagesEnUS } from './lowCodeStudioMessages.en-US';
import { lowCodeStudioMessagesZhCN } from './lowCodeStudioMessages.zh-CN';
import { systemMessagesEnUS, systemMessagesZhCN } from './systemMessages';
import { systemMessagesExtraEnUS, systemMessagesExtraZhCN } from './systemMessagesExtra';

type MessageBag = Record<string, string>;

export const domainMessagesZhCN: MessageBag = {
  ...applicationConsoleMessagesZhCN,
  ...lowCodeStudioMessagesZhCN,
  ...systemMessagesZhCN,
  ...systemMessagesExtraZhCN,
  ...domainDashboardMessagesZhCN,
  ...domainPlatformMessagesZhCN,
  ...domainSystemIdentityMessagesZhCN,
  ...domainSystemOperationsMessagesZhCN,
  ...domainSystemInfrastructureMessagesZhCN,
  ...domainWorkflowPagesMessagesZhCN,
  ...domainWorkflowNotificationsMessagesZhCN,
  ...domainWorkflowProcessMessagesZhCN,
  ...domainWorkflowTasksMessagesZhCN,
  ...domainWorkflowBusinessMessagesZhCN,
  ...domainAiWorkbenchMessagesZhCN,
  ...domainAiCapabilityMessagesZhCN,
  ...domainAiKnowledgeSecurityMessagesZhCN,
  ...domainPrintMessagesZhCN,
  ...domainSharedDomainMessagesZhCN,
};

export const domainMessagesEnUS: MessageBag = {
  ...applicationConsoleMessagesEnUS,
  ...lowCodeStudioMessagesEnUS,
  ...systemMessagesEnUS,
  ...systemMessagesExtraEnUS,
  ...domainDashboardMessagesEnUS,
  ...domainPlatformMessagesEnUS,
  ...domainSystemIdentityMessagesEnUS,
  ...domainSystemOperationsMessagesEnUS,
  ...domainSystemInfrastructureMessagesEnUS,
  ...domainWorkflowPagesMessagesEnUS,
  ...domainWorkflowNotificationsMessagesEnUS,
  ...domainWorkflowProcessMessagesEnUS,
  ...domainWorkflowTasksMessagesEnUS,
  ...domainWorkflowBusinessMessagesEnUS,
  ...domainAiWorkbenchMessagesEnUS,
  ...domainAiCapabilityMessagesEnUS,
  ...domainAiKnowledgeSecurityMessagesEnUS,
  ...domainPrintMessagesEnUS,
  ...domainSharedDomainMessagesEnUS,
};
