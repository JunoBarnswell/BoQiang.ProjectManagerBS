import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { projectManagementWorkbenchPath, type ProjectManagementWorkbenchArea } from '../state/projectManagementWorkbenchNavigation';

interface ProjectManagementWorkbenchSidebarProps {
  activeArea: ProjectManagementWorkbenchArea;
  mobileOpen: boolean;
  onCloseMobile: () => void;
}

const sidebarStorageKey = 'pm-workbench-sidebar-collapsed';

export function ProjectManagementWorkbenchSidebar({ activeArea, mobileOpen, onCloseMobile }: ProjectManagementWorkbenchSidebarProps) {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const [collapsed, setCollapsed] = useState(() => window.localStorage.getItem(sidebarStorageKey) === 'true');

  const toggleCollapsed = () => {
    const next = !collapsed;
    setCollapsed(next);
    window.localStorage.setItem(sidebarStorageKey, String(next));
  };

  const goToProjects = () => {
    navigate(projectManagementWorkbenchPath({ area: 'projects' }));
    onCloseMobile();
  };

  return <>
    {mobileOpen ? <button aria-label={translate('projectManagement.sidebar.collapse')} className="pm-workbench__overlay" onClick={onCloseMobile} type="button" /> : null}
    <aside className={`pm-workbench__sidebar ${collapsed ? 'pm-workbench__sidebar--collapsed' : ''} ${mobileOpen ? 'pm-workbench__sidebar--mobile-open' : ''}`}>
      <div className="pm-workbench__sidebar-inner">
        <div className="pm-workbench__primary-nav">
          <div className="pm-workbench__nav-item pm-workbench__nav-item--inert"><AppIcon name="inbox" /><span>{translate('projectManagement.sidebar.inbox')}</span></div>
          <div className="pm-workbench__nav-item pm-workbench__nav-item--inert"><AppIcon name="check-circle" /><span>{translate('projectManagement.sidebar.myIssues')}</span></div>
          <div className="pm-workbench__nav-item pm-workbench__nav-item--inert"><AppIcon name="git-branch" /><span>{translate('projectManagement.sidebar.reviews')}</span></div>
        </div>
        <div className="pm-workbench__workspace-heading"><span>{translate('projectManagement.sidebar.workspace')}</span><span aria-hidden="true">▾</span></div>
        <nav aria-label={translate('projectManagement.sidebar.workspace')} className="pm-workbench__workspace-nav">
          <button aria-current={activeArea === 'projects' ? 'page' : undefined} className={`pm-workbench__nav-item ${activeArea === 'projects' ? 'is-active' : ''}`} onClick={goToProjects} type="button"><AppIcon name="cube" /><span>{translate('projectManagement.sidebar.projects')}</span></button>
          <div aria-disabled="true" className="pm-workbench__nav-item pm-workbench__nav-item--inert"><AppIcon name="layers" /><span>{translate('projectManagement.sidebar.views')}</span></div>
        </nav>
      </div>
      <button aria-label={translate(collapsed ? 'projectManagement.sidebar.expand' : 'projectManagement.sidebar.collapse')} className="pm-workbench__collapse" onClick={toggleCollapsed} type="button"><AppIcon name={collapsed ? 'sidebarOpen' : 'sidebarClose'} /></button>
    </aside>
  </>;
}
