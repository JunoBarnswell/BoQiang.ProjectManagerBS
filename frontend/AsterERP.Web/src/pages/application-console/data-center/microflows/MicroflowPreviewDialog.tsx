import { Database, Play } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';


import { previewApplicationDataCenterMicroflow } from '../../../../api/application-data-center/applicationDataCenter.api';
import type {
  MicroflowDefinition,
  MicroflowPreviewMode,
  MicroflowPreviewRequest,
  MicroflowPreviewResponse
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { ResponsiveModal } from '../../../../shared/responsive/ResponsiveModal';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';

import { normalizeMicroflowDefinitionForSave } from './microflowDefinitionNormalizer';
import { buildReturnResultPathOptions } from './microflowNodeContext';
import { MicroflowPreviewDatasetView } from './MicroflowPreviewDatasetView';
import { MicroflowPreviewDebugPanel } from './MicroflowPreviewDebugPanel';
import {
  createInitialVariableValues,
  listPreviewInputVariables,
  serializeVariableValues,
  validateVariableValues,
  type MicroflowVariableInputValue
} from './microflowVariableSchema';
import { MicroflowVariableValueEditor } from './MicroflowVariableValueEditor';
import './microflowPreview.css';

interface MicroflowPreviewDialogProps {
  autoRunNonce?: number;
  definition: MicroflowDefinition | null;
  microflowId: string | null;
  onClose: () => void;
  open: boolean;
  title: string;
}

export function MicroflowPreviewDialog({
  autoRunNonce = 0,
  definition,
  microflowId,
  onClose,
  open,
  title
}: MicroflowPreviewDialogProps) {
  const message = useMessage();
  const [mode, setMode] = useState<MicroflowPreviewMode>('draft');
  const [variableValues, setVariableValues] = useState<Record<string, MicroflowVariableInputValue>>({});
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [result, setResult] = useState<MicroflowPreviewResponse | null>(null);
  const [selectedDatasetKey, setSelectedDatasetKey] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const lastAutoRunNonceRef = useRef(0);

  const canRun = Boolean(open && microflowId && definition);
  const previewVariables = useMemo(() => listPreviewInputVariables(definition), [definition]);
  const preferredResultPathOptions = useMemo(() => buildReturnResultPathOptions(definition), [definition]);
  const preferredResultPath = preferredResultPathOptions[0] ?? '';
  const serializedVariables = useMemo(
    () => serializeVariableValues(previewVariables, variableValues),
    [previewVariables, variableValues]
  );
  const runMutation = useApiMutation({
    mutationFn: (request: MicroflowPreviewRequest) => previewApplicationDataCenterMicroflow(microflowId ?? '', request),
    onError: (error) => {
      const nextMessage = getErrorMessage(error, '预览微流失败');
      setErrorMessage(nextMessage);
      message.error(nextMessage);
    },
    onSuccess: (response) => {
      setErrorMessage(null);
      setResult(response.data);
      setSelectedDatasetKey(response.data.primaryDatasetKey ?? response.data.datasets[0]?.key ?? null);
      message.success(response.data.message || '预览执行完成');
    }
  });

  const runPreviewWithValues = useCallback((nextMode: MicroflowPreviewMode, nextVariableValues: Record<string, MicroflowVariableInputValue>) => {
    if (!canRun) {
      setErrorMessage('请先保存微流后再预览当前画布定义');
      return;
    }

    const validation = validateVariableValues(previewVariables, nextVariableValues);
    setValidationErrors(validation.errors);
    if (!validation.valid) {
      const nextMessage = '表单信息校验失败，请修正标红字段后再运行';
      setErrorMessage(nextMessage);
      message.error(nextMessage);
      return;
    }

    const draftDefinition = definition ? normalizeMicroflowDefinitionForSave(definition) : null;
    runMutation.mutate({
      draftConfigJson: nextMode === 'draft' && draftDefinition ? JSON.stringify(draftDefinition) : null,
      executeRequest: {
        variables: serializeVariableValues(previewVariables, nextVariableValues)
      },
      maxRows: 100,
      mode: nextMode,
      preferredResultPath: preferredResultPath || null
    });
  }, [canRun, definition, message, preferredResultPath, previewVariables, runMutation]);

  useEffect(() => {
    if (!open) {
      return;
    }

    setVariableValues(createInitialVariableValues(previewVariables));
    setValidationErrors({});
    setResult(null);
    setSelectedDatasetKey(null);
    setErrorMessage(null);
  }, [open, previewVariables]);

  useEffect(() => {
    if (!open || !autoRunNonce || autoRunNonce === lastAutoRunNonceRef.current) {
      return;
    }

    lastAutoRunNonceRef.current = autoRunNonce;
    setMode('draft');
    const initialValues = createInitialVariableValues(previewVariables);
    setVariableValues(initialValues);
    setValidationErrors({});
    setResult(null);
    setSelectedDatasetKey(null);
    setErrorMessage(null);
    runPreviewWithValues('draft', initialValues);
  }, [autoRunNonce, open, previewVariables, runPreviewWithValues]);

  return (
    <ResponsiveModal
      bodyClassName="microflow-preview-modal__body"
      className="microflow-preview-modal"
      fullscreenOnSmall
      maxWidth={1440}
      mode="modal"
      open={open}
      title={`${title || '微流'}预览`}
      onClose={onClose}
    >
      <div className="microflow-preview-shell">
        <section className="microflow-preview-toolbar">
          <div className="microflow-preview-mode" role="tablist" aria-label="微流预览模式">
            <button aria-selected={mode === 'draft'} role="tab" type="button" onClick={() => setMode('draft')}>{translateCurrentLiteral("草稿预览")}</button>
            <button aria-selected={mode === 'published'} role="tab" type="button" onClick={() => setMode('published')}>{translateCurrentLiteral("已发布运行")}</button>
          </div>
          <div className="microflow-preview-result-hint">
            <span>{translateCurrentLiteral("返回路径")}</span>
            <strong>{preferredResultPath || '运行后自动识别'}</strong>
          </div>
          <button className="primary-button h-8 text-xs" disabled={!canRun || runMutation.isPending} type="button" onClick={() => runPreviewWithValues(mode, variableValues)}>
            <Play className="h-3.5 w-3.5" />
            {runMutation.isPending ? '运行中' : mode === 'draft' ? '运行草稿' : '运行已发布'}
          </button>
        </section>

        <section className="microflow-preview-variables">
          <header>
            <div>
              <strong>{translateCurrentLiteral("变量")}</strong>
              <span>{Object.keys(serializedVariables).length} 项</span>
            </div>
          </header>
          <MicroflowVariableValueEditor
            errors={validationErrors}
            values={variableValues}
            variables={previewVariables}
            onChange={(nextValues) => {
              setVariableValues(nextValues);
              setValidationErrors({});
            }}
          />
        </section>

        <section className="microflow-preview-content">
          <div className="microflow-preview-data-panel">
            <header><Database className="h-3.5 w-3.5" />{translateCurrentLiteral("业务数据")}</header>
            <MicroflowPreviewDatasetView
              errorMessage={errorMessage}
              loading={runMutation.isPending}
              result={result}
              selectedDatasetKey={selectedDatasetKey}
              onSelectDataset={setSelectedDatasetKey}
            />
            <MicroflowPreviewDebugPanel result={result} />
          </div>
        </section>
      </div>
    </ResponsiveModal>
  );

}
