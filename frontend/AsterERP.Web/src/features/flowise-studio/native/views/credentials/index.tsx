import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import ShareIcon from '@mui/icons-material/Share';
import {
  Avatar,
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
import { PermissionMuiIconButton } from '../../../../../shared/auth/PermissionMuiIconButton';
import { useConfirm } from '../../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../../shared/utils/errorMessage';
import { flowiseConfigurationResourcesApi } from '../../../api/configurationResources.api';
import { flowiseStudioApi } from '../../../api/flowiseStudio.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceDto, FlowiseResourceUpsertRequest } from '../../../types/shared.types';
import credentialEmptySvg from '../../assets/images/credential_empty.svg';
import keySvg from '../../assets/images/key.svg';
import { ShareWithWorkspaceDialog } from '../../ui-component/dialog/ShareWithWorkspaceDialog';
import { buildSourceQuery, formatSourceDate, getSourcePageTotalPages, parseJsonRecord, sourcePageSizeOptions } from '../common/sourcePageUtils';

import { AddEditCredentialDialog } from './AddEditCredentialDialog';
import { CredentialListDialog } from './CredentialListDialog';

export function FlowiseCredentialsNativePage() {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const canEdit = usePermission(flowisePermissions.credentialsEdit).hasPermission;
  const canShare = usePermission(flowisePermissions.share).hasPermission;
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [selectedComponent, setSelectedComponent] = useState<FlowiseResourceDto | null>(null);
  const [editing, setEditing] = useState<FlowiseResourceDto | null>(null);
  const [shareTarget, setShareTarget] = useState<FlowiseResourceDto | null>(null);

  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-source-credentials', keyword, page, pageSize],
    queryFn: ({ signal }) => flowiseConfigurationResourcesApi.credentials.list(buildSourceQuery(keyword, '', page, pageSize), signal)
  });
  const componentsQuery = useApiQuery({
    queryKey: ['flowise-source-credential-components'],
    queryFn: ({ signal }) => flowiseConfigurationResourcesApi.credentials.list({ pageIndex: 1, pageSize: 500, status: 'Component' }, signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (draft: FlowiseResourceUpsertRequest) => editing
      ? flowiseConfigurationResourcesApi.credentials.update(editing.id, draft)
      : flowiseConfigurationResourcesApi.credentials.create(draft),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => {
      setDialogOpen(false);
      setEditing(null);
      setSelectedComponent(null);
      await query.refetch();
    }
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => flowiseConfigurationResourcesApi.credentials.delete(id),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => query.refetch()
  });
  const sharedWorkspacesQuery = useApiQuery({
    enabled: Boolean(shareTarget?.id),
    queryKey: ['flowise-source-credential-share', shareTarget?.id ?? 'none'],
    queryFn: ({ signal }) => flowiseStudioApi.sharedWorkspaces.list(shareTarget?.id ?? '', signal)
  });
  const saveShareMutation = useApiMutation({
    mutationFn: (workspaceIds: string[]) =>
      flowiseStudioApi.sharedWorkspaces.save(shareTarget?.id ?? '', { itemType: 'credential', workspaceIds }),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => {
      setShareTarget(null);
      message.success(translate(flowiseI18nKeys.messages.shareSaved));
      await Promise.all([sharedWorkspacesQuery.refetch(), query.refetch()]);
    }
  });

  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);
  const components = componentsQuery.data?.data.items ?? [];

  const deleteCredential = (credential: FlowiseResourceDto) => {
    confirm({
      title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
      content: `Delete credential ${credential.displayName}?`,
      confirmText: translate(flowiseI18nKeys.actions.delete),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(credential.id);
      }
    });
  };

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div>
          <h1>{translate(flowiseI18nKeys.pages.credentials)}</h1>
          <p>{translate(flowiseI18nKeys.source.credentials.description)}</p>
        </div>
        <PermissionMuiButton code={flowisePermissions.credentialsEdit} startIcon={<AddIcon />} variant="contained" onClick={() => setPickerOpen(true)}>
          {translate(flowiseI18nKeys.source.credentials.addCredential)}
        </PermissionMuiButton>
      </header>

      <Stack className="flowise-source-toolbar" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
        <TextField fullWidth placeholder={translate(flowiseI18nKeys.source.credentials.search)} size="small" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} />
      </Stack>

      {!query.isLoading && rows.length === 0 ? (
        <Box className="flowise-source-empty">
          <img alt="CredentialEmptySVG" src={credentialEmptySvg} />
          <div>{translate(flowiseI18nKeys.source.credentials.empty)}</div>
        </Box>
      ) : (
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>{translate(flowiseI18nKeys.source.fields.name)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.lastUpdated)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.created)}</TableCell>
                {canShare ? <TableCell /> : null}
                {canEdit ? <TableCell /> : null}
                {canEdit ? <TableCell /> : null}
              </TableRow>
            </TableHead>
            <TableBody>
              {query.isLoading ? [0, 1].map((index) => (
                <TableRow key={index}>
                  {Array.from({ length: 3 + (canShare ? 1 : 0) + (canEdit ? 2 : 0) }).map((_, cellIndex) => (
                    <TableCell key={cellIndex}><Skeleton variant="text" /></TableCell>
                  ))}
                </TableRow>
              )) : rows.map((credential) => {
                const metadata = parseJsonRecord(credential.metadataJson);
                const iconSrc = typeof metadata.iconSrc === 'string' ? metadata.iconSrc : keySvg;
                const shared = metadata.shared === true;
                return (
                  <TableRow hover key={credential.id}>
                    <TableCell>
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <Avatar className="flowise-source-avatar">
                          <img alt={credential.category ?? credential.displayName} src={iconSrc} onError={(event) => { event.currentTarget.src = keySvg; }} />
                        </Avatar>
                        <span>{credential.displayName}</span>
                      </Stack>
                    </TableCell>
                    <TableCell>{formatSourceDate(credential.updatedTime)}</TableCell>
                    <TableCell>{formatSourceDate(credential.createdTime)}</TableCell>
                    {canShare ? (
                      <TableCell align="center">
                        {shared ? <span>{translate(flowiseI18nKeys.source.credentials.sharedCredential)}</span> : (
                          <PermissionMuiIconButton code={flowisePermissions.share} color="primary" title={translate(flowiseI18nKeys.actions.share)} onClick={() => setShareTarget(credential)}>
                            <ShareIcon fontSize="small" />
                          </PermissionMuiIconButton>
                        )}
                      </TableCell>
                    ) : null}
                    {canEdit ? (
                      <TableCell align="center">
                        {!shared ? (
                          <PermissionMuiIconButton code={flowisePermissions.credentialsEdit} color="primary" title={translate(flowiseI18nKeys.actions.edit)} onClick={() => { setEditing(credential); setDialogOpen(true); }}>
                            <EditIcon fontSize="small" />
                          </PermissionMuiIconButton>
                        ) : null}
                      </TableCell>
                    ) : null}
                    {canEdit ? (
                      <TableCell align="center">
                        {!shared ? (
                          <PermissionMuiIconButton code={flowisePermissions.credentialsEdit} color="error" title={translate(flowiseI18nKeys.actions.delete)} onClick={() => deleteCredential(credential)}>
                            <DeleteIcon fontSize="small" />
                          </PermissionMuiIconButton>
                        ) : null}
                      </TableCell>
                    ) : null}
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

      <CredentialListDialog
        components={components}
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(component) => {
          setSelectedComponent(component);
          setEditing(null);
          setPickerOpen(false);
          setDialogOpen(true);
        }}
      />
      <AddEditCredentialDialog
        component={selectedComponent}
        item={editing}
        open={dialogOpen}
        saving={upsertMutation.isPending}
        onClose={() => setDialogOpen(false)}
        onSubmit={(draft) => upsertMutation.mutate(draft)}
      />
      <ShareWithWorkspaceDialog
        loading={sharedWorkspacesQuery.isLoading}
        open={Boolean(shareTarget)}
        saving={saveShareMutation.isPending}
        title={shareTarget?.displayName ?? ''}
        workspaces={sharedWorkspacesQuery.data?.data ?? []}
        onClose={() => setShareTarget(null)}
        onSave={(workspaceIds) => saveShareMutation.mutate(workspaceIds)}
      />
    </section>
  );
}
