import { Box, Drawer, IconButton, Tooltip, Typography } from '@mui/material';
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

export function PmNavigationItem({ active, collapsed = false, icon, label, onClick, disabled = false }: { active?: boolean; collapsed?: boolean; icon: ReactNode; label: string; onClick?: () => void; disabled?: boolean }) {
  return <NavItem aria-current={active ? 'page' : undefined} aria-disabled={disabled || undefined} aria-label={collapsed ? label : undefined} data-active={active || undefined} onClick={disabled ? undefined : onClick} role={disabled ? undefined : 'button'} tabIndex={disabled ? undefined : 0} onKeyDown={event => { if (!disabled && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); onClick?.(); } }}><Box sx={{ display: 'grid', placeItems: 'center', width: 18, height: 18, flex: '0 0 18px' }}>{icon}</Box>{!collapsed && <Typography noWrap variant="body2">{label}</Typography>}</NavItem>;
}

export function PmNavigationBrand({ icon, label, collapsed }: { icon: ReactNode; label: string; collapsed: boolean }) {
  return <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1, pb: 2, minHeight: 42, overflow: 'hidden' }}><Box sx={{ display: 'grid', placeItems: 'center', width: 28, height: 28, flex: '0 0 28px', borderRadius: 1.5, bgcolor: 'primary.main', color: 'primary.contrastText' }}>{icon}</Box>{!collapsed && <Typography noWrap sx={{ fontSize: '.82rem', fontWeight: 700 }}>{label}</Typography>}</Box>;
}

export function PmNavigationSectionLabel({ children }: { children: ReactNode }) {
  return <Typography color="text.secondary" sx={{ mt: 2.5, mb: .5, px: 1, fontSize: '.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '.04em' }}>{children}</Typography>;
}

export function PmNavigationDisclosure({ expanded, label, onClick, icon }: { expanded: boolean; label: string; onClick: () => void; icon: ReactNode }) {
  return <Box component="button" aria-expanded={expanded} onClick={onClick} sx={{ display: 'flex', alignItems: 'center', gap: .5, width: '100%', px: 1, mt: 1.5, border: 0, background: 'transparent', color: 'text.secondary', cursor: 'pointer', textAlign: 'left' }}><Box sx={{ display: 'grid', placeItems: 'center', transform: expanded ? undefined : 'rotate(-90deg)', transition: 'transform 120ms ease' }}>{icon}</Box><Typography sx={{ fontSize: '.72rem' }}>{label}</Typography></Box>;
}

export function PmNavigationDrawer({ open, onClose, children }: { open: boolean; onClose: () => void; children: ReactNode }) {
  return <Drawer ModalProps={{ keepMounted: true }} onClose={onClose} open={open} sx={{ display: { xs: 'block', lg: 'none' }, '& .MuiDrawer-paper': { backgroundColor: 'background.paper' } }}>{children}</Drawer>;
}
