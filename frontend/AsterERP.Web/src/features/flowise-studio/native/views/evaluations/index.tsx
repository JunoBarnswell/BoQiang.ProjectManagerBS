import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import PauseIcon from '@mui/icons-material/Pause';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import RefreshIcon from '@mui/icons-material/Refresh';
import ReplayIcon from '@mui/icons-material/Replay';
import { Box, Checkbox, IconButton, Paper, Skeleton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, ToggleButton, Tooltip } from '@mui/material';
import { useEffect, useState } from 'react';
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
import { evaluationsApi } from '../../../api/evaluations.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseEvaluationListItemDto, FlowiseEvaluationSaveRequest } from '../../../types/evaluation.types';
import evaluationsEmptySvg from '../../assets/images/empty_evals.svg';
import { buildSourceQuery, formatSourceDate, getSourcePageTotalPages, parseJsonRecord, readNumber, sourcePageSizeOptions } from '../common/sourcePageUtils';

import { CreateEvaluationDialog } from './CreateEvaluationDialog';

export function FlowiseEvaluationsNativePage() {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<FlowiseEvaluationListItemDto | null>(null);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-source-evaluations', page, pageSize],
    queryFn: ({ signal }) => evaluationsApi.evaluations.list(buildSourceQuery('', '', page, pageSize), signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (draft: FlowiseEvaluationSaveRequest) => editing ? evaluationsApi.evaluations.update(editing.id, draft) : evaluationsApi.evaluations.create(draft),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => { setDialogOpen(false); setEditing(null); await query.refetch(); }
  });
  const runAgainMutation = useApiMutation({
    mutationFn: (id: string) => evaluationsApi.evaluations.runAgain(id),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.runAgainFailed))),
    onSuccess: () => message.success(translate(flowiseI18nKeys.messages.runCompleted))
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => evaluationsApi.evaluations.delete(id),
    onSuccess: async () => query.refetch()
  });

  const rows = query.data?.data.items ?? [];
  const { refetch } = query;
  const total = query.data?.data.total ?? 0;
  const totalPages = getSourcePageTotalPages(total, pageSize);
  const allSelected = rows.length > 0 && selectedIds.length === rows.length;

  useEffect(() => {
    if (!autoRefresh) {
      return;
    }

    const timer = window.setInterval(() => {
      void refetch();
    }, 5000);
    return () => window.clearInterval(timer);
  }, [autoRefresh, refetch]);

  const deleteEvaluation = (item: FlowiseEvaluationListItemDto) => confirm({
    title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
    content: `Delete evaluation ${item.name}?`,
    confirmText: translate(flowiseI18nKeys.actions.delete),
    onConfirm: async () => {
      await deleteMutation.mutateAsync(item.id);
    }
  });

  const deleteSelected = () => confirm({
    title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
    content: `Delete ${selectedIds.length} selected evaluation${selectedIds.length === 1 ? '' : 's'}?`,
    confirmText: translate(flowiseI18nKeys.actions.delete),
    onConfirm: async () => {
      await Promise.all(selectedIds.map((id) => deleteMutation.mutateAsync(id)));
      setSelectedIds([]);
      await query.refetch();
    }
  });

  const toggleSelection = (id: string, checked: boolean) => {
    setSelectedIds((current) => checked ? [...new Set([...current, id])] : current.filter((item) => item !== id));
  };

  const toggleAll = (checked: boolean) => {
    setSelectedIds(checked ? rows.map((item) => item.id) : []);
  };

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div><h1>{translate(flowiseI18nKeys.pages.evaluations)}</h1><p>{translate(flowiseI18nKeys.source.evaluations.description)}</p></div>
        <Stack direction="row" spacing={1}>
          <Tooltip title={translate(flowiseI18nKeys.source.evaluations.autoRefresh)}>
            <ToggleButton selected={autoRefresh} size="small" value="auto-refresh" onChange={() => setAutoRefresh((current) => !current)}>
              {autoRefresh ? <PauseIcon fontSize="small" /> : <PlayArrowIcon fontSize="small" />}
            </ToggleButton>
          </Tooltip>
          <IconButton title={translate(flowiseI18nKeys.actions.refresh)} onClick={() => void query.refetch()}><RefreshIcon fontSize="small" /></IconButton>
          <PermissionMuiButton code={flowisePermissions.evaluationsEdit} startIcon={<AddIcon />} variant="contained" onClick={() => { setEditing(null); setDialogOpen(true); }}>
            {translate(flowiseI18nKeys.source.evaluations.newEvaluation)}
          </PermissionMuiButton>
        </Stack>
      </header>
      {selectedIds.length > 0 ? (
        <PermissionMuiButton code={flowisePermissions.evaluationsEdit} color="error" startIcon={<DeleteIcon />} sx={{ alignSelf: 'flex-start' }} variant="outlined" onClick={deleteSelected}>
          {translate(flowiseI18nKeys.source.evaluations.deleteSelected)} ({selectedIds.length})
        </PermissionMuiButton>
      ) : null}
      {!query.isLoading && rows.length === 0 ? (
        <Box className="flowise-source-empty"><img alt="empty_evals" src={evaluationsEmptySvg} /><div>{translate(flowiseI18nKeys.source.evaluations.empty)}</div></Box>
      ) : (
        <TableContainer className="flowise-source-table-container" component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox"><Checkbox checked={allSelected} indeterminate={selectedIds.length > 0 && !allSelected} onChange={(event) => toggleAll(event.target.checked)} /></TableCell>
                <TableCell />
                <TableCell>{translate(flowiseI18nKeys.source.fields.name)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.latestVersion)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.averageMetrics)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.lastEvaluated)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.flows)}</TableCell>
                <TableCell>{translate(flowiseI18nKeys.source.fields.dataset)}</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {query.isLoading ? [0, 1].map((index) => <TableRow key={index}>{Array.from({ length: 9 }).map((_, cellIndex) => <TableCell key={cellIndex}><Skeleton /></TableCell>)}</TableRow>) : rows.map((item) => {
                const metadata = parseJsonRecord(item.advancedMetadataJson);
                const flows = item.definition.targetFlowId ? [item.definition.targetFlowId] : [];
                const latestVersion = readNumber(metadata.latestVersion ?? metadata.versionNo) || 1;
                return (
                  <TableRow hover key={item.id}>
                    <TableCell padding="checkbox"><Checkbox checked={selectedIds.includes(item.id)} onChange={(event) => toggleSelection(item.id, event.target.checked)} /></TableCell>
                    <TableCell>{latestVersion > 1 ? latestVersion : null}</TableCell>
                    <TableCell><Link className="flowise-native-table-link" to={`/flowise/evaluation_results/${item.id}`}>{item.name}</Link></TableCell>
                    <TableCell>{latestVersion}</TableCell>
                    <TableCell>
                      <Stack spacing={0.25}>
                        <span>{translate(flowiseI18nKeys.detail.passRate)}: {readNumber(metadata.passRate)}%</span>
                        <span>{translate(flowiseI18nKeys.fields.tokens)}: {readNumber(metadata.totalTokens)}</span>
                      </Stack>
                    </TableCell>
                    <TableCell>{formatSourceDate(item.updatedTime ?? item.createdTime)}</TableCell>
                    <TableCell className="flowise-source-ellipsis">{flows.length ? flows.join(', ') : '-'}</TableCell>
                    <TableCell className="flowise-source-ellipsis">{String(metadata.datasetName ?? item.definition.datasetId ?? '-')}</TableCell>
                    <TableCell align="right">
                      <IconButton component={Link} title={translate(flowiseI18nKeys.detail.evaluationResult)} to={`/flowise/evaluation_results/${item.id}`}><ReplayIcon fontSize="small" /></IconButton>
                      <PermissionMuiIconButton code={[flowisePermissions.run, flowisePermissions.retry]} color="primary" disabled={runAgainMutation.isPending} title={translate(flowiseI18nKeys.actions.run)} onClick={() => runAgainMutation.mutate(item.id)}>
                        <ReplayIcon fontSize="small" />
                      </PermissionMuiIconButton>
                      <PermissionMuiIconButton code={flowisePermissions.evaluationsEdit} color="primary" title={translate(flowiseI18nKeys.actions.edit)} onClick={() => { setEditing(item); setDialogOpen(true); }}>
                        <EditIcon fontSize="small" />
                      </PermissionMuiIconButton>
                      <PermissionMuiIconButton code={flowisePermissions.evaluationsEdit} color="error" title={translate(flowiseI18nKeys.actions.delete)} onClick={() => deleteEvaluation(item)}>
                        <DeleteIcon fontSize="small" />
                      </PermissionMuiIconButton>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
          <TablePagination component="div" count={total} page={Math.max(0, page - 1)} rowsPerPage={pageSize} rowsPerPageOptions={[...sourcePageSizeOptions]} onPageChange={(_, nextPage) => setPage(Math.min(totalPages, nextPage + 1))} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }} />
        </TableContainer>
      )}
      <CreateEvaluationDialog item={editing} open={dialogOpen} saving={upsertMutation.isPending} onClose={() => setDialogOpen(false)} onSubmit={(draft) => upsertMutation.mutate(draft)} />
    </section>
  );
}
