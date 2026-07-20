import { Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Drawer, IconButton, MenuItem, Popover, Select, Tab, Tabs, TextField, Tooltip, type ButtonProps, type SelectChangeEvent, type TextFieldProps } from '@mui/material';
import { styled } from '@mui/material/styles';
import { useId } from 'react';
import type { MouseEventHandler, ReactElement, ReactNode } from 'react';

export const PmButton = (props: ButtonProps) => <Button size="small" {...props} />;
export const PmIconButton = styled(IconButton)(({ theme }) => ({ color: theme.palette.text.secondary, '&:hover': { color: theme.palette.text.primary, backgroundColor: theme.palette.action.hover } }));
export const PmInput = styled(TextField)(({ theme }) => ({
  '& .MuiInputBase-root': { fontSize: '0.8125rem', backgroundColor: theme.palette.background.paper },
}));
export const PmSelect = styled(Select)(({ theme }) => ({ minWidth: 128, fontSize: '0.8125rem', backgroundColor: theme.palette.background.paper }));
export const PmMenuItem = MenuItem;
export const PmChip = (props: { label: string; onClick?: MouseEventHandler<HTMLElement>; color?: 'default' | 'primary' | 'success' | 'warning' | 'error' }) => <Chip clickable={Boolean(props.onClick)} size="small" {...props} />;
export function PmNotice({ severity, children }: { severity: 'info' | 'warning' | 'error'; children: ReactNode }) {
  const colors = { info: 'info.main', warning: 'warning.main', error: 'error.main' } as const;
  return <Box role="status" sx={{ border: 1, borderColor: colors[severity], borderRadius: 1, color: colors[severity], fontSize: '.78rem', px: 1.25, py: 1 }}>{children}</Box>;
}
export const PmMenuShortcut = styled('span')(({ theme }) => ({ marginLeft: 'auto', paddingLeft: theme.spacing(2), color: theme.palette.text.secondary, fontSize: '.72rem' }));
export function PmActiveFilterBar({ items, clearLabel, onClear }: { items: Array<{ id: string; label: string; onRemove: () => void }>; clearLabel: string; onClear: () => void }) {
  if (items.length === 0) return null;
  return <Box role="list" aria-label={clearLabel} sx={{ display: 'flex', alignItems: 'center', gap: .75, flexWrap: 'wrap', py: .75 }}>
    {items.map(item => <Chip key={item.id} label={item.label} onDelete={item.onRemove} size="small" variant="outlined" />)}
    <Button color="inherit" onClick={onClear} size="small">{clearLabel}</Button>
  </Box>;
}
export const PmTabs = styled(Tabs)(({ theme }) => ({ minHeight: 36, '& .MuiTab-root': { minHeight: 36, minWidth: 0, padding: theme.spacing(0, 1.5), fontSize: '0.78rem', textTransform: 'none' } }));
export const PmTab = Tab;
export const PmPopover = Popover;
export function PmDrawer({ open, onClose, children }: { open: boolean; onClose: () => void; children: ReactNode }) { return <Drawer anchor="right" onClose={onClose} open={open}><Box sx={{ width: 'min(92vw, 360px)', height: '100%' }}>{children}</Box></Drawer>; }
export function PmTooltip({ title, children }: { title: string; children: ReactElement }) { return <Tooltip title={title}>{children}</Tooltip>; }
export function PmDialog({ open, title, children, actions, onClose }: { open: boolean; title: string; children: ReactNode; actions?: ReactNode; onClose: () => void }) { return <Dialog fullWidth maxWidth="md" onClose={onClose} open={open}><DialogTitle sx={{ fontSize: '1rem', fontWeight: 700 }}>{title}</DialogTitle><DialogContent dividers>{children}</DialogContent>{actions ? <DialogActions sx={{ px: 3, py: 2 }}>{actions}</DialogActions> : null}</Dialog>; }
export function PmFormInput({ label, id, 'aria-label': ariaLabel, ...props }: TextFieldProps) {
  const generatedId = useId().replace(/:/g, '');
  const fieldId = id ?? `pm-field-${generatedId}`;
  const labelId = `${fieldId}-label`;
  const accessibleLabel = typeof ariaLabel === 'string' && !label ? ariaLabel : undefined;
  return <Box sx={{ position: 'relative' }}>
    {label ? <Box component="label" id={labelId} htmlFor={fieldId} sx={{
      backgroundColor: 'background.paper',
      color: 'text.secondary',
      fontSize: '.68rem',
      left: 10,
      lineHeight: 1,
      px: .35,
      position: 'absolute',
      top: 0,
      transform: 'translateY(-50%)',
      zIndex: 1,
    }}>{label}</Box> : null}
    <PmInput fullWidth id={fieldId} aria-label={accessibleLabel} aria-labelledby={label ? labelId : undefined} size="small" {...props} label={undefined} />
  </Box>;
}
export const PmLink = styled('a')(({ theme }) => ({ color: theme.palette.primary.main, display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', textDecoration: 'none', '&:hover': { textDecoration: 'underline' } }));
export function PmFormSelect({ label, value, onChange, children }: { label: string; value: string; onChange: (event: SelectChangeEvent<unknown>) => void; children: ReactNode }) { return <PmSelect displayEmpty fullWidth size="small" value={value} onChange={onChange} inputProps={{ 'aria-label': label }}>{children}</PmSelect>; }
