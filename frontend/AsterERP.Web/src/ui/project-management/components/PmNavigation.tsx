import { Box, IconButton, Tooltip, Typography } from '@mui/material';
import { styled } from '@mui/material/styles';
import type { ReactNode } from 'react';

const NavRoot = styled(Box)(({ theme }) => ({
  width: 248,
  flex: '0 0 248px',
  display: 'flex',
  flexDirection: 'column',
  padding: theme.spacing(2, 1.25, 1),
  borderRight: `1px solid ${theme.palette.divider}`,
  backgroundColor: theme.palette.background.paper,
  transition: 'width 160ms ease, flex-basis 160ms ease',
  '&[data-collapsed="true"]': { width: 58, flexBasis: 58 },
}));

const NavItem = styled(Box)(({ theme }) => ({
  display: 'flex',
  alignItems: 'center',
  gap: theme.spacing(1.25),
  minHeight: 34,
  padding: theme.spacing(.5, 1),
  borderRadius: theme.shape.borderRadius,
  color: theme.palette.text.secondary,
  cursor: 'pointer',
  '&:hover, &[data-active="true"]': { backgroundColor: theme.palette.action.hover, color: theme.palette.text.primary },
  '&:focus-visible': { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: 1 },
}));

export function PmNavigation({ collapsed, children, onToggle, toggleLabel }: { collapsed: boolean; children: ReactNode; onToggle: () => void; toggleLabel: string }) {
  return <NavRoot data-collapsed={collapsed}><Box sx={{ flex: 1, overflow: 'auto' }}>{children}</Box><Tooltip title={toggleLabel}><IconButton aria-label={toggleLabel} onClick={onToggle} size="small">{collapsed ? '›' : '‹'}</IconButton></Tooltip></NavRoot>;
}

export function PmNavigationItem({ active, icon, label, onClick, disabled = false }: { active?: boolean; icon: ReactNode; label: string; onClick?: () => void; disabled?: boolean }) {
  return <NavItem aria-current={active ? 'page' : undefined} aria-disabled={disabled || undefined} data-active={active || undefined} onClick={disabled ? undefined : onClick} role={disabled ? undefined : 'button'} tabIndex={disabled ? undefined : 0} onKeyDown={event => { if (!disabled && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); onClick?.(); } }}><Box sx={{ display: 'grid', placeItems: 'center', width: 18, height: 18, flex: '0 0 18px' }}>{icon}</Box><Typography noWrap variant="body2">{label}</Typography></NavItem>;
}
