import { Button, FormControl, InputLabel, MenuItem, Select, Stack, TextField, ToggleButton, ToggleButtonGroup } from '@mui/material';
import { useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { PermissionButton } from '../../../../../shared/auth/PermissionButton';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { useConfirm } from '../../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../../shared/utils/errorMessage';
import { customMcpServersApi } from '../../../api/customMcpServers.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import { translateFlowiseStatus } from '../../../i18n/flowiseTranslate';
import type { FlowiseCustomMcpServerDto, FlowiseCustomMcpServerUpsertRequest } from '../../../types/customMcpServer.types';
import { ItemCard } from '../../ui-component/cards/ItemCard';
import { FlowListTable, type FlowListTableColumn } from '../../ui-component/table/FlowListTable';

import { CustomMcpServerDialog } from './CustomMcpServerDialog';

export function CustomMcpServerPanel() {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(12);
  const [viewMode, setViewMode] = useState<'card' | 'list'>('card');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'add' | 'edit'>('add');
  const [editing, setEditing] = useState<FlowiseCustomMcpServerDto | null>(null);

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-custom-mcp-servers', keyword, pageIndex, pageSize],
    queryFn: ({ signal }) => customMcpServersApi.list({ keyword, pageIndex, pageSize }, signal)
  });
  const toolsQuery = useApiQuery({
    enabled: Boolean(editing && dialogOpen),
    queryKey: ['flowise-custom-mcp-server-tools', editing?.id],
    queryFn: ({ signal }) => customMcpServersApi.tools(editing?.id ?? '', signal)
  });

  const saveMutation = useApiMutation({
    mutationFn: (request: FlowiseCustomMcpServerUpsertRequest) => editing ? customMcpServersApi.update(editing.id, request) : customMcpServersApi.create(request),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.customMcpServerSaveFailed))),
    onSuccess: async (response) => {
      const saved = response.data;
      if (dialogMode === 'add') {
        setEditing(saved);
        setDialogMode('edit');
      }
      await authorizeMutation.mutateAsync(saved.id);
      await listQuery.refetch();
    }
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => customMcpServersApi.delete(id),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => listQuery.refetch()
  });
  const authorizeMutation = useApiMutation({
    mutationFn: (id: string) => customMcpServersApi.authorize(id),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.customMcpServerAuthorizeFailed))),
    onSuccess: async (response) => {
      if (response.data.status === 'Error') {
        message.error(response.data.errorMessage ?? translate(flowiseI18nKeys.messages.customMcpServerAuthorizeFailed));
      } else {
        message.success(translate(flowiseI18nKeys.messages.customMcpServerAuthorized).replace('{count}', String(response.data.toolCount)));
      }
      await listQuery.refetch();
      await toolsQuery.refetch();
    }
  });

  const rows = listQuery.data?.data.items ?? [];
  const total = listQuery.data?.data.total ?? 0;
  const columns: FlowListTableColumn<FlowiseCustomMcpServerDto>[] = [
    { key: 'name', title: translate(flowiseI18nKeys.fields.name), render: (item) => <strong>{item.name}</strong> },
    { key: 'serverUrl', title: translate(flowiseI18nKeys.customMcp.serverUrl), render: (item) => <span className="flowise-cell-ellipsis">{item.serverUrl}</span> },
    { key: 'toolCount', title: translate(flowiseI18nKeys.customMcp.tools), render: (item) => String(item.toolCount) },
    { key: 'status', title: translate(flowiseI18nKeys.fields.status), render: (item) => translateFlowiseStatus(item.status, translate) },
    { key: 'updatedTime', title: translate(flowiseI18nKeys.fields.updated), render: (item) => item.updatedTime ? new Date(item.updatedTime).toLocaleString() : '-' }
  ];

  const openCreate = () => {
    setEditing(null);
    setDialogMode('add');
    setDialogOpen(true);
  };
  const openEdit = async (item: FlowiseCustomMcpServerDto) => {
    try {
      const response = await customMcpServersApi.get(item.id);
      setEditing(response.data);
      setDialogMode('edit');
      setDialogOpen(true);
    } catch (error) {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.loadFailed)));
    }
  };
  const deleteItem = (item: FlowiseCustomMcpServerDto) => {
    confirm({
      title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
      content: item.name,
      onConfirm: async () => {
        await deleteMutation.mutateAsync(item.id);
      }
    });
  };

  return (
    <>
      <Stack className="flowise-native-toolbar flowise-custom-mcp-toolbar" direction={{ xs: 'column', md: 'row' }} spacing={1.5}>
        <TextField
          placeholder={translate(flowiseI18nKeys.customMcp.searchPlaceholder)}
          value={keyword}
          onChange={(event) => {
            setKeyword(event.target.value);
            setPageIndex(1);
          }}
        />
        <ToggleButtonGroup
          exclusive
          value={viewMode}
          onChange={(_, value: 'card' | 'list' | null) => {
            if (value) {
              setViewMode(value);
            }
          }}
        >
          <ToggleButton value="card">{translate(flowiseI18nKeys.actions.cardView)}</ToggleButton>
          <ToggleButton value="list">{translate(flowiseI18nKeys.actions.listView)}</ToggleButton>
        </ToggleButtonGroup>
        <PermissionButton className="btn-primary" code={flowisePermissions.toolsCreate} onClick={openCreate} type="button">
          {translate(flowiseI18nKeys.customMcp.add)}
        </PermissionButton>
      </Stack>
      {viewMode === 'card' ? (
        <div className="flowise-native-card-grid">
          {rows.map((item) => (
            <ItemCard
              actions={<CustomMcpServerActions authorizing={authorizeMutation.isPending} item={item} onAuthorize={(target) => authorizeMutation.mutate(target.id)} onDelete={deleteItem} onEdit={(target) => { void openEdit(target); }} />}
              badge={<span className={`flowise-status flowise-status--${item.status.toLowerCase()}`}>{translateFlowiseStatus(item.status, translate)}</span>}
              icon="plug"
              key={item.id}
              meta={<CustomMcpServerMeta item={item} />}
              subtitle={item.serverUrl}
              title={item.name}
            >
              <p>{item.errorMessage || translate(flowiseI18nKeys.customMcp.toolCount).replace('{count}', String(item.toolCount))}</p>
            </ItemCard>
          ))}
        </div>
      ) : (
        <FlowListTable
          columns={columns}
          emptyText={translate(flowiseI18nKeys.customMcp.empty)}
          getRowKey={(item) => item.id}
          loading={listQuery.isLoading}
          rowActions={(item) => <CustomMcpServerActions authorizing={authorizeMutation.isPending} item={item} onAuthorize={(target) => authorizeMutation.mutate(target.id)} onDelete={deleteItem} onEdit={(target) => { void openEdit(target); }} />}
          rows={rows}
        />
      )}
      {!listQuery.isLoading && rows.length === 0 && viewMode === 'card' ? <div className="flowise-native-empty">{translate(flowiseI18nKeys.customMcp.empty)}</div> : null}
      {total > 0 ? (
        <Stack className="flowise-native-pagination" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
          <Button disabled={pageIndex <= 1} type="button" variant="outlined" onClick={() => setPageIndex((value) => Math.max(1, value - 1))}>
            {translate(flowiseI18nKeys.actions.previous)}
          </Button>
          <span>{translate(flowiseI18nKeys.customMcp.pagination).replace('{page}', String(pageIndex)).replace('{total}', String(total))}</span>
          <FormControl size="small">
            <InputLabel id="custom-mcp-page-size-label">{translate(flowiseI18nKeys.fields.pageSize)}</InputLabel>
            <Select
              label={translate(flowiseI18nKeys.fields.pageSize)}
              labelId="custom-mcp-page-size-label"
              value={pageSize}
              onChange={(event) => {
                setPageSize(Number(event.target.value));
                setPageIndex(1);
              }}
            >
              <MenuItem value={12}>12</MenuItem>
              <MenuItem value={24}>24</MenuItem>
              <MenuItem value={48}>48</MenuItem>
            </Select>
          </FormControl>
          <Button disabled={pageIndex * pageSize >= total} type="button" variant="outlined" onClick={() => setPageIndex((value) => value + 1)}>
            {translate(flowiseI18nKeys.actions.next)}
          </Button>
        </Stack>
      ) : null}

      <CustomMcpServerDialog
        authorizing={authorizeMutation.isPending}
        mode={dialogMode}
        open={dialogOpen}
        saving={saveMutation.isPending}
        server={editing}
        tools={toolsQuery.data?.data ?? []}
        toolsLoading={toolsQuery.isLoading}
        onAuthorize={(server) => authorizeMutation.mutate(server.id)}
        onClose={() => setDialogOpen(false)}
        onDelete={deleteItem}
        onSave={(request) => saveMutation.mutate(request)}
      />
    </>
  );
}

