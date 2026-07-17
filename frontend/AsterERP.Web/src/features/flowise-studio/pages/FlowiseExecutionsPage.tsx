import SearchIcon from '@mui/icons-material/Search';
import {
  Button,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField
} from '@mui/material';
import { useMemo, useState } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { flowisePermissions } from '../../../shared/auth/permissionCodes';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { flowiseStudioApi } from '../api/flowiseStudio.api';
import type { FlowiseExecutionDto } from '../flowiseStudio.types';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { translateFlowiseStatus } from '../i18n/flowiseTranslate';
import { MainCard } from '../native/ui-component/cards/MainCard';
import { FlowListTable, type FlowListTableColumn } from '../native/ui-component/table/FlowListTable';

export function FlowiseExecutionsPage() {
  const confirm = useConfirm();
  const message = useMessage();
  const { translate } = useI18n();
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchDraft, setSearchDraft] = useState({ keyword: '', status: '' });
  const [search, setSearch] = useState(searchDraft);

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise', 'executions', pageIndex, pageSize, search],
    queryFn: ({ signal }) => flowiseStudioApi.executions.list({ ...search, pageIndex, pageSize }, signal)
  });

  const deleteMutation = useApiMutation({
    mutationFn: flowiseStudioApi.executions.delete,
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.deleteExecutionFailed))),
    onSuccess: async () => {
      message.success(translate(flowiseI18nKeys.messages.executionDeleted));
      await listQuery.refetch();
    }
  });

  const columns = useMemo<FlowListTableColumn<FlowiseExecutionDto>[]>(
    () => [
      {
        key: 'resourceName',
        title: translate(flowiseI18nKeys.fields.resource),
        width: '220px',
        render: (item) => (
          <div className="flowise-table-title">
            <strong>{item.resourceName}</strong>
            <span>{item.flowType}</span>
          </div>
        )
      },
      {
        key: 'status',
        title: translate(flowiseI18nKeys.fields.status),
        width: '120px',
        render: (item) => <span className={`flowise-status flowise-status--${item.status.toLowerCase()}`}>{translateFlowiseStatus(item.status, translate)}</span>
      },
      { key: 'durationMs', title: translate(flowiseI18nKeys.fields.duration), width: '110px', render: (item) => `${item.durationMs} ms` },
      { key: 'traceId', title: translate(flowiseI18nKeys.fields.traceId), width: '260px', render: (item) => <code>{item.traceId}</code> },
      { key: 'errorCode', title: translate(flowiseI18nKeys.fields.error), width: '240px', render: (item) => item.errorCode ? `${item.errorCode}: ${item.errorMessage ?? ''}` : '-' },
      { key: 'createdTime', title: translate(flowiseI18nKeys.fields.created), width: '180px', render: (item) => new Date(item.createdTime).toLocaleString() }
    ],
    [translate]
  );

  const rows = listQuery.data?.data.items ?? [];
  return (
    <MainCard
      description={translate(flowiseI18nKeys.executions.description)}
      title={translate(flowiseI18nKeys.pages.executions)}
      toolbar={
        <div className="flowise-native-toolbar">
          <TextField
            placeholder={translate(flowiseI18nKeys.search.executions)}
            size="small"
            value={searchDraft.keyword}
            onChange={(event) => setSearchDraft((current) => ({ ...current, keyword: event.target.value }))}
          />
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="flowise-executions-status-label">{translate(flowiseI18nKeys.fields.status)}</InputLabel>
            <Select
              label={translate(flowiseI18nKeys.fields.status)}
              labelId="flowise-executions-status-label"
              value={searchDraft.status}
              onChange={(event) => setSearchDraft((current) => ({ ...current, status: event.target.value }))}
            >
              <MenuItem value="">{translate(flowiseI18nKeys.common.allStatus)}</MenuItem>
              <MenuItem value="Running">{translate(flowiseI18nKeys.status.running)}</MenuItem>
              <MenuItem value="Completed">{translate(flowiseI18nKeys.status.completed)}</MenuItem>
              <MenuItem value="Failed">{translate(flowiseI18nKeys.status.failed)}</MenuItem>
            </Select>
          </FormControl>
          <Button startIcon={<SearchIcon />} variant="outlined" onClick={() => { setPageIndex(1); setSearch(searchDraft); }}>
            {translate(flowiseI18nKeys.actions.search)}
          </Button>
          <Button variant="text" onClick={() => { const next = { keyword: '', status: '' }; setSearchDraft(next); setSearch(next); setPageIndex(1); }}>
            {translate(flowiseI18nKeys.common.reset)}
          </Button>
        </div>
      }
    >
      <FlowListTable
        columns={columns}
        emptyText={translate(flowiseI18nKeys.messages.noExecutions)}
        getRowKey={(item) => item.id}
        loading={listQuery.isLoading}
        rowActions={(item) => (
          <div className="flowise-row-actions">
            <details>
              <summary className="op-link">{translate(flowiseI18nKeys.common.output)}</summary>
              <pre>{item.status === 'Failed' ? item.errorMessage : item.outputJson}</pre>
            </details>
            <PermissionButton className="op-link danger" code={flowisePermissions.executionsManage} disabled={deleteMutation.isPending} iconStart={false} onClick={() => {
              confirm({
                title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
                content: translate(flowiseI18nKeys.messages.deleteExecutionConfirm).replace('{traceId}', item.traceId),
                onConfirm: async () => {
                  await deleteMutation.mutateAsync(item.id);
                }
              });
            }} type="button">
              {translate(flowiseI18nKeys.actions.delete)}
            </PermissionButton>
          </div>
        )}
        rows={rows}
      />
      <Stack className="flowise-native-pagination" direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Button disabled={pageIndex <= 1} variant="outlined" onClick={() => setPageIndex((current) => Math.max(1, current - 1))}>
          {translate(flowiseI18nKeys.actions.previous)}
        </Button>
        <span>{pageIndex}</span>
        <Button disabled={rows.length < pageSize} variant="outlined" onClick={() => setPageIndex((current) => current + 1)}>
          {translate(flowiseI18nKeys.actions.next)}
        </Button>
        <FormControl size="small" sx={{ minWidth: 100 }}>
          <InputLabel id="flowise-executions-page-size-label">{translate(flowiseI18nKeys.fields.pageSize)}</InputLabel>
          <Select
            label={translate(flowiseI18nKeys.fields.pageSize)}
            labelId="flowise-executions-page-size-label"
            value={String(pageSize)}
            onChange={(event) => { setPageSize(Number(event.target.value)); setPageIndex(1); }}
          >
            <MenuItem value="20">20</MenuItem>
            <MenuItem value="50">50</MenuItem>
            <MenuItem value="100">100</MenuItem>
          </Select>
        </FormControl>
      </Stack>
    </MainCard>
  );
}
