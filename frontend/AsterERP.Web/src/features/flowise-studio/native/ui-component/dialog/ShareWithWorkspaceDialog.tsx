import { Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField } from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseSharedWorkspaceDto } from '../../../types/shared.types';

interface ShareWithWorkspaceDialogProps {
  loading?: boolean;
  open: boolean;
  saving?: boolean;
  title: string;
  workspaces: FlowiseSharedWorkspaceDto[];
  onClose: () => void;
  onSave: (workspaceIds: string[]) => void;
}

export function ShareWithWorkspaceDialog({ loading, open, saving, title, workspaces, onClose, onSave }: ShareWithWorkspaceDialogProps) {
  const { translate } = useI18n();
  const [rows, setRows] = useState<FlowiseSharedWorkspaceDto[]>([]);

  useEffect(() => {
    setRows(workspaces);
  }, [workspaces]);

  const updateRow = (workspaceId: string, shared: boolean) => {
    setRows((current) => current.map((row) => row.workspaceId === workspaceId ? { ...row, shared } : row));
  };

  const selectedIds = rows.filter((row) => row.shared).map((row) => row.workspaceId);

  return (
    <Dialog fullWidth maxWidth="sm" open={open} onClose={onClose}>
      <DialogTitle>{translate(flowiseI18nKeys.source.credentials.shareTitle)}</DialogTitle>
      <DialogContent>
        <TextField disabled fullWidth label={translate(flowiseI18nKeys.fields.name)} margin="dense" size="small" value={title} />
        {loading ? <p className="flowise-source-dialog-hint">{translate(flowiseI18nKeys.common.loading)}</p> : null}
        {!loading && rows.length === 0 ? <p className="flowise-source-dialog-hint">{translate(flowiseI18nKeys.messages.noWorkspaces)}</p> : null}
        {!loading && rows.length > 0 ? (
          <TableContainer className="flowise-source-table-container" component={Paper} sx={{ mt: 2 }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>{translate(flowiseI18nKeys.fields.workspace)}</TableCell>
                  <TableCell align="center">{translate(flowiseI18nKeys.actions.share)}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((row) => (
                  <TableRow hover key={row.workspaceId}>
                    <TableCell>{row.workspaceName}</TableCell>
                    <TableCell align="center">
                      <Checkbox checked={row.shared} onChange={(event) => updateRow(row.workspaceId, event.target.checked)} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        ) : null}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <Button disabled={saving || loading} variant="contained" onClick={() => onSave(selectedIds)}>
          {translate(flowiseI18nKeys.actions.share)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
