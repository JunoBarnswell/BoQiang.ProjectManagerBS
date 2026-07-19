import { useEffect, type ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import { usePermission } from '../../../core/auth/usePermission';
import { ProjectManagementGovernanceNav } from './ProjectManagementGovernanceNav';
import { ProjectManagementProjectNav } from './ProjectManagementProjectNav';
import { ProjectManagementWorkspaceNav } from './ProjectManagementWorkspaceNav';
import { ProjectManagementWorkbenchProvider, useProjectManagementWorkbenchContext } from '../state/ProjectManagementWorkbenchContext';
import { parseProjectManagementWorkbenchRoute, projectManagementWorkbenchPath, type ProjectManagementProjectSection, type ProjectManagementWorkbenchArea } from '../state/projectManagementWorkbenchNavigation';

export function ProjectManagementWorkbenchLayout({ children }: { children: ReactNode }) {
  return <ProjectManagementWorkbenchProvider><ProjectManagementWorkbenchLayoutContent>{children}</ProjectManagementWorkbenchLayoutContent></ProjectManagementWorkbenchProvider>;
}

function ProjectManagementWorkbenchLayoutContent({ children }: { children: ReactNode }) {
  const location = useLocation();
  const navigate = useNavigate();
  const route = parseProjectManagementWorkbenchRoute(location.pathname);
  const { markPanelVisited } = useProjectManagementWorkbenchContext();
  const { hasPermission: canUseAudit } = usePermission('project-management:audit:view');
  const { hasPermission: canUseSyncExport } = usePermission('project-management:sync:export');
  const { hasPermission: canUseSyncImport } = usePermission('project-management:sync:import');

  useEffect(() => {
    markPanelVisited(`${route.area}:${route.projectId ?? ''}:${route.projectSection ?? ''}`);
  }, [markPanelVisited, route.area, route.projectId, route.projectSection]);

  const navigateToArea = (area: ProjectManagementWorkbenchArea) => navigate(projectManagementWorkbenchPath({ area }));
  const navigateToProjectSection = (projectSection: ProjectManagementProjectSection) => {
    if (!route.projectId) return;
    navigate(projectManagementWorkbenchPath({ area: 'projects', projectId: route.projectId, projectSection }));
  };

  return <section aria-label="项目管理工作台" className="responsive-page px-3 py-3">
    <header className="responsive-toolbar">
      <div className="responsive-toolbar__title">{route.projectId ? '项目工作台' : null}</div>
      <div className="responsive-toolbar__actions">
        <ProjectManagementWorkspaceNav activeArea={route.area} onNavigate={navigateToArea} />
        <ProjectManagementGovernanceNav activeArea={route.area} canUseAudit={canUseAudit} canUseSync={canUseSyncExport || canUseSyncImport} onNavigate={navigateToArea} />
      </div>
      {route.projectId ? <div className="mt-2 pt-2 border-t border-gray-200"><ProjectManagementProjectNav activeSection={route.projectSection} onNavigate={navigateToProjectSection} /></div> : null}
    </header>
    <div className="responsive-page-content">{children}</div>
  </section>;
}
