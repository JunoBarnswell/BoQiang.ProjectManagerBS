import {
  Avatar,
  Dialog,
  DialogContent,
  DialogTitle,
  List,
  ListItemButton,
  ListItemAvatar,
  ListItemText,
  TextField
} from '@mui/material';
import { useMemo, useState } from 'react';

import type { FlowiseResourceDto } from '../../../types/shared.types';
import keySvg from '../../assets/images/key.svg';
import { parseJsonRecord } from '../common/sourcePageUtils';

interface CredentialListDialogProps {
  components: FlowiseResourceDto[];
  open: boolean;
  onClose: () => void;
  onSelect: (component: FlowiseResourceDto) => void;
}

export function CredentialListDialog({ components, open, onClose, onSelect }: CredentialListDialogProps) {
  const [search, setSearch] = useState('');
  const rows = useMemo(() => {
    const keyword = search.toLowerCase();
    return components.filter((component) => !keyword || `${component.displayName} ${component.resourceKey} ${component.category}`.toLowerCase().includes(keyword));
  }, [components, search]);

  return (
    <Dialog fullWidth maxWidth="sm" open={open} onClose={onClose}>
      <DialogTitle>Add New Credential</DialogTitle>
      <DialogContent>
        <TextField fullWidth autoFocus placeholder="Search credential type" size="small" sx={{ mb: 2 }} value={search} onChange={(event) => setSearch(event.target.value)} />
        <List className="flowise-source-picker-list">
          {rows.map((component) => {
            const metadata = parseJsonRecord(component.metadataJson);
            const iconSrc = typeof metadata.iconSrc === 'string' ? metadata.iconSrc : keySvg;
            return (
              <ListItemButton key={component.id} onClick={() => onSelect(component)}>
                <ListItemAvatar>
                  <Avatar className="flowise-source-avatar">
                    <img alt={component.displayName} src={iconSrc} onError={(event) => { event.currentTarget.src = keySvg; }} />
                  </Avatar>
                </ListItemAvatar>
                <ListItemText primary={component.displayName} secondary={component.description || component.resourceKey} />
              </ListItemButton>
            );
          })}
        </List>
      </DialogContent>
    </Dialog>
  );
}
