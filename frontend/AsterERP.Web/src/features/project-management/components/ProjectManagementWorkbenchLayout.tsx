import { useEffect, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { PmContent, PmMobileTrigger, PmPage, PmWorkbenchBody, ProjectManagementThemeProvider } from '../../../ui/project-management';
import { ProjectManagementWorkbenchProvider, useProjectManagementWorkbenchContext } from '../state/ProjectManagementWorkbenchContext';
import { parseProjectManagementWorkbenchRoute } from '../state/projectManagementWorkbenchNavigation';

import { ProjectManagementWorkbenchSidebar } from './ProjectManagementWorkbenchSidebar';
export function ProjectManagementWorkbenchLayout({ children }: { children: ReactNode }) {
  return <ProjectManagementThemeProvider><ProjectManagementWorkbenchProvider><ProjectManagementWorkbenchLayoutContent>{children}</ProjectManagementWorkbenchLayoutContent></ProjectManagementWorkbenchProvider></ProjectManagementThemeProvider>;
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

  return <PmPage aria-label={translate('projectManagement.sidebar.ariaLabel')}>
      <ProjectManagementWorkbenchSidebar activeArea={route.area} mobileOpen={mobileMenuOpen} onCloseMobile={() => setMobileMenuOpen(false)} />
      <PmContent>
        <PmMobileTrigger aria-label={translate('projectManagement.sidebar.openMobile')} onClick={() => setMobileMenuOpen(true)} type="button">☰</PmMobileTrigger>
        <PmWorkbenchBody>{children}</PmWorkbenchBody>
      </PmContent>
    </PmPage>;
}
