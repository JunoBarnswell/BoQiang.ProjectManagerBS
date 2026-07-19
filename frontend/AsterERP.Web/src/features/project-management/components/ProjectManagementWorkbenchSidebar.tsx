import { Box, Drawer, Typography } from '@mui/material';
import { IconBriefcase2, IconCheck, IconChevronDown, IconGitPullRequest, IconInbox, IconLayersSubtract, IconLayoutSidebarLeftExpand } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { PmNavigation, PmNavigationItem } from '../../../ui/project-management';
import type { ProjectManagementWorkbenchArea } from '../state/projectManagementWorkbenchNavigation';

const collapsedKey = 'pm-workbench-sidebar-collapsed';

function SidebarContent({ activeArea, collapsed, onToggle, onNavigate, toggleLabel }: { activeArea: ProjectManagementWorkbenchArea; collapsed: boolean; onToggle: () => void; onNavigate: () => void; toggleLabel: string }) {
  const { translate } = useI18n();
  return <PmNavigation collapsed={collapsed} onToggle={onToggle} toggleLabel={toggleLabel}>
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1, pb: 2, minHeight: 42, overflow: 'hidden' }}>
      <Box sx={{ display: 'grid', placeItems: 'center', width: 28, height: 28, flex: '0 0 28px', borderRadius: 1.5, bgcolor: 'primary.main', color: 'primary.contrastText' }}><IconBriefcase2 size={16} /></Box>
      {!collapsed && <Typography noWrap sx={{ fontSize: '.82rem', fontWeight: 700 }}>AsterERP</Typography>}
    </Box>
    <Box sx={{ display: 'grid', gap: .25 }}>
      <PmNavigationItem disabled icon={<IconInbox size={17} />} label={translate('projectManagement.sidebar.inbox')} />
      <PmNavigationItem disabled icon={<IconCheck size={17} />} label={translate('projectManagement.sidebar.myIssues')} />
      <PmNavigationItem disabled icon={<IconGitPullRequest size={17} />} label={translate('projectManagement.sidebar.reviews')} />
    </Box>
    {!collapsed && <Typography color="text.secondary" sx={{ mt: 2.5, mb: .5, px: 1, fontSize: '.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '.04em' }}>{translate('projectManagement.sidebar.workspace')}</Typography>}
    <PmNavigationItem active={activeArea === 'projects'} icon={<IconBriefcase2 size={17} />} label={translate('projectManagement.sidebar.projects')} onClick={() => { onNavigate(); }} />
    <PmNavigationItem disabled icon={<IconLayersSubtract size={17} />} label={translate('projectManagement.sidebar.views')} />
    {!collapsed && <Box sx={{ display: 'flex', alignItems: 'center', gap: .5, px: 1, mt: 2.5, color: 'text.secondary' }}><IconChevronDown size={14} /><Typography sx={{ fontSize: '.72rem' }}>{translate('projectManagement.sidebar.workspace')}</Typography></Box>}
    {collapsed && <Box sx={{ display: 'grid', placeItems: 'center', mt: 2 }}><IconLayoutSidebarLeftExpand size={16} /></Box>}
    {!collapsed && <Box sx={{ mt: 'auto', pt: 2 }}><Typography color="text.secondary" sx={{ px: 1, fontSize: '.72rem' }}>{translate('projectManagement.sidebar.help')}</Typography></Box>}
  </PmNavigation>;
}

export function ProjectManagementWorkbenchSidebar({ activeArea, mobileOpen, onCloseMobile }: { activeArea: ProjectManagementWorkbenchArea; mobileOpen: boolean; onCloseMobile: () => void }) {
  const navigate = useNavigate();
  const location = useLocation();
  const { translate } = useI18n();
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(collapsedKey) === 'true');
  const toggle = () => setCollapsed(current => { const next = !current; localStorage.setItem(collapsedKey, String(next)); return next; });
  useEffect(() => { if (mobileOpen) onCloseMobile(); }, [location.pathname, mobileOpen, onCloseMobile]);
  const content = <SidebarContent activeArea={activeArea} collapsed={collapsed} onNavigate={() => navigate('/platform/project-management')} onToggle={toggle} toggleLabel={translate(collapsed ? 'projectManagement.sidebar.expand' : 'projectManagement.sidebar.collapse')} />;
  return <><Box component="aside" sx={{ display: { xs: 'none', md: 'block' }, flex: '0 0 auto' }}>{content}</Box><Drawer ModalProps={{ keepMounted: true }} onClose={onCloseMobile} open={mobileOpen} sx={{ display: { xs: 'block', md: 'none' } }}>{content}</Drawer></>;
}
