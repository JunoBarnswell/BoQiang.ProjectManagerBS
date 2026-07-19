import { Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, MenuItem, Popover, Select, Tab, Tabs, TextField, Tooltip, type ButtonProps, type SelectChangeEvent } from '@mui/material';
import { styled } from '@mui/material/styles';
import type { ReactElement, ReactNode } from 'react';

export const PmButton = (props: ButtonProps) => <Button size="small" {...props} />;
export const PmIconButton = styled(IconButton)(({ theme }) => ({ color: theme.palette.text.secondary, '&:hover': { color: theme.palette.text.primary, backgroundColor: theme.palette.action.hover } }));
export const PmInput = styled(TextField)(({ theme }) => ({ '& .MuiInputBase-root': { fontSize: '0.8125rem', backgroundColor: theme.palette.background.paper } }));
export const PmSelect = styled(Select)(({ theme }) => ({ minWidth: 128, fontSize: '0.8125rem', backgroundColor: theme.palette.background.paper }));
export const PmMenuItem = MenuItem;
export const PmChip = (props: { label: string; onClick?: () => void; color?: 'default' | 'primary' | 'success' | 'warning' | 'error' }) => <Chip clickable={Boolean(props.onClick)} size="small" {...props} />;
export const PmTabs = styled(Tabs)(({ theme }) => ({ minHeight: 36, '& .MuiTab-root': { minHeight: 36, minWidth: 0, padding: theme.spacing(0, 1.5), fontSize: '0.78rem', textTransform: 'none' } }));
export const PmTab = Tab;
export const PmPopover = Popover;
export function PmTooltip({ title, children }: { title: string; children: ReactElement }) { return <Tooltip title={title}>{children}</Tooltip>; }
export function PmDialog({ open, title, children, actions, onClose }: { open: boolean; title: string; children: ReactNode; actions?: ReactNode; onClose: () => void }) { return <Dialog fullWidth maxWidth="md" onClose={onClose} open={open}><DialogTitle sx={{ fontSize: '1rem', fontWeight: 700 }}>{title}</DialogTitle><DialogContent dividers>{children}</DialogContent>{actions ? <DialogActions sx={{ px: 3, py: 2 }}>{actions}</DialogActions> : null}</Dialog>; }
export function PmFormInput(props: React.ComponentProps<typeof TextField>) { return <PmInput fullWidth size="small" {...props} />; }
export function PmFormSelect({ label, value, onChange, children }: { label: string; value: string; onChange: (event: SelectChangeEvent<unknown>) => void; children: ReactNode }) { return <PmSelect displayEmpty fullWidth size="small" value={value} onChange={onChange} inputProps={{ 'aria-label': label }}>{children}</PmSelect>; }
