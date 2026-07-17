import AddIcon from '@mui/icons-material/Add';
import {
  Box,
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
import { useConfirm } from '../../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../../shared/utils/errorMessage';
import { flowiseConfigurationResourcesApi } from '../../../api/configurationResources.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceDto, FlowiseResourceUpsertRequest } from '../../../types/shared.types';
import apiEmptySvg from '../../assets/images/api_empty.svg';
import { buildSourceQuery, getSourcePageTotalPages, sourcePageSizeOptions } from '../common/sourcePageUtils';

import { APIKeyDialog } from './APIKeyDialog';
import { APIKeyRow } from './APIKeyRow';

export function FlowiseApiKeysNativePage() {
  const { translate } = useI18n();
  const canEdit = usePermission(flowisePermissions.apiKeysEdit).hasPermission;
  const confirm = useConfirm();
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [editing, setEditing] = useState<FlowiseResourceDto | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [revealed, setRevealed] = useState<Record<string, string>>({});

  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-source-api-keys', keyword, page, pageSize],
    queryFn: ({ signal }) => flowiseConfigurationResourcesApi.apiKeys.list(buildSourceQuery(keyword, '', page, pageSize), signal)
  });

  const upsertMutation = useApiMutation({
    mutationFn: (draft: FlowiseResourceUpsertRequest) => editing
      ? flowiseConfigurationResourcesApi.apiKeys.update(editing.id, draft)
      : flowiseConfigurationResourcesApi.apiKeys.create(draft),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async (response) => {
      if (response.data.oneTimeSecret) {
        setRevealed((current) => ({ ...current, [response.data.id]: response.data.oneTimeSecret ?? '' }));
        await navigator.clipboard?.writeText(response.data.oneTimeSecret);
        message.success(translate(flowiseI18nKeys.messages.copiedToClipboard));
      }
      setDialogOpen(false);
      setEditing(null);
      await query.refetch();
    }
  });

  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => flowiseConfigurationResourcesApi.apiKeys.delete(id),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => query.refetch()
  });

  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);

  const openCreate = () => {
    setEditing(null);
    setDialogOpen(true);
  };

  const copySecret = async (value: string) => {
    if (!value) {
      return;
    }
    await navigator.clipboard?.writeText(value);
    message.success(translate(flowiseI18nKeys.messages.copiedToClipboard));
  };

  const deleteItem = (item: FlowiseResourceDto) => {
    confirm({
      title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
      content: `Delete key [${item.displayName}] ?`,
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
          <h1>{translate(flowiseI18nKeys.pages.apiKeys)}</h1>
          <p>{translate(flowiseI18nKeys.source.apiKeys.description)}</p>
        </div>
        <PermissionMuiButton code={flowisePermissions.apiKeysEdit} startIcon={<AddIcon />} variant="contained" onClick={openCreate}>
          {translate(flowiseI18nKeys.source.apiKeys.createKey)}
        </PermissionMuiButton>
      </header>

      <Stack className="flowise-source-toolbar" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
        <TextField fullWidth placeholder={translate(flowiseI18nKeys.source.apiKeys.search)} size="small" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} />
      </Stack>

      {!query.isLoading && rows.length === 0 ? (
        <Box className="flowise-source-empty">
          <img alt="APIEmptySVG" src={apiEmptySvg} />
          <div>{translate(flowiseI18nKeys.source.apiKeys.empty)}</div>
        </Box>
      ) : (
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>{translate(flowiseI18nKeys.source.fields.keyName)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.apiKey)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.permissions)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.usage)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.updated)}</TableCell>
                {canEdit ? <TableCell /> : null}
                {canEdit ? <TableCell /> : null}
              </TableRow>
            </TableHead>
            <TableBody>
              {query.isLoading ? (
                [0, 1].map((index) => (
                  <TableRow key={index}>
                    {Array.from({ length: canEdit ? 7 : 5 }).map((_, cellIndex) => (
                      <TableCell key={cellIndex}><Skeleton variant="text" /></TableCell>
                    ))}
                  </TableRow>
                ))
              ) : rows.map((item) => (
                <APIKeyRow
                  item={item}
                  key={item.id}
                  revealedValue={revealed[item.id]}
                  onCopy={copySecret}
                  onDelete={deleteItem}
                  onEdit={(target) => {
                    setEditing(target);
                    setDialogOpen(true);
                  }}
                />
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

      <APIKeyDialog
        item={editing}
        open={dialogOpen}
        saving={upsertMutation.isPending}
        onClose={() => setDialogOpen(false)}
        onSubmit={(draft) => upsertMutation.mutate(draft)}
      />
    </section>
  );
}
