import type { ProjectManagementWorkbenchArea } from '../state/projectManagementWorkbenchNavigation';

interface WorkspaceNavigationItem {
  area: Extract<ProjectManagementWorkbenchArea, 'projects' | 'my-work' | 'search'>;
  label: string;
}

const items: WorkspaceNavigationItem[] = [
  { area: 'projects', label: '项目中心' },
  { area: 'my-work', label: '我的工作' },
  { area: 'search', label: '搜索' },
];

export function ProjectManagementWorkspaceNav({ activeArea, onNavigate }: { activeArea: ProjectManagementWorkbenchArea; onNavigate: (area: WorkspaceNavigationItem['area']) => void }) {
  return <nav aria-label="项目管理工作区" className="flex flex-wrap gap-2">{items.map((item) => <button aria-current={activeArea === item.area ? 'page' : undefined} className={activeArea === item.area ? 'primary-button' : 'ghost-button'} key={item.area} onClick={() => onNavigate(item.area)} type="button">{item.label}</button>)}</nav>;
}
