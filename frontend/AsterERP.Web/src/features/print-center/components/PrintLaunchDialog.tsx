import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { printCenterApi } from '../api/printCenter.api';
import type { PrintLaunchRequest, PrintMode } from '../types';
import { resolveAssetTokensInData } from '../utils/assetTokenResolver';
import { exportPrintRuntimePdf, previewPrintRuntime, printPrintRuntime } from '../utils/printRuntimeDesignerClient';

interface PrintLaunchDialogProps {
  onClose: () => void;
  open: boolean;
  request: PrintLaunchRequest | null;
}

export function PrintLaunchDialog({ onClose, open, request }: PrintLaunchDialogProps) {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const message = useMessage();
  const [mode, setMode] = useState<PrintMode>('currentPage');
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const [isRunning, setIsRunning] = useState(false);

  const templateOptionsQuery = useApiQuery({
    enabled: open && Boolean(request),
    queryFn: ({ signal }) => printCenterApi.getTemplateOptions(request!.menuCode, request!.scene, signal),
    queryKey: ['print-center', 'template-options', request?.menuCode, request?.scene]
  });

  const templateOptions = useMemo(() => templateOptionsQuery.data?.data ?? [], [templateOptionsQuery.data?.data]);
  const activeTemplate = useMemo(
    () => templateOptions.find((item) => item.id === selectedTemplateId) ?? templateOptions[0] ?? null,
    [selectedTemplateId, templateOptions]
  );

  useEffect(() => {
    if (!open) {
      return;
    }

    setMode(request?.scene === 'list' ? (request.selectedIds.length > 0 ? 'selected' : 'currentPage') : 'currentPage');
    setSelectedTemplateId('');
  }, [open, request]);

  useEffect(() => {
    if (templateOptions.length > 0 && !selectedTemplateId) {
      const defaultTemplate = templateOptions.find((item) => item.isDefault) ?? templateOptions[0];
      setSelectedTemplateId(defaultTemplate.id);
    }
  }, [selectedTemplateId, templateOptions]);

  if (!open || !request) {
    return null;
  }

  const runAction = async (action: 'export' | 'preview' | 'print') => {
    if (!activeTemplate) {
      message.error(translate('print.launch.noTemplate'));
      return;
    }

    setIsRunning(true);
    try {
      const runtimeResponse = await printCenterApi.resolveRuntime({
        conditions: request.conditions,
        detailId: request.detailId,
        menuCode: request.menuCode,
        mode: request.scene === 'list' ? mode : 'currentPage',
        pageIndex: request.pageIndex,
        pageSize: request.pageSize,
        scene: request.scene,
        selectedIds: request.selectedIds,
        sorts: request.sorts,
        templateId: activeTemplate.id
      });

      const assetResolution = await resolveAssetTokensInData(runtimeResponse.data.data);
      const payload = { ...runtimeResponse.data, data: assetResolution.resolvedData };
      try {
        if (action === 'preview') {
          await previewPrintRuntime(payload);
        } else if (action === 'print') {
          await printPrintRuntime(payload);
        } else {
          await exportPrintRuntimePdf(payload);
        }
      } finally {
        assetResolution.cleanup();
      }

      onClose();
    } catch (error) {
      message.error(getErrorMessage(
        error,
        action === 'export' ? translate('print.launch.error.exportFailed') : translate('print.launch.error.printFailed')
      ));
    } finally {
      setIsRunning(false);
    }
  };

  return (
    <>
      <div className="fixed inset-0 z-40 bg-gray-900/40" onClick={onClose} />
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
        <div className="w-full max-w-lg rounded-xl bg-white shadow-2xl">
          <div className="border-b border-gray-200 px-5 py-4">
            <h3 className="text-base font-semibold text-gray-900">{translate('print.launch.title')}</h3>
            <p className="mt-1 text-sm text-gray-500">
              {formatMessage(translate('print.launch.target'), {
                menuCode: request.menuCode,
                scene: request.scene === 'list' ? translate('print.launch.scene.list') : translate('print.launch.scene.detail')
              })}
            </p>
          </div>

          <div className="space-y-4 px-5 py-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{translate('print.launch.templateLabel')}</label>
              <select
                className="w-full rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-500"
                value={selectedTemplateId}
                onChange={(event) => setSelectedTemplateId(event.target.value)}
              >
                {templateOptions.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}{item.isDefault ? translate('print.launch.defaultSuffix') : ''}
                  </option>
                ))}
              </select>
              {!templateOptionsQuery.isLoading && templateOptions.length === 0 ? (
                <div className="mt-2 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-700">
                  {translate('print.launch.noTemplateShort')}
                  <button
                    className="ml-2 font-medium text-primary-600 hover:text-primary-700"
                    type="button"
                    onClick={() => navigate(`/system/print-center/new?menuCode=${encodeURIComponent(request.menuCode)}&scene=${encodeURIComponent(request.scene)}`)}
                  >
                    {translate('print.launch.configureTemplate')}
                  </button>
                </div>
              ) : null}
            </div>

            {request.scene === 'list' ? (
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">{translate('print.launch.listRangeLabel')}</label>
                <select
                  className="w-full rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-500"
                  value={mode}
                  onChange={(event) => setMode(event.target.value as PrintMode)}
                >
                  <option value="currentPage">{translate('print.launch.range.currentPage')}</option>
                  <option value="selected" disabled={request.selectedIds.length === 0}>{translate('print.launch.range.selected')}</option>
                  <option value="allFiltered">{translate('print.launch.range.allFiltered')}</option>
                </select>
              </div>
            ) : null}
          </div>

          <div className="flex items-center justify-between gap-2 border-t border-gray-200 px-5 py-4">
            <button className="rounded border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50" type="button" onClick={onClose}>
              {translate('print.launch.cancel')}
            </button>
            <div className="flex items-center gap-2">
              <button
                className="rounded border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                disabled={isRunning || !activeTemplate}
                type="button"
                onClick={() => void runAction('preview')}
              >
                {translate('print.launch.preview')}
              </button>
              <button
                className="rounded border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                disabled={isRunning || !activeTemplate}
                type="button"
                onClick={() => void runAction('export')}
              >
                {translate('print.launch.exportPdf')}
              </button>
              <button
                className="rounded bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
                disabled={isRunning || !activeTemplate}
                type="button"
                onClick={() => void runAction('print')}
              >
                {isRunning ? translate('print.launch.processing') : translate('print.launch.print')}
              </button>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
