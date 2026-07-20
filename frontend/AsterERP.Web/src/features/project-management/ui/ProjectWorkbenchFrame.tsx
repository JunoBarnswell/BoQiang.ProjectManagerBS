import { Box, Divider, Stack, Tooltip, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import { PmIcon } from '../../../ui/project-management';
import { useProjectManagementI18n } from '../projectManagementI18n';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import './projectWorkbench.css';

export function ProjectWorkbenchFrame({ children, active }: { children: ReactNode; active: 'overview' | 'requirements' }) {
  const { t } = useProjectManagementI18n();
  const navigate = useNavigate();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const overviewPath = toProjectManagementPlatformRoute(`projects/${encodeURIComponent(projectId)}/overview`);
  const requirementsPath = toProjectManagementPlatformRoute(`projects/${encodeURIComponent(projectId)}/requirements`);

  return <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, width: '100%', bgcolor: '#f7f8fb' }}>
    <Stack alignItems="center" spacing={1} sx={{ width: 48, py: 1.5, bgcolor: '#fff', borderRight: '1px solid #e7eaf0' }}>
      <Tooltip title={t('projectManagement.workbench.back')}><span><button aria-label={t('projectManagement.workbench.back')} className="pm-workbench-rail-button" onClick={() => navigate(toProjectManagementPlatformRoute())} type="button"><PmIcon name="briefcase" /></button></span></Tooltip>
      <Tooltip title={t('projectManagement.workbench.overview')}><span><button aria-label={t('projectManagement.workbench.overview')} className={active === 'overview' ? 'pm-workbench-rail-button is-active' : 'pm-workbench-rail-button'} onClick={() => navigate(overviewPath)} type="button"><PmIcon name="layers" /></button></span></Tooltip>
      <Tooltip title={t('projectManagement.workbench.requirementsNav')}><span><button aria-label={t('projectManagement.workbench.requirementsNav')} className={active === 'requirements' ? 'pm-workbench-rail-button is-active' : 'pm-workbench-rail-button'} onClick={() => navigate(requirementsPath)} type="button"><PmIcon name="folder" /></button></span></Tooltip>
    </Stack>
    <Box sx={{ display: 'flex', flexDirection: 'column', flex: '1 1 auto', minWidth: 0, minHeight: 0, bgcolor: '#fff' }}>
      <Box component="nav" sx={{ display: 'flex', alignItems: 'center', gap: 2.5, minHeight: 44, px: 3, borderBottom: '1px solid #e7eaf0' }}>
        <button className={active === 'overview' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => navigate(overviewPath)} type="button">{t('projectManagement.workbench.overview')}</button>
        <button className={active === 'requirements' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => navigate(requirementsPath)} type="button">{t('projectManagement.workbench.requirementsNav')}</button>
      </Box>
      <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, minWidth: 0, overflow: 'hidden' }}>{children}</Box>
    </Box>
  </Box>;
}

export function ProjectScreenHeader({ code, name, onCreateRequirement }: { code: string; name: string; onCreateRequirement?: () => void }) {
  const { format, t } = useProjectManagementI18n();
  return <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 3, py: 2.25, borderBottom: '1px solid #eef0f4' }}>
    <Stack spacing={0.3}><Typography color="text.secondary" variant="caption">{format('projectManagement.workbench.breadcrumb', { code })}</Typography><Typography fontWeight={700} variant="h6">{name}</Typography></Stack>
    {onCreateRequirement ? <button className="pm-primary-button" onClick={onCreateRequirement} type="button"><PmIcon name="plus" size={15} /> {t('projectManagement.workbench.createRequirement')}</button> : null}
  </Stack>;
}

export function ProjectScreenDivider() { return <Divider sx={{ borderColor: '#eef0f4' }} />; }
