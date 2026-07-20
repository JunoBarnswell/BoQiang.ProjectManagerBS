import { Box, Divider, Stack as MuiStack, Tooltip, Typography as MuiTypography } from '@mui/material';
import type { ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import { PmIcon } from '../../../ui/project-management';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import './projectWorkbench.css';

const Stack = MuiStack as any;
const Typography = MuiTypography as any;

const futureSections = ['缺陷', '工作项', '迭代', '里程碑', '报表', '设置'];

export function ProjectWorkbenchFrame({ children, active }: { children: ReactNode; active: 'overview' | 'requirements' }) {
  const navigate = useNavigate();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const overviewPath = toProjectManagementPlatformRoute(`projects/${encodeURIComponent(projectId)}/overview`);
  const requirementsPath = toProjectManagementPlatformRoute(`projects/${encodeURIComponent(projectId)}/requirements`);

  return <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, width: '100%', bgcolor: '#f7f8fb' }}>
    <Stack alignItems="center" spacing={1} sx={{ width: 48, py: 1.5, bgcolor: '#fff', borderRight: '1px solid #e7eaf0' }}>
      <Tooltip title="返回项目"><span><button aria-label="返回项目" className="pm-workbench-rail-button" onClick={() => navigate(toProjectManagementPlatformRoute())} type="button"><PmIcon name="briefcase" /></button></span></Tooltip>
      <Tooltip title="项目概览"><span><button aria-label="项目概览" className={active === 'overview' ? 'pm-workbench-rail-button is-active' : 'pm-workbench-rail-button'} onClick={() => navigate(overviewPath)} type="button"><PmIcon name="layers" /></button></span></Tooltip>
      <Tooltip title="需求中心"><span><button aria-label="需求中心" className={active === 'requirements' ? 'pm-workbench-rail-button is-active' : 'pm-workbench-rail-button'} onClick={() => navigate(requirementsPath)} type="button"><PmIcon name="folder" /></button></span></Tooltip>
    </Stack>
    <Box sx={{ display: 'flex', flexDirection: 'column', flex: '1 1 auto', minWidth: 0, minHeight: 0, bgcolor: '#fff' }}>
      <Box component="nav" sx={{ display: 'flex', alignItems: 'center', gap: 2.5, minHeight: 44, px: 3, borderBottom: '1px solid #e7eaf0' }}>
        <button className={active === 'overview' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => navigate(overviewPath)} type="button">概览</button>
        <button className={active === 'requirements' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => navigate(requirementsPath)} type="button">需求</button>
        {futureSections.map((section) => <Tooltip key={section} title="后续版本，当前不可用"><span><Typography color="text.disabled" component="span" sx={{ fontSize: 13 }}>{section}</Typography></span></Tooltip>)}
      </Box>
      <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, minWidth: 0, overflow: 'hidden' }}>{children}</Box>
    </Box>
  </Box>;
}

export function ProjectScreenHeader({ code, name, onCreateRequirement }: { code: string; name: string; onCreateRequirement?: () => void }) {
  return <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 3, py: 2.25, borderBottom: '1px solid #eef0f4' }}>
    <Stack spacing={0.3}><Typography color="text.secondary" variant="caption">项目 / {code}</Typography><Typography fontWeight={700} variant="h6">{name}</Typography></Stack>
    {onCreateRequirement ? <button className="pm-primary-button" onClick={onCreateRequirement} type="button"><PmIcon name="plus" size={15} /> 新建需求</button> : null}
  </Stack>;
}

export function ProjectScreenDivider() { return <Divider sx={{ borderColor: '#eef0f4' }} />; }
