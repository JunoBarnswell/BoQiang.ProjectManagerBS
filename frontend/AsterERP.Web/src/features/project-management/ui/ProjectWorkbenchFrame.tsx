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
    <Box className="pm-workbench" sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, width: '100%', bgcolor: 'var(--app-bg-subtle)' }}>
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1, width: 48, py: 1.5, bgcolor: 'var(--app-white)', borderRight: '1px solid var(--app-border-subtle)' }}>
        <TooltipButton label={t('projectManagement.workbench.back')} onClick={() => navigate(toProjectManagementPlatformRoute())}><PmIcon name="briefcase" /></TooltipButton>
        <TooltipButton active={active === 'overview'} label={t('projectManagement.workbench.overview')} onClick={() => navigate(overviewPath)}><PmIcon name="layers" /></TooltipButton>
        <TooltipButton active={active === 'requirements'} label={t('projectManagement.workbench.requirementsNav')} onClick={() => navigate(requirementsPath)}><PmIcon name="folder" /></TooltipButton>
      </Box>
      <Box sx={{ display: 'flex', flexDirection: 'column', flex: '1 1 auto', minWidth: 0, minHeight: 0, bgcolor: 'var(--app-white)' }}>
        <Box component="nav" sx={{ display: 'flex', alignItems: 'center', gap: 2.5, minHeight: 44, px: 3, borderBottom: '1px solid var(--app-border-subtle)' }}>
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
    <Box className="pm-workbench-header">
      <Box className="pm-workbench-header__meta">
        <Typography className="pm-workbench-header__breadcrumb" variant="caption">{format('projectManagement.workbench.breadcrumb', { code })}</Typography>
        <Typography className="pm-workbench-header__title" noWrap variant="h6">{name}</Typography>
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
