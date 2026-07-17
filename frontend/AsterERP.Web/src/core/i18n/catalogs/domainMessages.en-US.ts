import { applicationConsoleMessagesEnUS } from './applicationConsoleMessages.en-US';
import { domainAiCapabilityMessagesEnUS } from './domain/en-US/aiCapabilityMessages';
import { domainAiKnowledgeSecurityMessagesEnUS } from './domain/en-US/aiKnowledgeSecurityMessages';
import { domainAiWorkbenchMessagesEnUS } from './domain/en-US/aiWorkbenchMessages';
import { domainDashboardMessagesEnUS } from './domain/en-US/dashboardMessages';
import { domainPlatformMessagesEnUS } from './domain/en-US/platformMessages';
import { domainPrintMessagesEnUS } from './domain/en-US/printMessages';
import { domainSharedDomainMessagesEnUS } from './domain/en-US/sharedDomainMessages';
import { domainSystemIdentityMessagesEnUS } from './domain/en-US/systemIdentityMessages';
import { domainSystemInfrastructureMessagesEnUS } from './domain/en-US/systemInfrastructureMessages';
import { domainSystemOperationsMessagesEnUS } from './domain/en-US/systemOperationsMessages';
import { domainWorkflowBusinessMessagesEnUS } from './domain/en-US/workflowBusinessMessages';
import { domainWorkflowNotificationsMessagesEnUS } from './domain/en-US/workflowNotificationsMessages';
import { domainWorkflowPagesMessagesEnUS } from './domain/en-US/workflowPagesMessages';
import { domainWorkflowProcessMessagesEnUS } from './domain/en-US/workflowProcessMessages';
import { domainWorkflowTasksMessagesEnUS } from './domain/en-US/workflowTasksMessages';
import { lowCodeStudioMessagesEnUS } from './lowCodeStudioMessages.en-US';
import { systemMessagesEnUS } from './systemMessages.en-US';
import { systemMessagesExtraEnUS } from './systemMessagesExtra.en-US';

type MessageBag = Record<string, string>;

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
  ...domainSharedDomainMessagesEnUS
};
