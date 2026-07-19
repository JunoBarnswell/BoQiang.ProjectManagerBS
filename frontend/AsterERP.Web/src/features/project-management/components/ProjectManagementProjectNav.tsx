import type { ProjectManagementProjectSection } from '../state/projectManagementWorkbenchNavigation';

const items: Array<{ label: string; section: ProjectManagementProjectSection }> = [
  { label: '概览', section: 'overview' },
  { label: '任务', section: 'tasks' },
  { label: '里程碑', section: 'milestones' },
  { label: '成员', section: 'members' },
  { label: '报表', section: 'reports' },
  { label: '设置', section: 'settings' },
];

export function ProjectManagementProjectNav({ activeSection, onNavigate }: { activeSection?: ProjectManagementProjectSection; onNavigate: (section: ProjectManagementProjectSection) => void }) {
  return <nav aria-label="当前项目" className="flex flex-wrap gap-2">{items.map((item) => <button aria-current={activeSection === item.section ? 'page' : undefined} className={activeSection === item.section ? 'primary-button' : 'ghost-button'} key={item.section} onClick={() => onNavigate(item.section)} type="button">{item.label}</button>)}</nav>;
}
