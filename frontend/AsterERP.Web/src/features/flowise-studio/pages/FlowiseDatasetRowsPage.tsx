import UploadFileIcon from '@mui/icons-material/UploadFile';
import { useState } from 'react';
import { useParams } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { flowisePermissions } from '../../../shared/auth/permissionCodes';
import { PermissionMuiButton } from '../../../shared/auth/PermissionMuiButton';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { evaluationsApi } from '../api/evaluations.api';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { MainCard } from '../native/ui-component/cards/MainCard';
import { FlowListTable, type FlowListTableColumn } from '../native/ui-component/table/FlowListTable';
import { UploadCSVFileDialog } from '../native/views/datasets/UploadCSVFileDialog';
import type { FlowiseDatasetRowDto } from '../types/evaluation.types';

export function FlowiseDatasetRowsPage() {
  const { id = '' } = useParams();
  const { translate } = useI18n();
  const message = useMessage();
  const [uploadOpen, setUploadOpen] = useState(false);
  const datasetQuery = useApiQuery({
    enabled: Boolean(id),
    queryKey: ['flowise', 'dataset', id],
    queryFn: ({ signal }) => evaluationsApi.datasets.detail(id, signal)
  });
  const rowsQuery = useApiQuery({
    enabled: Boolean(id),
    queryKey: ['flowise', 'dataset', id, 'rows'],
    queryFn: ({ signal }) => evaluationsApi.datasets.rows(id, signal)
  });
  const uploadMutation = useApiMutation({
    mutationFn: ({ file, firstRowHeaders }: { file: File; firstRowHeaders: boolean }) => evaluationsApi.datasets.uploadCsv(id, file, firstRowHeaders),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.uploadFailed))),
    onSuccess: async (response) => {
      setUploadOpen(false);
      message.success(`${translate(flowiseI18nKeys.source.datasets.uploadSuccess)} (${response.data.importedRows})`);
      await Promise.all([datasetQuery.refetch(), rowsQuery.refetch()]);
    }
  });
  const columns: FlowListTableColumn<FlowiseDatasetRowDto>[] = [
    { key: 'input', title: translate(flowiseI18nKeys.fields.input), width: '320px', render: (item) => <span className="flowise-cell-ellipsis">{item.input}</span> },
    { key: 'expectedOutput', title: translate(flowiseI18nKeys.fields.expectedOutput), width: '320px', render: (item) => <span className="flowise-cell-ellipsis">{item.expectedOutput ?? '-'}</span> },
    { key: 'actualOutput', title: translate(flowiseI18nKeys.fields.actualOutput), width: '320px', render: (item) => <span className="flowise-cell-ellipsis">{item.actualOutput ?? '-'}</span> }
  ];

  return (
    <MainCard
      actions={(
        <PermissionMuiButton code={flowisePermissions.datasetsEdit} startIcon={<UploadFileIcon />} variant="contained" onClick={() => setUploadOpen(true)}>
          {translate(flowiseI18nKeys.source.datasets.uploadCsv)}
        </PermissionMuiButton>
      )}
      description={datasetQuery.data?.data.description ?? ''}
      title={datasetQuery.data?.data.name ?? translate(flowiseI18nKeys.detail.datasetRows)}
    >
      <FlowListTable
        columns={columns}
        emptyText={translate(flowiseI18nKeys.messages.noDatasetRows)}
        getRowKey={(item) => item.id}
        loading={rowsQuery.isLoading}
        rows={rowsQuery.data?.data ?? []}
      />
      <UploadCSVFileDialog
        datasetName={datasetQuery.data?.data.name ?? ''}
        open={uploadOpen}
        saving={uploadMutation.isPending}
        onClose={() => setUploadOpen(false)}
        onSubmit={(file, firstRowHeaders) => uploadMutation.mutate({ file, firstRowHeaders })}
      />
    </MainCard>
  );
}
