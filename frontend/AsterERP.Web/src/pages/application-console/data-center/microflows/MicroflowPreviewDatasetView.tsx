import type {
  ApplicationDataCenterPreviewResponse,
  MicroflowPreviewDataset,
  MicroflowPreviewResponse
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { WorkbenchResultViewer } from '../workbench/components/WorkbenchResultViewer';

interface MicroflowPreviewDatasetViewProps {
  errorMessage?: string | null;
  loading?: boolean;
  onSelectDataset: (key: string) => void;
  result: MicroflowPreviewResponse | null;
  selectedDatasetKey: string | null;
}

export function MicroflowPreviewDatasetView({
  errorMessage,
  loading = false,
  onSelectDataset,
  result,
  selectedDatasetKey
}: MicroflowPreviewDatasetViewProps) {
  if (loading) {
    return <div className="microflow-preview-empty">{translateCurrentLiteral("正在执行微流...")}</div>;
  }

  if (errorMessage) {
    return <div className="microflow-preview-error">{errorMessage}</div>;
  }

  if (!result) {
    return <div className="microflow-preview-empty">{translateCurrentLiteral("选择模式并运行后查看数据集")}</div>;
  }

  if (result.datasets.length === 0) {
    return <div className="microflow-preview-empty">{result.message || '本次运行没有返回可表格化的数据'}</div>;
  }

  const selectedDataset = findSelectedDataset(result.datasets, selectedDatasetKey) ?? result.datasets[0];
  return (
    <div className="microflow-preview-dataset">
      <div className="microflow-preview-tabs" role="tablist" aria-label="微流结果数据集">
        {result.datasets.map((dataset) => (
          <button
            aria-selected={dataset.key === selectedDataset.key}
            className="microflow-preview-tab"
            key={dataset.key}
            role="tab"
            type="button"
            onClick={() => onSelectDataset(dataset.key)}
          >
            <span>{dataset.key}</span>
            <small>{dataset.totalRows}{dataset.truncated ? '+' : ''}</small>
          </button>
        ))}
      </div>
      <div className="microflow-preview-dataset__meta">
        <span>{selectedDataset.title}</span>
        <span>{selectedDataset.sourcePath}</span>
        {selectedDataset.truncated ? <span>{translateCurrentLiteral("已截断显示")}</span> : null}
      </div>
      <WorkbenchResultViewer preview={toWorkbenchPreview(selectedDataset)} />
    </div>
  );
}

function findSelectedDataset(datasets: MicroflowPreviewDataset[], selectedDatasetKey: string | null) {
  if (!selectedDatasetKey) {
    return null;
  }

  return datasets.find((dataset) => dataset.key === selectedDatasetKey) ?? null;
}

function toWorkbenchPreview(dataset: MicroflowPreviewDataset): ApplicationDataCenterPreviewResponse {
  return {
    fields: dataset.fields,
    message: `${dataset.title} · ${dataset.totalRows} 行${dataset.truncated ? ' · 已截断' : ''}`,
    rows: dataset.rows
  };
}
