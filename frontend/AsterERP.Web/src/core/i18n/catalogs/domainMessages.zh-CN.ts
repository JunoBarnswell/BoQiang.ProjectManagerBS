import { applicationConsoleMessagesZhCN } from './applicationConsoleMessages.zh-CN';
import { domainAiCapabilityMessagesZhCN } from './domain/zh-CN/aiCapabilityMessages';
import { domainAiKnowledgeSecurityMessagesZhCN } from './domain/zh-CN/aiKnowledgeSecurityMessages';
import { domainAiWorkbenchMessagesZhCN } from './domain/zh-CN/aiWorkbenchMessages';
import { domainDashboardMessagesZhCN } from './domain/zh-CN/dashboardMessages';
import { domainPlatformMessagesZhCN } from './domain/zh-CN/platformMessages';
import { domainPrintMessagesZhCN } from './domain/zh-CN/printMessages';
import { domainSharedDomainMessagesZhCN } from './domain/zh-CN/sharedDomainMessages';
import { domainSystemIdentityMessagesZhCN } from './domain/zh-CN/systemIdentityMessages';
import { domainSystemInfrastructureMessagesZhCN } from './domain/zh-CN/systemInfrastructureMessages';
import { domainSystemOperationsMessagesZhCN } from './domain/zh-CN/systemOperationsMessages';
import { domainWorkflowBusinessMessagesZhCN } from './domain/zh-CN/workflowBusinessMessages';
import { domainWorkflowNotificationsMessagesZhCN } from './domain/zh-CN/workflowNotificationsMessages';
import { domainWorkflowPagesMessagesZhCN } from './domain/zh-CN/workflowPagesMessages';
import { domainWorkflowProcessMessagesZhCN } from './domain/zh-CN/workflowProcessMessages';
import { domainWorkflowTasksMessagesZhCN } from './domain/zh-CN/workflowTasksMessages';
import { lowCodeStudioMessagesZhCN } from './lowCodeStudioMessages.zh-CN';
import { systemMessagesZhCN } from './systemMessages.zh-CN';
import { systemMessagesExtraZhCN } from './systemMessagesExtra.zh-CN';

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
  ...domainSharedDomainMessagesZhCN
};
