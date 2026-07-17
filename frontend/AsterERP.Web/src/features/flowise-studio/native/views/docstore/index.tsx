import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import GridViewIcon from '@mui/icons-material/GridView';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import {
  Box,
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
  TextField,
  ToggleButton,
  ToggleButtonGroup
} from '@mui/material';
import { useState } from 'react';
import { Link } from 'react-router-dom';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { PermissionMuiButton } from '../../../../../shared/auth/PermissionMuiButton';
import { PermissionMuiIconButton } from '../../../../../shared/auth/PermissionMuiIconButton';
import { useConfirm } from '../../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../../shared/utils/errorMessage';
import { documentStoresApi } from '../../../api/documentStores.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseDocumentStoreListItemDto, FlowiseDocumentStoreSaveRequest } from '../../../types/documentStore.types';
import docStoreEmptySvg from '../../assets/images/doc_store_empty.svg';
import { ItemCard } from '../../ui-component/cards/ItemCard';
import { buildSourceQuery, formatSourceDate, getSourcePageTotalPages, parseJsonRecord, readNumber, sourcePageSizeOptions, splitTags } from '../common/sourcePageUtils';

import { AddDocStoreDialog } from './AddDocStoreDialog';

export function FlowiseDocumentStoresNativePage() {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [view, setView] = useState<'card' | 'list'>(() => localStorage.getItem('docStoreDisplayStyle') === 'list' ? 'list' : 'card');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<FlowiseDocumentStoreListItemDto | null>(null);

  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-source-docstores', keyword, page, pageSize],
    queryFn: ({ signal }) => documentStoresApi.list(buildSourceQuery(keyword, '', page, pageSize), signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (draft: FlowiseDocumentStoreSaveRequest) => editing ? documentStoresApi.update(editing.id, draft) : documentStoresApi.create(draft),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => {
      setDialogOpen(false);
      setEditing(null);
      await query.refetch();
    }
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => documentStoresApi.delete(id),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => query.refetch()
  });

  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);

  const deleteStore = (item: FlowiseDocumentStoreListItemDto) => {
    confirm({
      title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
      content: `Delete document store ${item.name}?`,
      confirmText: translate(flowiseI18nKeys.actions.delete),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(item.id);
      }
    });
  };

  const changeView = (_: unknown, nextView: 'card' | 'list' | null) => {
    if (!nextView) {
      return;
    }

    setView(nextView);
    localStorage.setItem('docStoreDisplayStyle', nextView);
  };

  const renderActions = (item: FlowiseDocumentStoreListItemDto) => (
    <>
      <IconButton component={Link} title={translate(flowiseI18nKeys.actions.open)} to={`/flowise/document-stores/${item.id}`}><OpenInNewIcon fontSize="small" /></IconButton>
      <PermissionMuiIconButton code={flowisePermissions.documentStoresEdit} color="primary" title={translate(flowiseI18nKeys.actions.rename)} onClick={() => { setEditing(item); setDialogOpen(true); }}>
        <EditIcon fontSize="small" />
      </PermissionMuiIconButton>
      <PermissionMuiIconButton code={flowisePermissions.documentStoresEdit} color="error" title={translate(flowiseI18nKeys.actions.delete)} onClick={() => deleteStore(item)}>
        <DeleteIcon fontSize="small" />
      </PermissionMuiIconButton>
    </>
  );

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div>
          <h1>{translate(flowiseI18nKeys.pages.documentStores)}</h1>
          <p>{translate(flowiseI18nKeys.source.docstores.description)}</p>
        </div>
        <PermissionMuiButton code={flowisePermissions.documentStoresEdit} startIcon={<AddIcon />} variant="contained" onClick={() => { setEditing(null); setDialogOpen(true); }}>
          {translate(flowiseI18nKeys.source.docstores.addNew)}
        </PermissionMuiButton>
      </header>

      <Stack className="flowise-source-toolbar" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
        <TextField fullWidth placeholder={translate(flowiseI18nKeys.source.docstores.search)} size="small" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} />
        <ToggleButtonGroup exclusive size="small" value={view} onChange={changeView}>
          <ToggleButton title={translate(flowiseI18nKeys.actions.cardView)} value="card"><GridViewIcon fontSize="small" /></ToggleButton>
          <ToggleButton title={translate(flowiseI18nKeys.actions.listView)} value="list"><FormatListBulletedIcon fontSize="small" /></ToggleButton>
        </ToggleButtonGroup>
      </Stack>

      {!query.isLoading && rows.length === 0 ? (
        <Box className="flowise-source-empty">
          <img alt="doc_store_empty" src={docStoreEmptySvg} />
          <div>{translate(flowiseI18nKeys.source.docstores.empty)}</div>
        </Box>
      ) : view === 'card' && !query.isLoading ? (
        <>
          <div className="flowise-native-card-grid">
            {rows.map((item) => {
              const loaders = splitTags(item.loaderConfig.loaderType ?? item.category ?? '');
              return (
                <ItemCard
                  actions={renderActions(item)}
                  icon="database"
                  key={item.id}
                  meta={loaders.length ? loaders.map((loader) => <span key={loader}>{loader}</span>) : <span>{translate(flowiseI18nKeys.common.none)}</span>}
                  subtitle={formatSourceDate(item.updatedTime ?? item.createdTime)}
                  title={<Link className="flowise-native-card-title-link" to={`/flowise/document-stores/${item.id}`}>{item.name}</Link>}
                >
                  <p>{item.description || '-'}</p>
                </ItemCard>
              );
            })}
          </div>
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
        </>
      ) : (
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>{translate(flowiseI18nKeys.source.fields.name)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.description)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.loaders)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.files)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.chunks)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.updated)}</TableCell>
                <TableCell align="right" />
              </TableRow>
            </TableHead>
            <TableBody>
              {query.isLoading ? [0, 1].map((index) => (
                <TableRow key={index}>{Array.from({ length: 7 }).map((_, cellIndex) => <TableCell key={cellIndex}><Skeleton /></TableCell>)}</TableRow>
              )) : rows.map((item) => {
                const metadata = parseJsonRecord(item.advancedMetadataJson);
                const loaders = splitTags(item.loaderConfig.loaderType ?? item.category ?? '');
                return (
                  <TableRow hover key={item.id}>
                    <TableCell><Link className="flowise-native-table-link" to={`/flowise/document-stores/${item.id}`}>{item.name}</Link></TableCell>
                    <TableCell className="flowise-source-ellipsis">{item.description || '-'}</TableCell>
                    <TableCell>
                      <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 0.5 }}>
                        {loaders.length ? loaders.map((loader) => <Chip key={loader} label={loader} size="small" />) : <span>-</span>}
                      </Stack>
                    </TableCell>
                    <TableCell>{readNumber(metadata.fileCount)}</TableCell>
                    <TableCell>{readNumber(metadata.chunkCount)}</TableCell>
                    <TableCell>{formatSourceDate(item.updatedTime ?? item.createdTime)}</TableCell>
                    <TableCell align="right">{renderActions(item)}</TableCell>
                  </TableRow>
                );
              })}
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

      <AddDocStoreDialog item={editing} open={dialogOpen} saving={upsertMutation.isPending} onClose={() => setDialogOpen(false)} onSubmit={(draft) => upsertMutation.mutate(draft)} />
    </section>
  );
}
