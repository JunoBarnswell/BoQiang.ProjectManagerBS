import { useEffect, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { ProjectManagementWorkbenchProvider, useProjectManagementWorkbenchContext } from '../state/ProjectManagementWorkbenchContext';
import { parseProjectManagementWorkbenchRoute } from '../state/projectManagementWorkbenchNavigation';

import { ProjectManagementWorkbenchSidebar } from './ProjectManagementWorkbenchSidebar';
import './projectManagementWorkbench.css';

export function ProjectManagementWorkbenchLayout({ children }: { children: ReactNode }) {
  return <ProjectManagementWorkbenchProvider><ProjectManagementWorkbenchLayoutContent>{children}</ProjectManagementWorkbenchLayoutContent></ProjectManagementWorkbenchProvider>;
}

function ProjectManagementWorkbenchLayoutContent({ children }: { children: ReactNode }) {
  const location = useLocation();
  const { translate } = useI18n();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const route = parseProjectManagementWorkbenchRoute(location.pathname);
  const { markPanelVisited } = useProjectManagementWorkbenchContext();

  useEffect(() => {
    markPanelVisited(`${route.area}:${route.projectId ?? ''}:${route.projectSection ?? ''}`);
  }, [markPanelVisited, route.area, route.projectId, route.projectSection]);

  return <section aria-label={translate('projectManagement.sidebar.ariaLabel')} className="responsive-page pm-workbench-page">
    <div className="pm-workbench">
      <ProjectManagementWorkbenchSidebar activeArea={route.area} mobileOpen={mobileMenuOpen} onCloseMobile={() => setMobileMenuOpen(false)} />
      <div className="pm-workbench__content">
        <button aria-label={translate('projectManagement.sidebar.openMobile')} className="pm-workbench__mobile-trigger" onClick={() => setMobileMenuOpen(true)} type="button">☰</button>
        <div className="responsive-page-content">{children}</div>
      </div>
    </div>
  </section>;
}
