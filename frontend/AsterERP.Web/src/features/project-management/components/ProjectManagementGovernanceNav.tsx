import type { ProjectManagementWorkbenchArea } from '../state/projectManagementWorkbenchNavigation';

interface GovernanceNavigationItem {
  area: Extract<ProjectManagementWorkbenchArea, 'recycle-bin' | 'sync' | 'audit'>;
  label: string;
  visible: boolean;
}

export function ProjectManagementGovernanceNav({ activeArea, canUseAudit, canUseSync, onNavigate }: { activeArea: ProjectManagementWorkbenchArea; canUseAudit: boolean; canUseSync: boolean; onNavigate: (area: GovernanceNavigationItem['area']) => void }) {
  const items: GovernanceNavigationItem[] = [
    { area: 'recycle-bin', label: '回收站', visible: true },
    { area: 'sync', label: '同步', visible: canUseSync },
    { area: 'audit', label: '审计', visible: canUseAudit },
  ];
  return <nav aria-label="项目治理" className="flex flex-wrap gap-2">{items.filter((item) => item.visible).map((item) => <button aria-current={activeArea === item.area ? 'page' : undefined} className={activeArea === item.area ? 'primary-button' : 'ghost-button'} key={item.area} onClick={() => onNavigate(item.area)} type="button">{item.label}</button>)}</nav>;
}
