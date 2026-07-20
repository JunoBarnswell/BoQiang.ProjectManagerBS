import { Divider, Menu, MenuItem, ListItemIcon, ListItemText } from '@mui/material';
import { useState, type ReactNode } from 'react';

import { PmMenuShortcut } from './PmControls';

export interface PmContextMenuItem {
  id: string;
  label: string;
  icon?: ReactNode;
  shortcut?: string;
  permission?: string;
  danger?: boolean;
  disabled?: boolean;
  separator?: boolean;
  children?: PmContextMenuItem[];
  action?: string;
}

export function PmEntityContextMenu({ anchorPosition, items, onClose, onAction }: { anchorPosition: { top: number; left: number } | null; items: PmContextMenuItem[]; onClose: () => void; onAction: (action: string) => void }) {
  return <Menu anchorReference="anchorPosition" anchorPosition={anchorPosition ?? undefined} onClose={onClose} open={Boolean(anchorPosition)}>
    {items.map(item => <ContextMenuItem item={item} key={item.id} onAction={onAction} onClose={onClose} />)}
  </Menu>;
}

function ContextMenuItem({ item, onClose, onAction }: { item: PmContextMenuItem; onClose: () => void; onAction: (action: string) => void }) {
  const [submenuAnchor, setSubmenuAnchor] = useState<HTMLElement | null>(null);
  if (item.separator) return <Divider component="li" />;
  const hasChildren = Boolean(item.children?.length);
  return <>
    <MenuItem disabled={item.disabled} onClick={event => { if (hasChildren) { setSubmenuAnchor(event.currentTarget); return; } if (item.action) onAction(item.action); onClose(); }} onMouseEnter={event => { if (hasChildren) setSubmenuAnchor(event.currentTarget); }} sx={{ color: item.danger ? 'error.main' : undefined, minWidth: 210 }}>
      {item.icon ? <ListItemIcon>{item.icon}</ListItemIcon> : null}<ListItemText primary={item.label} />{item.shortcut ? <PmMenuShortcut>{item.shortcut}</PmMenuShortcut> : null}{hasChildren ? <span aria-hidden="true">›</span> : null}
    </MenuItem>
    {hasChildren && <Menu anchorEl={submenuAnchor} anchorOrigin={{ horizontal: 'right', vertical: 'top' }} onClose={() => setSubmenuAnchor(null)} open={Boolean(submenuAnchor)} transformOrigin={{ horizontal: 'left', vertical: 'top' }}>
      {item.children?.map(child => <MenuItem disabled={child.disabled} key={child.id} onClick={() => { if (child.action) onAction(child.action); onClose(); }} sx={{ color: child.danger ? 'error.main' : undefined, minWidth: 210 }}>
        {child.icon ? <ListItemIcon>{child.icon}</ListItemIcon> : null}<ListItemText primary={child.label} />{child.shortcut ? <PmMenuShortcut>{child.shortcut}</PmMenuShortcut> : null}
      </MenuItem>)}
    </Menu>}
  </>;
}
