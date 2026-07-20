import { useEffect, type ReactNode } from 'react';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { PmContent, PmPage, PmWorkbenchBody, ProjectManagementThemeProvider } from '../../../ui/project-management';
import { ProjectManagementWorkbenchProvider, useProjectManagementWorkbenchContext } from '../state/ProjectManagementWorkbenchContext';

export function ProjectManagementWorkbenchLayout({ children }: { children: ReactNode }) {
  return <ProjectManagementThemeProvider><ProjectManagementWorkbenchProvider><ProjectManagementWorkbenchLayoutContent>{children}</ProjectManagementWorkbenchLayoutContent></ProjectManagementWorkbenchProvider></ProjectManagementThemeProvider>;
}

function ProjectManagementWorkbenchLayoutContent({ children }: { children: ReactNode }) {
  const { translate } = useI18n();
  const { markPanelVisited } = useProjectManagementWorkbenchContext();

  useEffect(() => {
    markPanelVisited('projects');
  }, [markPanelVisited]);

  return <PmPage aria-label={translate('projectManagement.sidebar.ariaLabel')}>
      <PmContent><PmWorkbenchBody>{children}</PmWorkbenchBody></PmContent>
    </PmPage>;
}