function CustomMcpServerActions({
  authorizing,
  item,
  onAuthorize,
  onDelete,
  onEdit
}: {
  authorizing: boolean;
  item: FlowiseCustomMcpServerDto;
  onAuthorize: (item: FlowiseCustomMcpServerDto) => void;
  onDelete: (item: FlowiseCustomMcpServerDto) => void;
  onEdit: (item: FlowiseCustomMcpServerDto) => void;
}) {
  const { translate } = useI18n();
  return (
    <>
      <PermissionButton className="btn-secondary" code={flowisePermissions.toolsUpdate} iconStart={false} onClick={() => onEdit(item)} type="button">
        {translate(flowiseI18nKeys.actions.edit)}
      </PermissionButton>
      <PermissionButton className="btn-secondary" code={[flowisePermissions.toolsUpdate, flowisePermissions.toolsCreate]} disabled={authorizing} iconStart={false} onClick={() => onAuthorize(item)} type="button">
        {translate(flowiseI18nKeys.customMcp.authorize)}
      </PermissionButton>
      <PermissionButton className="btn-ghost danger" code={flowisePermissions.toolsDelete} iconStart={false} onClick={() => onDelete(item)} type="button">
        {translate(flowiseI18nKeys.actions.delete)}
      </PermissionButton>
    </>
  );
}

function CustomMcpServerMeta({ item }: { item: FlowiseCustomMcpServerDto }) {
  return (
    <>
      <span>{item.authType}</span>
      {item.workspaceId ? <span>{item.workspaceId}</span> : null}
      {item.toolCount > 0 ? <span>{item.toolCount}</span> : null}
    </>
  );
}
