import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Skeleton, Stack, Tab, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, Tabs, TextField } from '@mui/material';
import { useState } from 'react';

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
import toolEmptySvg from '../../assets/images/tools_empty.svg';
import { buildSourceQuery, createResourceDraft, formatSourceDate, getSourcePageTotalPages, sourcePageSizeOptions, summarizeJson, toResourceDraft } from '../common/sourcePageUtils';

import { CustomMcpServerPanel } from './CustomMcpServerPanel';


export function FlowiseToolsNativePage() {
  const confirm = useConfirm();
  const message = useMessage();
  const { translate } = useI18n();
  const [activeTab, setActiveTab] = useState<'tools' | 'mcp'>('tools');
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<FlowiseResourceDto | null>(null);
  const [draft, setDraft] = useState<FlowiseResourceUpsertRequest>(() => createResourceDraft({ definitionJson: '{"schema":{},"code":""}' }));
  const query = useApiQuery({
    keepPreviousData: true,
    enabled: activeTab === 'tools',
    queryKey: ['flowise-source-tools', keyword, page, pageSize],
    queryFn: ({ signal }) => flowiseConfigurationResourcesApi.tools.list(buildSourceQuery(keyword, '', page, pageSize), signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (request: FlowiseResourceUpsertRequest) => editing ? flowiseConfigurationResourcesApi.tools.update(editing.id, request) : flowiseConfigurationResourcesApi.tools.create(request),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => { setDialogOpen(false); setEditing(null); await query.refetch(); }
  });
  const deleteMutation = useApiMutation({ mutationFn: (id: string) => flowiseConfigurationResourcesApi.tools.delete(id), onSuccess: async () => query.refetch() });
  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);

  const openToolDialog = (item?: FlowiseResourceDto) => {
    setEditing(item ?? null);
    setDraft(item ? toResourceDraft(item) : createResourceDraft({ definitionJson: '{"schema":{},"code":""}' }));
    setDialogOpen(true);
  };
  const deleteTool = (item: FlowiseResourceDto) => confirm({
    title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
    content: translate(flowiseI18nKeys.source.tools.deleteConfirm).replace('{name}', item.displayName),
    confirmText: translate(flowiseI18nKeys.actions.delete),
    onConfirm: async () => {
      await deleteMutation.mutateAsync(item.id);
    }
  });

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div><h1>{translate(flowiseI18nKeys.pages.tools)}</h1><p>{translate('flowise.native.tools.description')}</p></div>
        {activeTab === 'tools' ? (
          <PermissionMuiButton code={flowisePermissions.toolsCreate} startIcon={<AddIcon />} variant="contained" onClick={() => openToolDialog()}>
            {translate(flowiseI18nKeys.actions.addNew)}
          </PermissionMuiButton>
        ) : null}
      </header>
      <Tabs value={activeTab} onChange={(_, value: 'tools' | 'mcp') => setActiveTab(value)}>
        <Tab label={translate(flowiseI18nKeys.source.tools.customTools)} value="tools" />
        <Tab label={translate(flowiseI18nKeys.source.tools.customMcpServer)} value="mcp" />
      </Tabs>
      {activeTab === 'mcp' ? <CustomMcpServerPanel /> : (
        <>
          <Stack className="flowise-source-toolbar"><TextField fullWidth placeholder={translate(flowiseI18nKeys.source.tools.search)} size="small" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} /></Stack>
          {!query.isLoading && rows.length === 0 ? <Box className="flowise-source-empty"><img alt="ToolEmptySVG" src={toolEmptySvg} /><div>{translate(flowiseI18nKeys.source.tools.noTools)}</div></Box> : (
            <TableContainer className="flowise-source-table-container" component={Paper}>
              <Table>
                <TableHead><TableRow><TableCell>{translate(flowiseI18nKeys.fields.name)}</TableCell><TableCell>{translate(flowiseI18nKeys.source.tools.toolName)}</TableCell><TableCell>{translate(flowiseI18nKeys.fields.category)}</TableCell><TableCell>{translate(flowiseI18nKeys.fields.definitionJson)}</TableCell><TableCell>{translate(flowiseI18nKeys.fields.updated)}</TableCell><TableCell align="right">{translate(flowiseI18nKeys.source.fields.actions)}</TableCell></TableRow></TableHead>
                <TableBody>
                  {query.isLoading ? [0, 1].map((index) => <TableRow key={index}>{Array.from({ length: 6 }).map((_, cellIndex) => <TableCell key={cellIndex}><Skeleton /></TableCell>)}</TableRow>) : rows.map((item) => (
                    <TableRow hover key={item.id}>
                      <TableCell>{item.displayName}</TableCell>
                      <TableCell><code>{item.resourceKey}</code></TableCell>
                      <TableCell>{item.category || '-'}</TableCell>
                      <TableCell className="flowise-source-ellipsis">{summarizeJson(item.definitionJson)}</TableCell>
                      <TableCell>{formatSourceDate(item.updatedTime ?? item.createdTime)}</TableCell>
                      <TableCell align="right">
                        <PermissionMuiIconButton code={flowisePermissions.toolsEdit} color="primary" title={translate(flowiseI18nKeys.actions.edit)} onClick={() => openToolDialog(item)}>
                          <EditIcon fontSize="small" />
                        </PermissionMuiIconButton>
                        <PermissionMuiIconButton code={flowisePermissions.toolsDelete} color="error" title={translate(flowiseI18nKeys.actions.delete)} onClick={() => deleteTool(item)}>
                          <DeleteIcon fontSize="small" />
                        </PermissionMuiIconButton>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <TablePagination component="div" count={total} page={Math.max(0, page - 1)} rowsPerPage={pageSize} rowsPerPageOptions={[...sourcePageSizeOptions]} onPageChange={(_, nextPage) => setPage(Math.min(totalPages, nextPage + 1))} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }} />
            </TableContainer>
          )}
        </>
      )}
      <Dialog fullWidth maxWidth="md" open={dialogOpen} onClose={() => setDialogOpen(false)}>
        <DialogTitle>{editing ? translate('flowise.native.tools.editTitle') : translate('flowise.native.tools.createTitle')}</DialogTitle>
        <DialogContent><Stack spacing={2} sx={{ pt: 1 }}>
          <TextField label={translate(flowiseI18nKeys.fields.name)} value={draft.displayName} onChange={(event) => setDraft({ ...draft, displayName: event.target.value })} />
          <TextField label={translate(flowiseI18nKeys.source.tools.toolName)} value={draft.resourceKey} onChange={(event) => setDraft({ ...draft, resourceKey: event.target.value })} />
          <TextField label={translate(flowiseI18nKeys.fields.category)} value={draft.category ?? ''} onChange={(event) => setDraft({ ...draft, category: event.target.value })} />
          <TextField multiline label={translate(flowiseI18nKeys.source.tools.toolDefinitionJson)} minRows={8} value={draft.definitionJson ?? '{}'} onChange={(event) => setDraft({ ...draft, definitionJson: event.target.value })} />
          <TextField multiline label={translate(flowiseI18nKeys.fields.description)} minRows={2} value={draft.description ?? ''} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
        </Stack></DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>{translate(flowiseI18nKeys.common.cancel)}</Button>
          <PermissionMuiButton code={editing ? flowisePermissions.toolsEdit : flowisePermissions.toolsCreate} disabled={!draft.displayName || !draft.resourceKey || upsertMutation.isPending} variant="contained" onClick={() => upsertMutation.mutate(draft)}>
            {translate(flowiseI18nKeys.common.save)}
          </PermissionMuiButton>
        </DialogActions>
      </Dialog>
    </section>
  );
}
