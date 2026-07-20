import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import { getProjectManagementSavedViews } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementSavedView } from '../../../api/project-management/projectManagement.types';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useAuthStore } from '../../../core/state/authStore';
import { PmBox, PmIcon, PmNavigation, PmNavigationBrand, PmNavigationDrawer, PmNavigationItem, PmNavigationSectionLabel, PmText, usePmMediaQuery } from '../../../ui/project-management';
import type { ProjectManagementWorkbenchArea } from '../state/projectManagementWorkbenchNavigation';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

function SidebarContent({ activeArea, activeView, collapsed, onToggle, onNavigate, onViewNavigate, onSavedViewNavigate, savedViews, toggleLabel, viewsStorageKey }: { activeArea: ProjectManagementWorkbenchArea; activeView: string; collapsed: boolean; onToggle: () => void; onNavigate: () => void; onViewNavigate: (view: string) => void; onSavedViewNavigate: (view: ProjectManagementSavedView) => void; savedViews: ProjectManagementSavedView[]; toggleLabel: string; viewsStorageKey: string }) {
  const { translate } = useI18n();
  const [viewsOpen, setViewsOpen] = useState(() => readStorage(viewsStorageKey) !== 'false');
  useEffect(() => { setViewsOpen(readStorage(viewsStorageKey) !== 'false'); }, [viewsStorageKey]);
  const toggleViews = () => setViewsOpen(current => { const next = !current; writeStorage(viewsStorageKey, String(next)); return next; });
  return <PmNavigation collapsed={collapsed} onToggle={onToggle} toggleLabel={toggleLabel}>
    <PmNavigationBrand collapsed={collapsed} icon={<PmIcon name="briefcase" size={16} />} label="AsterERP" />
    <PmBox sx={{ display: 'grid', gap: .25 }}>
      <PmNavigationItem collapsed={collapsed} disabled icon={<PmIcon name="inbox" size={17} />} label={translate('projectManagement.sidebar.inbox')} />
      <PmNavigationItem collapsed={collapsed} disabled icon={<PmIcon name="check" size={17} />} label={translate('projectManagement.sidebar.myIssues')} />
      <PmNavigationItem collapsed={collapsed} disabled icon={<PmIcon name="gitPullRequest" size={17} />} label={translate('projectManagement.sidebar.reviews')} />
    </PmBox>
    {!collapsed && <PmNavigationSectionLabel>{translate('projectManagement.sidebar.workspace')}</PmNavigationSectionLabel>}
    <PmNavigationItem active={activeArea === 'projects'} collapsed={collapsed} icon={<PmIcon name="briefcase" size={17} />} label={translate('projectManagement.sidebar.projects')} onClick={onNavigate} />
    <PmNavigationItem active={activeView !== 'all'} collapsed={collapsed} icon={<PmIcon name="layers" size={17} />} label={translate('projectManagement.sidebar.views')} onClick={toggleViews} />
    {!collapsed && viewsOpen && <PmBox sx={{ display: 'grid', gap: .25, mt: .5 }}><PmNavigationItem active={activeView === 'all'} collapsed={collapsed} icon={<PmIcon name="briefcase" size={15} />} label={translate('projectManagement.sidebar.allProjects')} onClick={() => onViewNavigate('all')} /><PmNavigationItem active={activeView === 'my-projects'} collapsed={collapsed} icon={<PmIcon name="check" size={15} />} label={translate('projectManagement.sidebar.myProjects')} onClick={() => onViewNavigate('my-projects')} /><PmNavigationItem active={activeView === 'due-this-week'} collapsed={collapsed} icon={<PmIcon name="calendarDue" size={15} />} label={translate('projectManagement.sidebar.dueThisWeek')} onClick={() => onViewNavigate('due-this-week')} /><PmNavigationItem active={activeView === 'at-risk'} collapsed={collapsed} icon={<PmIcon name="alert" size={15} />} label={translate('projectManagement.sidebar.atRisk')} onClick={() => onViewNavigate('at-risk')} /><PmNavigationItem active={activeView === 'archived'} collapsed={collapsed} icon={<PmIcon name="archive" size={15} />} label={translate('projectManagement.sidebar.archived')} onClick={() => onViewNavigate('archived')} />{savedViews.map(view => <PmNavigationItem key={view.id} collapsed={collapsed} icon={<PmIcon name="layers" size={15} />} label={view.viewName} onClick={() => onSavedViewNavigate(view)} />)}<PmNavigationItem collapsed={collapsed} icon={<PmIcon name="plus" size={15} />} label={translate('projectManagement.sidebar.newView')} onClick={() => onNavigate()} /></PmBox>}
    {!collapsed && <PmBox sx={{ mt: 'auto', pt: 2 }}><PmText color="text.secondary" sx={{ px: 1, fontSize: '.72rem' }}>{translate('projectManagement.sidebar.help')}</PmText></PmBox>}
  </PmNavigation>;
}

