import { describe, expect, it, vi } from 'vitest';

vi.mock('@/app/navigation/routes', () => ({
  appRoutes: [
    { labelKey: 'nav.flowiseExecutions', path: '/flowise/executions' },
    { labelKey: 'nav.flowiseEvaluations', path: '/flowise/evaluation-results/:id' },
    { labelKey: 'nav.flowiseChatflows', path: '/flowise/chatflows' }
  ]
}));

import { getMenuLabelKey, resolveMenuLabel } from './menuLabels';

const translate =
  (dictionary: Record<string, string>) =>
    (key: string) =>
      dictionary[key] ?? key;

describe('menuLabels', () => {
  it('resolves Flowise route labels and directory labels through i18n keys', () => {
    expect(getMenuLabelKey({ menuCode: 'flowise', routePath: null })).toBe('nav.flowiseStudio');
    expect(getMenuLabelKey({ menuCode: 'flowise:evaluations-group', routePath: null })).toBe('nav.flowiseEvaluationsGroup');
    expect(getMenuLabelKey({ menuCode: 'flowise:executions', routePath: '/flowise/executions' })).toBe('nav.flowiseExecutions');
    expect(getMenuLabelKey({ menuCode: 'flowise:dataset-row', routePath: '/flowise/evaluation-results/123' })).toBe('nav.flowiseEvaluations');
  });

  it('resolves workflow directory and query route labels through i18n keys', () => {
    expect(getMenuLabelKey({ menuCode: 'workflow', routePath: null })).toBe('nav.workflowRoot');
    expect(getMenuLabelKey({ menuCode: 'workflow:workspace', routePath: null })).toBe('nav.workflowWorkspaceGroup');
    expect(getMenuLabelKey({ menuCode: 'workflow:management', routePath: null })).toBe('nav.workflowManagementGroup');
    expect(getMenuLabelKey({ menuCode: 'workflow:analytics', routePath: null })).toBe('nav.workflowAnalyticsGroup');
    expect(getMenuLabelKey({ menuCode: 'workflow:settings', routePath: null })).toBe('nav.workflowSettingsGroup');
    expect(getMenuLabelKey({ menuCode: 'workflow:bindings', routePath: '/workflows/bindings' })).toBe('nav.workflowBindings');
    expect(getMenuLabelKey({ menuCode: 'workflow:todo', routePath: '/workflows/tasks?tab=todo' })).toBe('nav.workflowTaskTodo');
    expect(getMenuLabelKey({ menuCode: 'workflow:done', routePath: '/workflows/tasks?tab=done' })).toBe('nav.workflowTaskDone');
    expect(getMenuLabelKey({ menuCode: 'workflow:mine', routePath: '/workflows/tasks?tab=mine' })).toBe('nav.workflowTaskMine');
    expect(getMenuLabelKey({ menuCode: 'workflow:cc', routePath: '/workflows/tasks?tab=cc' })).toBe('nav.workflowTaskCc');
    expect(getMenuLabelKey({ menuCode: 'workflow:report:approval', routePath: '/workflows/reports?tab=approval' })).toBe('nav.workflowReportApproval');
    expect(getMenuLabelKey({ menuCode: 'workflow:report:efficiency', routePath: '/workflows/reports?tab=efficiency' })).toBe('nav.workflowReportEfficiency');
    expect(getMenuLabelKey({ menuCode: 'workflow:report:business', routePath: '/workflows/reports?tab=business' })).toBe('nav.workflowReportBusiness');
    expect(getMenuLabelKey({ menuCode: 'workflow:settings:org', routePath: '/system/users?from=workflow' })).toBe('nav.workflowSettingsOrg');
    expect(getMenuLabelKey({ menuCode: 'workflow:settings:roles', routePath: '/system/roles?from=workflow' })).toBe('nav.workflowSettingsRoles');
  });

  it('renders localized Flowise menu labels for zh-CN and en-US', () => {
    const zh = translate({
      'nav.flowiseStudio': 'Flowise',
      'nav.flowiseEvaluationsGroup': '评估',
      'nav.flowiseExecutions': '执行记录',
      'nav.workflowRoot': '审批流',
      'nav.workflowTaskTodo': '待办审批'
    });
    const en = translate({
      'nav.flowiseStudio': 'Flowise',
      'nav.flowiseEvaluationsGroup': 'Evaluations',
      'nav.flowiseExecutions': 'Executions',
      'nav.workflowRoot': 'Workflow',
      'nav.workflowTaskTodo': 'To-do Approvals'
    });

    expect(resolveMenuLabel({ menuCode: 'flowise', menuName: 'Flowise', routePath: null }, zh)).toBe('Flowise');
    expect(resolveMenuLabel({ menuCode: 'flowise:evaluations-group', menuName: 'Evaluations', routePath: null }, zh)).toBe('评估');
    expect(resolveMenuLabel({ menuCode: 'flowise:executions', menuName: 'Executions', routePath: '/flowise/executions' }, zh)).toBe('执行记录');
    expect(resolveMenuLabel({ menuCode: 'flowise:executions', menuName: 'Executions', routePath: '/flowise/executions' }, en)).toBe('Executions');
    expect(resolveMenuLabel({ menuCode: 'workflow', menuName: '审批流', routePath: null }, zh)).toBe('审批流');
    expect(resolveMenuLabel({ menuCode: 'workflow:todo', menuName: '待办审批', routePath: '/workflows/tasks?tab=todo' }, en)).toBe('To-do Approvals');
  });
});
