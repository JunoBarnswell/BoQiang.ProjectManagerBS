import {
  BookOpen,
  BriefcaseBusiness,
  Building2,
  CalendarClock,
  ClipboardList,
  Clock3,
  Database,
  KeyRound,
  Megaphone,
  MenuSquare,
  ScrollText,
  ShieldCheck,
  SlidersHorizontal,
  Users,
  Workflow,
  Wrench
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

import type { AiKernelFunctionDefinitionDto } from '.././api/aiCenter.api';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { translateCurrentLiteral, useI18n } from '../../../core/i18n/I18nProvider';

interface AgentToolCapabilitySelectorProps {
  selectedDomains: string[];
  selectedToolCodes: string[];
  tools: AiKernelFunctionDefinitionDto[];
  workMode: 'Agent' | 'Ask' | 'Plan';
  onToggleDomain: (domain: string, selected: boolean) => void;
  onToolCodesChange: (toolCodes: string[]) => void;
}

interface SystemToolGroup {
  icon: LucideIcon;
  labelKey: string;
  prefixes: string[];
}

const systemToolGroups: SystemToolGroup[] = [
  { icon: Users, labelKey: 'ai.workflowSupport.agentToolCapability.system.userManagement', prefixes: ['system.user.'] },
  { icon: Building2, labelKey: 'ai.workflowSupport.agentToolCapability.system.departmentManagement', prefixes: ['system.department.'] },
  { icon: BriefcaseBusiness, labelKey: 'ai.workflowSupport.agentToolCapability.system.positionManagement', prefixes: ['system.position.'] },
  { icon: MenuSquare, labelKey: 'ai.workflowSupport.agentToolCapability.system.menuManagement', prefixes: ['system.menu.'] },
  { icon: ShieldCheck, labelKey: 'ai.workflowSupport.agentToolCapability.system.roleManagement', prefixes: ['system.role.'] },
  { icon: BookOpen, labelKey: 'ai.workflowSupport.agentToolCapability.system.dictManagement', prefixes: ['system.dict.'] },
  { icon: SlidersHorizontal, labelKey: 'ai.workflowSupport.agentToolCapability.system.parameterManagement', prefixes: ['system.parameter.'] },
  { icon: Megaphone, labelKey: 'ai.workflowSupport.agentToolCapability.system.announcementManagement', prefixes: ['system.announcement.'] },
  { icon: ScrollText, labelKey: 'ai.workflowSupport.agentToolCapability.system.operationLogManagement', prefixes: ['system.operationLog.'] },
  { icon: ClipboardList, labelKey: 'ai.workflowSupport.agentToolCapability.system.loginLogManagement', prefixes: ['system.loginLog.'] },
  { icon: Clock3, labelKey: 'ai.workflowSupport.agentToolCapability.system.onlineUsers', prefixes: ['system.onlineUser.'] },
  { icon: CalendarClock, labelKey: 'ai.workflowSupport.agentToolCapability.system.scheduledJobs', prefixes: ['system.scheduledJob.'] }
];

export function AgentToolCapabilitySelector({
  selectedDomains,
  selectedToolCodes,
  tools,
  workMode,
  onToggleDomain,
  onToolCodesChange
}: AgentToolCapabilitySelectorProps) {
  const { translate } = useI18n();
  const workflowTools = tools.filter((item) => item.toolDomain === 'workflow' || item.toolCode.startsWith('workflow.'));
  const workflowSelected = selectedDomains.includes('workflow');
  const systemTools = tools.filter((item) => item.toolDomain === 'system-admin' || item.toolCode.startsWith('system.'));
  const dataCenterTools = tools.filter((item) => item.toolDomain === 'data-center' || item.toolCode.startsWith('dataCenter.'));
  const dataCenterSelected = selectedDomains.includes('data-center');
  const selectedCodes = new Set(selectedToolCodes);
  const disabled = workMode !== 'Agent';
  const selectedSystemCount = systemTools.filter((tool) => selectedCodes.has(tool.toolCode)).length;
  const workflowSupportKeys = {
    agentModeHint: 'ai.workflowSupport.agentToolCapability.mode.agent',
    noToolsRegistered: 'ai.workflowSupport.agentToolCapability.noToolsRegistered',
    nonAgentModeHint: 'ai.workflowSupport.agentToolCapability.mode.nonAgent',
    permissionSummary: 'ai.workflowSupport.agentToolCapability.permissionSummary',
    systemLabel: 'ai.workflowSupport.agentToolCapability.systemLabel',
    systemToolCount: 'ai.workflowSupport.agentToolCapability.systemToolCount',
    title: 'ai.workflowSupport.agentToolCapability.title',
    workflowLabel: 'ai.workflowSupport.agentToolCapability.workflowLabel',
    workflowSummary: 'ai.workflowSupport.agentToolCapability.workflowSummary',
    maxRiskSummary: 'ai.workflowSupport.agentToolCapability.maxRiskSummary'
  } as const;

  return (
    <section className="ai-tool-capability-section">
      <h3>
        <Wrench size={16} />
        {translate(workflowSupportKeys.title)}
      </h3>
      <label className={`ai-tool-capability-item ${workflowSelected ? 'ai-tool-capability-item--selected' : ''}`}>
        <input
          checked={workflowSelected}
          disabled={disabled}
          type="checkbox"
          onChange={(event) => onToggleDomain('workflow', event.target.checked)}
        />
        <span className="ai-tool-capability-item__icon">
          <Workflow size={15} />
        </span>
        <span className="ai-tool-capability-item__body">
          <strong>{translate(workflowSupportKeys.workflowLabel)}</strong>
          <small>{formatMessage(translate(workflowSupportKeys.workflowSummary), {
            count: workflowTools.length,
            permissions: summarizePermissions(workflowTools, translate, workflowSupportKeys),
            risk: summarizeRisks(workflowTools, translate, workflowSupportKeys)
          })}</small>
        </span>
      </label>

      <div className="ai-system-tool-group">
        <div className="ai-system-tool-group__header">
          <span>
            <KeyRound size={14} />
            {translate(workflowSupportKeys.systemLabel)}
          </span>
          <small>{formatMessage(translate(workflowSupportKeys.systemToolCount), { selected: selectedSystemCount, total: systemTools.length })}</small>
        </div>
        <div className="ai-system-tool-grid">
          {systemToolGroups.map((group) => {
            const groupTools = resolveGroupTools(systemTools, group);
            const groupCodes = groupTools.map((tool) => tool.toolCode);
            const selectedCount = groupCodes.filter((code) => selectedCodes.has(code)).length;
            const selected = groupCodes.length > 0 && selectedCount === groupCodes.length;
            const partial = selectedCount > 0 && !selected;
            const Icon = group.icon;
            return (
              <label
                className={[
                  'ai-system-tool-item',
                  selected ? 'ai-system-tool-item--selected' : '',
                  partial ? 'ai-system-tool-item--partial' : ''
                ].filter(Boolean).join(' ')}
                key={group.labelKey}
              >
                <input
                  checked={selected}
                  disabled={disabled || groupCodes.length === 0}
                  type="checkbox"
                  onChange={(event) => onToggleGroup(groupCodes, event.target.checked, selectedCodes, onToolCodesChange)}
                />
                <span className="ai-system-tool-item__icon">
                  <Icon size={14} />
                </span>
                <span className="ai-system-tool-item__body">
                  <strong>{translate(group.labelKey)}</strong>
                  <small>{formatMessage(translate(workflowSupportKeys.workflowSummary), {
                    count: groupTools.length,
                    permissions: summarizePermissions(groupTools, translate, workflowSupportKeys),
                    risk: summarizeRisks(groupTools, translate, workflowSupportKeys)
                  })}</small>
                </span>
              </label>
            );
          })}
        </div>
      </div>

      <label className={`ai-tool-capability-item ${dataCenterSelected ? 'ai-tool-capability-item--selected' : ''}`}>
        <input
          checked={dataCenterSelected}
          disabled={disabled || dataCenterTools.length === 0}
          type="checkbox"
          onChange={(event) => onToggleDomain('data-center', event.target.checked)}
        />
        <span className="ai-tool-capability-item__icon">
          <Database size={15} />
        </span>
        <span className="ai-tool-capability-item__body">
          <strong>{translateCurrentLiteral("数据中心工具")}</strong>
          <small>{formatMessage(translate(workflowSupportKeys.workflowSummary), {
            count: dataCenterTools.length,
            permissions: summarizePermissions(dataCenterTools, translate, workflowSupportKeys),
            risk: summarizeRisks(dataCenterTools, translate, workflowSupportKeys)
          })}</small>
        </span>
      </label>

      <p className="ai-muted-text">
        {workMode === 'Agent'
          ? translate(workflowSupportKeys.agentModeHint)
          : translate(workflowSupportKeys.nonAgentModeHint)}
      </p>
    </section>
  );
}

function resolveGroupTools(tools: AiKernelFunctionDefinitionDto[], group: SystemToolGroup) {
  return tools.filter((tool) => group.prefixes.some((prefix) => tool.toolCode.startsWith(prefix)));
}

function onToggleGroup(
  groupCodes: string[],
  selected: boolean,
  selectedCodes: Set<string>,
  onToolCodesChange: (toolCodes: string[]) => void
) {
  const next = new Set(selectedCodes);
  for (const code of groupCodes) {
    if (selected) {
      next.add(code);
    } else {
      next.delete(code);
    }
  }

  onToolCodesChange(Array.from(next));
}

function summarizeRisks(
  tools: AiKernelFunctionDefinitionDto[],
  translate: (key: string) => string,
  workflowSupportKeys: {
    maxRiskSummary: string;
    noToolsRegistered: string;
  }
) {
  if (tools.length === 0) {
    return translate(workflowSupportKeys.noToolsRegistered);
  }

  const maxRisk = tools
    .map((tool) => Number(tool.riskLevel.replace(/^L/i, '')))
    .filter((value) => Number.isFinite(value))
    .reduce((max, value) => Math.max(max, value), 0);
  return formatMessage(translate(workflowSupportKeys.maxRiskSummary), { level: maxRisk });
}

function summarizePermissions(
  tools: AiKernelFunctionDefinitionDto[],
  translate: (key: string) => string,
  workflowSupportKeys: {
    permissionSummary: string;
  }
) {
  const permissionCodes = new Set<string>();
  for (const tool of tools) {
    for (const code of tool.requiredPermissionCodes ?? []) {
      if (code) {
        permissionCodes.add(code);
      }
    }
  }

  return formatMessage(translate(workflowSupportKeys.permissionSummary), { count: permissionCodes.size });
}
