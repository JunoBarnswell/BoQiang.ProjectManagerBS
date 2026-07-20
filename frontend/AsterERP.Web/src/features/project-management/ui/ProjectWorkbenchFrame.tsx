import { Box, Typography } from '@mui/material';
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

  return (
    <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, width: '100%', bgcolor: '#f7f8fb' }}>
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1, width: 48, py: 1.5, bgcolor: '#fff', borderRight: '1px solid #e7eaf0' }}>
        <TooltipButton label={t('projectManagement.workbench.back')} onClick={() => navigate(toProjectManagementPlatformRoute())}><PmIcon name="briefcase" /></TooltipButton>
        <TooltipButton active={active === 'overview'} label={t('projectManagement.workbench.overview')} onClick={() => navigate(overviewPath)}><PmIcon name="layers" /></TooltipButton>
        <TooltipButton active={active === 'requirements'} label={t('projectManagement.workbench.requirementsNav')} onClick={() => navigate(requirementsPath)}><PmIcon name="folder" /></TooltipButton>
      </Box>
      <Box sx={{ display: 'flex', flexDirection: 'column', flex: '1 1 auto', minWidth: 0, minHeight: 0, bgcolor: '#fff' }}>
        <Box component="nav" sx={{ display: 'flex', alignItems: 'center', gap: 2.5, minHeight: 44, px: 3, borderBottom: '1px solid #e7eaf0' }}>
          <button className={active === 'overview' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => navigate(overviewPath)} type="button">{t('projectManagement.workbench.overview')}</button>
          <button className={active === 'requirements' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => navigate(requirementsPath)} type="button">{t('projectManagement.workbench.requirementsNav')}</button>
        </Box>
        <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, minWidth: 0, overflow: 'hidden' }}>{children}</Box>
      </Box>
    </Box>
  );
}

export function ProjectScreenHeader({
  code,
  name,
  onCreateRequirement,
  onRefresh,
  refreshing = false,
}: {
  code: string;
  name: string;
  onCreateRequirement?: () => void;
  onRefresh?: () => void | Promise<void>;
  refreshing?: boolean;
}) {
  const { format, t } = useProjectManagementI18n();
  return (
    <Box
      sx={{
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 1.5,
        px: 3,
        py: 2,
        borderBottom: '1px solid #eef0f4',
      }}
    >
      <Box sx={{ minWidth: 0, flex: '1 1 220px' }}>
        <Typography color="text.secondary" variant="caption">{format('projectManagement.workbench.breadcrumb', { code })}</Typography>
        <Typography fontWeight={700} noWrap sx={{ mt: 0.25, lineHeight: 1.35 }} variant="h6">{name}</Typography>
      </Box>
      <Box className="pm-header-actions" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, ml: 'auto', flexShrink: 0 }}>
        {onRefresh ? (
          <button
            aria-busy={refreshing}
            aria-label={t('projectManagement.workbench.refresh')}
            className="pm-workbench-command"
            disabled={refreshing}
            onClick={() => { void onRefresh(); }}
            type="button"
          >
            <span className={refreshing ? 'pm-refresh-icon is-spinning' : 'pm-refresh-icon'}>
              <PmIcon name="refresh" size={15} />
            </span>
            {t('projectManagement.workbench.refresh')}
          </button>
        ) : null}
        {onCreateRequirement ? (
          <button className="pm-primary-button" onClick={onCreateRequirement} type="button">
            <PmIcon name="plus" size={15} /> {t('projectManagement.workbench.createRequirement')}
          </button>
        ) : null}
      </Box>
    </Box>
  );
}

export function ProjectScreenDivider() { return null; }

function TooltipButton({ active, children, label, onClick }: { active?: boolean; children: ReactNode; label: string; onClick: () => void }) {
  return (
    <span title={label}>
      <button aria-label={label} className={active ? 'pm-workbench-rail-button is-active' : 'pm-workbench-rail-button'} onClick={onClick} type="button">{children}</button>
    </span>
  );
}
