import { Bot, ChevronLeft } from 'lucide-react';
import type { ReactNode } from 'react';

import type { ApplicationDataSourceWorkbench } from '../../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../../shared/icons/AppIcon';
import { WorkspacePanel } from '../../../workspace-shell/WorkspacePanel';
import { WorkspaceToolbar } from '../../../workspace-shell/WorkspaceToolbar';
import type { WorkbenchTab, WorkbenchTabDefinition } from '../workbenchTypes';

import { WorkbenchTabs } from './WorkbenchTabs';

interface WorkbenchShellProps {
  activeTab: WorkbenchTab;
  children: ReactNode;
  onOpenAiWorkbench: () => void;
  loading: boolean;
  tabs: WorkbenchTabDefinition[];
  workbench: ApplicationDataSourceWorkbench | null;
  onBack: () => void;
  onTabChange: (tab: WorkbenchTab) => void;
}

export function WorkbenchShell({ activeTab, children, loading, onOpenAiWorkbench, tabs, workbench, onBack, onTabChange }: WorkbenchShellProps) {
  const dataSource = workbench?.dataSource ?? null;
  const activeTabDefinition = tabs.find((item) => item.key === activeTab) ?? null;
  const subtitle = loading
    ? '数据库工作台'
    : [dataSource?.objectCode, dataSource?.objectType].filter((item): item is string => Boolean(item && item.trim())).join(' / ');

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-2 overflow-hidden bg-transparent">
      <WorkspaceToolbar
        density="tight"
        actions={(
          <button className="primary-button h-8 rounded-lg shadow-[0_8px_20px_rgba(37,99,235,0.22)] text-xs" type="button" onClick={onOpenAiWorkbench}>
            <Bot className="h-3.5 w-3.5" />{translateCurrentLiteral("打开 AI 工具")}</button>
        )}
        icon={<AppIcon className="h-4 w-4" name="database" />}
        leading={(
          <button className="secondary-button h-8 text-xs" type="button" onClick={onBack}>
            <ChevronLeft className="h-3.5 w-3.5" />{translateCurrentLiteral("返回数据源列表")}</button>
        )}
        subtitle={subtitle}
        title={loading ? '数据库工作台' : dataSource?.objectName ?? '数据库工作台'}
      />

      <div className="flex min-h-0 flex-1 flex-col gap-2">
        <main className="min-h-0 space-y-2">
          <WorkspacePanel
            bodyClassName="p-1"
            description="表、视图、微流和映射缓存共享同一套数据库工作台语义。"
            title={activeTabDefinition ? activeTabDefinition.title : '数据库模块'}
          >
            <WorkbenchTabs active={activeTab} tabs={tabs} onChange={onTabChange} />
          </WorkspacePanel>
          <div className="min-h-0 overflow-y-auto overflow-x-hidden">
            <div className="min-w-0">{children}</div>
          </div>
        </main>
      </div>
    </div>
  );
}
