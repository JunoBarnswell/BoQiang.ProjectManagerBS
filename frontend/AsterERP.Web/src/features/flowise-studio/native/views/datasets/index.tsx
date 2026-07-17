import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import { Box, Paper, Skeleton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField } from '@mui/material';
import { useState } from 'react';
import { Link } from 'react-router-dom';

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
import { evaluationsApi } from '../../../api/evaluations.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseDatasetListItemDto, FlowiseDatasetSaveRequest } from '../../../types/evaluation.types';
import datasetEmptySvg from '../../assets/images/empty_datasets.svg';
import { buildSourceQuery, formatSourceDate, getSourcePageTotalPages, sourcePageSizeOptions } from '../common/sourcePageUtils';

import { AddEditDatasetDialog } from './AddEditDatasetDialog';

export function FlowiseDatasetsNativePage() {
  const { translate } = useI18n();
  const canEdit = usePermission(flowisePermissions.datasetsEdit).hasPermission;
  const confirm = useConfirm();
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<FlowiseDatasetListItemDto | null>(null);

  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-source-datasets', keyword, page, pageSize],
    queryFn: ({ signal }) => evaluationsApi.datasets.list(buildSourceQuery(keyword, '', page, pageSize), signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (draft: FlowiseDatasetSaveRequest) => editing ? evaluationsApi.datasets.update(editing.id, draft) : evaluationsApi.datasets.create(draft),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => { setDialogOpen(false); setEditing(null); await query.refetch(); }
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => evaluationsApi.datasets.delete(id),
    onSuccess: async () => query.refetch()
  });

  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);

  const deleteDataset = (item: FlowiseDatasetListItemDto) => confirm({
    title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
    content: `Delete dataset ${item.name}?`,
    confirmText: translate(flowiseI18nKeys.actions.delete),
    onConfirm: async () => {
      await deleteMutation.mutateAsync(item.id);
    }
  });

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div><h1>{translate(flowiseI18nKeys.pages.datasets)}</h1><p>{translate(flowiseI18nKeys.source.datasets.description)}</p></div>
        <PermissionMuiButton code={flowisePermissions.datasetsEdit} startIcon={<AddIcon />} variant="contained" onClick={() => { setEditing(null); setDialogOpen(true); }}>
          {translate(flowiseI18nKeys.source.datasets.addDataset)}
        </PermissionMuiButton>
      </header>
      <Stack className="flowise-source-toolbar" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
        <TextField fullWidth placeholder={translate(flowiseI18nKeys.source.datasets.search)} size="small" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} />
      </Stack>
      {!query.isLoading && rows.length === 0 ? (
        <Box className="flowise-source-empty"><img alt="empty_datasets" src={datasetEmptySvg} /><div>{translate(flowiseI18nKeys.source.datasets.empty)}</div></Box>
      ) : (
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>{translate(flowiseI18nKeys.source.fields.name)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.description)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.rows)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.lastUpdated)}</TableCell>
                {canEdit ? <TableCell /> : null}
                {canEdit ? <TableCell /> : null}
              </TableRow>
            </TableHead>
            <TableBody>
              {query.isLoading ? [0, 1].map((index) => <TableRow key={index}>{Array.from({ length: canEdit ? 6 : 4 }).map((_, cellIndex) => <TableCell key={cellIndex}><Skeleton /></TableCell>)}</TableRow>) : rows.map((item) => (
                  <TableRow hover key={item.id}>
                    <TableCell><Link className="flowise-native-table-link" to={`/flowise/dataset_rows/${item.id}`}>{item.name}</Link></TableCell>
                    <TableCell className="flowise-source-ellipsis">{item.description || '-'}</TableCell>
                    <TableCell>{item.rowCount}</TableCell>
                    <TableCell>{formatSourceDate(item.updatedTime ?? item.createdTime)}</TableCell>
                    {canEdit ? (
                      <TableCell align="center">
                        <PermissionMuiIconButton code={flowisePermissions.datasetsEdit} color="primary" onClick={() => { setEditing(item); setDialogOpen(true); }}>
                          <EditIcon fontSize="small" />
                        </PermissionMuiIconButton>
                      </TableCell>
                    ) : null}
                    {canEdit ? (
                      <TableCell align="center">
                        <PermissionMuiIconButton code={flowisePermissions.datasetsEdit} color="error" onClick={() => deleteDataset(item)}>
                          <DeleteIcon fontSize="small" />
                        </PermissionMuiIconButton>
                      </TableCell>
                    ) : null}
                  </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination component="div" count={total} page={Math.max(0, page - 1)} rowsPerPage={pageSize} rowsPerPageOptions={[...sourcePageSizeOptions]} onPageChange={(_, nextPage) => setPage(Math.min(totalPages, nextPage + 1))} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }} />
        </TableContainer>
      )}
      <AddEditDatasetDialog item={editing} open={dialogOpen} saving={upsertMutation.isPending} onClose={() => setDialogOpen(false)} onSubmit={(draft) => upsertMutation.mutate(draft)} />
    </section>
  );
}