export function ProjectManagementWorkbenchSidebar({ activeArea, mobileOpen, onCloseMobile }: { activeArea: ProjectManagementWorkbenchArea; mobileOpen: boolean; onCloseMobile: () => void }) {
  const navigate = useNavigate();
  const location = useLocation();
  const activeView = new URLSearchParams(location.search).get('view') || 'all';
  const { translate } = useI18n();
  const scope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore(state => state.user?.userId ?? 'anonymous');
  const mobileViewport = usePmMediaQuery('(max-width: 1023px)');
  const collapsedKey = `pm-workbench-sidebar-collapsed:${scope.tenantId}:${scope.appCode}:${userId}`;
  const viewsStorageKey = `pm-workbench-views-open:${scope.tenantId}:${scope.appCode}:${userId}`;
  const [collapsed, setCollapsed] = useState(() => readStorage(collapsedKey) === 'true');
  const savedViewsQuery = useQuery({ enabled: scope.isAvailable, queryKey: projectManagementQueryKeys.savedViews(scope, '__home__'), queryFn: ({ signal }) => getProjectManagementSavedViews('__home__', signal) });
  const toggle = () => setCollapsed(current => { const next = !current; writeStorage(collapsedKey, String(next)); return next; });
  useEffect(() => { setCollapsed(readStorage(collapsedKey) === 'true'); }, [collapsedKey]);
  useEffect(() => { if (mobileOpen) onCloseMobile(); }, [location.pathname, mobileOpen, onCloseMobile]);
  const applySavedView = (view: ProjectManagementSavedView) => {
    try {
      const saved = JSON.parse(view.queryJson) as Record<string, unknown>;
      const params = new URLSearchParams();
      for (const key of ['collection', 'view', 'sortBy', 'sortDirection', 'density', 'insights', 'insightsTab']) {
        const value = saved[key];
        if (value !== undefined && value !== null) params.set(key, String(value));
      }
      if (saved.filter) params.set('filter', JSON.stringify(saved.filter));
      if (Array.isArray(saved.columns)) params.set('columns', saved.columns.join(','));
      params.set('savedView', view.id);
      navigate(`/platform/project-management?${params.toString()}`);
    } catch {
      navigate(`/platform/project-management?view=all`);
    }
  };
  const content = <SidebarContent activeArea={activeArea} activeView={activeView} collapsed={collapsed} onNavigate={() => navigate('/platform/project-management?saveView=true')} onSavedViewNavigate={applySavedView} onViewNavigate={view => navigate(`/platform/project-management?view=${encodeURIComponent(view)}`)} savedViews={savedViewsQuery.data?.data ?? []} onToggle={toggle} toggleLabel={translate(collapsed ? 'projectManagement.sidebar.expand' : 'projectManagement.sidebar.collapse')} viewsStorageKey={viewsStorageKey} />;
  return <>{!mobileViewport && <PmBox component="aside" sx={{ flex: '0 0 auto' }}>{content}</PmBox>}<PmNavigationDrawer onClose={onCloseMobile} open={mobileOpen}>{content}</PmNavigationDrawer></>;
}

function readStorage(key: string): string | null {
  try {
    return typeof window === 'undefined' ? null : window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStorage(key: string, value: string): void {
  try {
    if (typeof window !== 'undefined') window.localStorage.setItem(key, value);
  } catch {
    // 浏览器禁用存储时，导航仍保持可用。
  }
}
