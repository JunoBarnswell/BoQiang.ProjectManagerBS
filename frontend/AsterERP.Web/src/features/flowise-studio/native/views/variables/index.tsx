import AddIcon from '@mui/icons-material/Add';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import HelpIcon from '@mui/icons-material/Help';
import VisibilityIcon from '@mui/icons-material/Visibility';
import {
  Box,
  Button,
  Chip,
  IconButton,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField
} from '@mui/material';
import { useState } from 'react';

import { usePermission } from '../../../../../core/auth/usePermission';
import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { PermissionMuiButton } from '../../../../../shared/auth/PermissionMuiButton';
import { PermissionMuiIconButton } from '../../../../../shared/auth/PermissionMuiIconButton';
import { useConfirm } from '../../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../../shared/utils/errorMessage';
import { flowiseConfigurationResourcesApi } from '../../../api/configurationResources.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceDto, FlowiseResourceUpsertRequest } from '../../../types/shared.types';
import variablesEmptySvg from '../../assets/images/variables_empty.svg';
import { buildSourceQuery, formatSourceDate, getSourcePageTotalPages, sourcePageSizeOptions } from '../common/sourcePageUtils';

import { AddEditVariableDialog } from './AddEditVariableDialog';
import { HowToUseVariablesDialog } from './HowToUseVariablesDialog';

export function FlowiseVariablesNativePage() {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const canEdit = usePermission(flowisePermissions.variablesEdit).hasPermission;
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);
  const [editing, setEditing] = useState<FlowiseResourceDto | null>(null);
  const [revealed, setRevealed] = useState<Record<string, string>>({});

  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-source-variables', keyword, page, pageSize],
    queryFn: ({ signal }) => flowiseConfigurationResourcesApi.variables.list(buildSourceQuery(keyword, '', page, pageSize), signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (draft: FlowiseResourceUpsertRequest) => editing
      ? flowiseConfigurationResourcesApi.variables.update(editing.id, draft)
      : flowiseConfigurationResourcesApi.variables.create(draft),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => {
      setDialogOpen(false);
      setEditing(null);
      await query.refetch();
    }
  });
  const revealMutation = useApiMutation({
    mutationFn: (id: string) => flowiseConfigurationResourcesApi.variables.reveal?.(id) ?? Promise.reject(new Error('reveal unsupported')),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async (response) => {
      const value = response.data.oneTimeSecret ?? response.data.secretMask ?? '';
      setRevealed((current) => ({ ...current, [response.data.id]: value }));
      await navigator.clipboard?.writeText(value);
      message.success(translate(flowiseI18nKeys.messages.copiedToClipboard));
    }
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => flowiseConfigurationResourcesApi.variables.delete(id),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => query.refetch()
  });

  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);

  const deleteVariable = (item: FlowiseResourceDto) => {
    confirm({
      title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
      content: `Delete variable ${item.displayName}?`,
      confirmText: translate(flowiseI18nKeys.actions.delete),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(item.id);
      }
    });
  };

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div>
          <h1>{translate(flowiseI18nKeys.pages.variables)}</h1>
          <p>{translate(flowiseI18nKeys.source.variables.description)}</p>
        </div>
        <Stack direction="row" spacing={1}>
          <Button startIcon={<HelpIcon />} variant="outlined" onClick={() => setHelpOpen(true)}>{translate(flowiseI18nKeys.source.variables.howToUse)}</Button>
          <PermissionMuiButton code={flowisePermissions.variablesEdit} startIcon={<AddIcon />} variant="contained" onClick={() => { setEditing(null); setDialogOpen(true); }}>
            {translate(flowiseI18nKeys.source.variables.addVariable)}
          </PermissionMuiButton>
        </Stack>
      </header>

      <Stack className="flowise-source-toolbar" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
        <TextField fullWidth placeholder={translate(flowiseI18nKeys.source.variables.search)} size="small" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} />
      </Stack>

      {!query.isLoading && rows.length === 0 ? (
        <Box className="flowise-source-empty">
          <img alt="VariablesEmptySVG" src={variablesEmptySvg} />
          <div>{translate(flowiseI18nKeys.source.variables.empty)}</div>
        </Box>
      ) : (
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>{translate(flowiseI18nKeys.source.fields.name)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.value)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.type)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.lastUpdated)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.created)}</TableCell>
                {canEdit ? <TableCell /> : null}
                {canEdit ? <TableCell /> : null}
              </TableRow>
            </TableHead>
            <TableBody>
              {query.isLoading ? [0, 1].map((index) => (
                <TableRow key={index}>
                  {Array.from({ length: canEdit ? 7 : 5 }).map((_, cellIndex) => <TableCell key={cellIndex}><Skeleton variant="text" /></TableCell>)}
                </TableRow>
              )) : rows.map((item) => (
                <TableRow hover key={item.id}>
                  <TableCell>{item.displayName}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                      <span className="flowise-source-secret">{revealed[item.id] ?? item.secretMask ?? '******'}</span>
                      <PermissionMuiIconButton code={flowisePermissions.revealSecret} disabled={revealMutation.isPending} size="small" onClick={() => revealMutation.mutate(item.id)}>
                        <VisibilityIcon fontSize="small" />
                      </PermissionMuiIconButton>
                      <IconButton disabled={!revealed[item.id]} size="small" onClick={() => void navigator.clipboard?.writeText(revealed[item.id] ?? '')}>
                        <ContentCopyIcon fontSize="small" />
                      </IconButton>
                    </Stack>
                  </TableCell>
                  <TableCell>{item.category ? <Chip label={item.category} size="small" /> : '-'}</TableCell>
                  <TableCell>{formatSourceDate(item.updatedTime ?? item.createdTime)}</TableCell>
                  <TableCell>{formatSourceDate(item.createdTime)}</TableCell>
                  {canEdit ? (
                    <TableCell align="center">
                      <PermissionMuiIconButton code={flowisePermissions.variablesEdit} color="primary" onClick={() => { setEditing(item); setDialogOpen(true); }}>
                        <EditIcon fontSize="small" />
                      </PermissionMuiIconButton>
                    </TableCell>
                  ) : null}
                  {canEdit ? (
                    <TableCell align="center">
                      <PermissionMuiIconButton code={flowisePermissions.variablesEdit} color="error" onClick={() => deleteVariable(item)}>
                        <DeleteIcon fontSize="small" />
                      </PermissionMuiIconButton>
                    </TableCell>
                  ) : null}
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={total}
            page={Math.max(0, page - 1)}
            rowsPerPage={pageSize}
            rowsPerPageOptions={[...sourcePageSizeOptions]}
            onPageChange={(_, nextPage) => setPage(Math.min(totalPages, nextPage + 1))}
            onRowsPerPageChange={(event) => {
              setPageSize(Number(event.target.value));
              setPage(1);
            }}
          />
        </TableContainer>
      )}

      <AddEditVariableDialog item={editing} open={dialogOpen} saving={upsertMutation.isPending} onClose={() => setDialogOpen(false)} onSubmit={(draft) => upsertMutation.mutate(draft)} />
      <HowToUseVariablesDialog open={helpOpen} onClose={() => setHelpOpen(false)} />
    </section>
  );
}
