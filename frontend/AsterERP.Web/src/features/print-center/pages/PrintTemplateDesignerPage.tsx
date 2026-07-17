import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';

import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../../shared/feedback/useMessage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { printCenterApi } from '../api/printCenter.api';
import { PrintAssetPicker } from '../components/PrintAssetPicker';
import { PrintDesignerHost, type PrintDesignerHostRef } from '../components/PrintDesignerHost';
import type { PrintScene, PrintTargetOptionDto, PrintTemplateUpsertRequest, PrintVariableNodeDto } from '../types';
import { restoreAssetTokens, resolveAssetTokensInData } from '../utils/assetTokenResolver';
import {
  createSystemFilePrintTemplateDefault,
  isEmptyDesignerTemplateData
} from '../utils/systemFilePrintTemplateDefaults';

interface DesignerFormState {
  menuCode: string;
  name: string;
  remark: string;
  scene: PrintScene;
  templateCode: string;
}

const defaultFormState: DesignerFormState = {
  menuCode: '',
  name: '',
  remark: '',
  scene: 'list',
  templateCode: ''
};

export function PrintTemplateDesignerPage() {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const { templateId } = useParams();
  const [searchParams] = useSearchParams();
  const message = useMessage();
  const hostRef = useRef<PrintDesignerHostRef | null>(null);
  const assetReverseMapRef = useRef<Map<string, string>>(new Map());
  const assetCleanupRef = useRef<(() => void) | null>(null);
  const [form, setForm] = useState<DesignerFormState>({
    ...defaultFormState,
    menuCode: searchParams.get('menuCode') ?? '',
    scene: (searchParams.get('scene') as PrintScene | null) ?? 'list'
  });
  const [designerData, setDesignerData] = useState<Record<string, unknown> | null>(null);
  const [templateLoadStatus, setTemplateLoadStatus] = useState<{ attempts: number; loaded: boolean } | null>(null);

  const targetsQuery = useApiQuery({
    queryFn: ({ signal }) => printCenterApi.getTargets(signal),
    queryKey: ['print-center', 'targets']
  });

  const templateQuery = useApiQuery({
    enabled: Boolean(templateId),
    queryFn: ({ signal }) => printCenterApi.getTemplate(templateId!, signal),
    queryKey: ['print-center', 'template-detail', templateId]
  });

  const targetDetailQuery = useApiQuery({
    enabled: Boolean(form.menuCode),
    queryFn: ({ signal }) => printCenterApi.getTarget(form.menuCode, form.scene, signal),
    queryKey: ['print-center', 'target-detail', form.menuCode, form.scene]
  });

  const saveMutation = useApiMutation({
    mutationFn: (request: PrintTemplateUpsertRequest) => printCenterApi.saveTemplate(request)
  });
  const publishMutation = useApiMutation({
    mutationFn: (id: string) => printCenterApi.publishTemplate(id)
  });

  const targetOptions = useMemo(
    () => (targetsQuery.data?.data ?? []).map((item: PrintTargetOptionDto) => ({ label: item.menuName, value: item.menuCode })),
    [targetsQuery.data?.data]
  );

  const availableVariables = useMemo<PrintVariableNodeDto[]>(
    () => targetDetailQuery.data?.data.availableVariables ?? [],
    [targetDetailQuery.data?.data.availableVariables]
  );

  const testData = useMemo<Record<string, unknown>>(
    () => (targetDetailQuery.data?.data.testData as Record<string, unknown> | null) ?? {},
    [targetDetailQuery.data?.data.testData]
  );
  const systemFileDefaultTemplateData = useMemo<Record<string, unknown> | null>(() => {
    if (form.menuCode !== 'system:file') {
      return null;
    }

    return createSystemFilePrintTemplateDefault(form.scene);
  }, [form.menuCode, form.scene]);
  const effectiveDesignerData = useMemo<Record<string, unknown> | null>(() => {
    if (form.menuCode === 'system:file' && isEmptyDesignerTemplateData(designerData)) {
      return systemFileDefaultTemplateData;
    }

    return designerData;
  }, [designerData, form.menuCode, systemFileDefaultTemplateData]);
  const effectiveDesignerDataKey = useMemo(
    () => JSON.stringify(effectiveDesignerData?.pages ?? null),
    [effectiveDesignerData]
  );
  const effectiveElementCount = useMemo(() => {
    const pages = effectiveDesignerData?.pages;
    if (!Array.isArray(pages)) {
      return 0;
    }

    return pages.reduce((total, page) => {
      const elements = (page as { elements?: unknown }).elements;
      return total + (Array.isArray(elements) ? elements.length : 0);
    }, 0);
  }, [effectiveDesignerData?.pages]);

  useEffect(() => {
    if (!templateQuery.data?.data) {
      return;
    }

    const template = templateQuery.data.data;
    setForm({
      menuCode: template.menuCode,
      name: template.name,
      remark: template.remark ?? '',
      scene: template.scene,
      templateCode: template.templateCode
    });
  }, [templateQuery.data?.data]);

  useEffect(() => {
    const data = templateQuery.data?.data?.data as Record<string, unknown> | null | undefined;
    if (!data) {
      setDesignerData(systemFileDefaultTemplateData);
      return;
    }

    if (form.menuCode === 'system:file' && isEmptyDesignerTemplateData(data as Record<string, unknown>)) {
      setDesignerData(systemFileDefaultTemplateData);
      return;
    }

    let disposed = false;
    void resolveAssetTokensInData(data).then((result) => {
      if (disposed) {
        result.cleanup();
        return;
      }

      assetCleanupRef.current?.();
      assetCleanupRef.current = result.cleanup;
      assetReverseMapRef.current = result.reverseMap;
      setDesignerData(result.resolvedData as Record<string, unknown>);
    });

    return () => {
      disposed = true;
    };
  }, [form.menuCode, systemFileDefaultTemplateData, templateQuery.data?.data?.data]);

  useEffect(() => () => {
    assetCleanupRef.current?.();
  }, []);

  const currentTemplateId = templateQuery.data?.data.id ?? templateId ?? null;

  const handleSave = async () => {
    const templateData = hostRef.current?.getTemplateData();
    if (!templateData) {
      message.error(translate('print.designer.notReady'));
      return null;
    }

    try {
      const payload: PrintTemplateUpsertRequest = {
        data: restoreAssetTokens(templateData, assetReverseMapRef.current),
        ext: {
          availableVariables,
          supportsAssets: targetDetailQuery.data?.data.supportsAssets ?? false
        },
        id: currentTemplateId,
        menuCode: form.menuCode,
        name: form.name.trim(),
        permissions: templateQuery.data?.data.permissions ?? null,
        remark: form.remark.trim() || undefined,
        scene: form.scene,
        templateCode: form.templateCode.trim() || undefined,
        updatedAt: Date.now()
      };

      const response = await saveMutation.mutateAsync(payload);
      message.success(translate('print.designer.saveSuccess'));
      if (!currentTemplateId) {
        navigate(`/system/print-center/${response.data.id}/designer`, { replace: true });
      } else {
        await templateQuery.refetch();
      }

      return response.data.id;
    } catch (error) {
      message.error(getErrorMessage(error, translate('print.designer.saveFailed')));
      return null;
    }
  };

  const handlePublish = async () => {
    const savedId = await handleSave();
    const targetId = savedId ?? currentTemplateId;
    if (!targetId) {
      return;
    }

    try {
      await publishMutation.mutateAsync(targetId);
      message.success(translate('print.publishSuccess'));
      await templateQuery.refetch();
    } catch (error) {
      message.error(getErrorMessage(error, translate('print.publishFailed')));
    }
  };

  const actionNode = (
    <div className="flex items-center gap-2">
      <button
        className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
        type="button"
        onClick={() => navigate(searchParams.get('returnTo') || '/system/print-center')}
      >
        <AppIcon name="arrow-left" /> {translate('print.designer.back')}
      </button>
      <button
        className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
        type="button"
        onClick={() => void handleSave()}
      >
        <AppIcon name="floppy-disk" /> {translate('print.designer.saveDraft')}
      </button>
      <button
        className="flex items-center gap-1 rounded bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700"
        type="button"
        onClick={() => void handlePublish()}
      >
        <AppIcon name="paper-plane-tilt" /> {translate('print.designer.publish')}
      </button>
    </div>
  );

  return (
    <CrudPage
      actions={actionNode}
      description={translate('print.designer.description')}
      eyebrow={translate('print.eyebrow')}
      title={currentTemplateId ? translate('print.designer.titleEdit') : translate('print.designer.titleCreate')}
    >
      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-hidden">
        <div className="grid grid-cols-1 gap-4 rounded-lg border border-gray-200 bg-white p-4 shadow-sm xl:grid-cols-[2fr_1fr_1fr_2fr_2fr]">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{translate('print.designer.menuLabel')}</label>
            <select
              className="w-full rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-500"
              value={form.menuCode}
              onChange={(event) => setForm((current) => ({ ...current, menuCode: event.target.value }))}
            >
              <option value="">{translate('print.designer.menuPlaceholder')}</option>
              {targetOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{translate('print.designer.sceneLabel')}</label>
            <select
              className="w-full rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-500"
              value={form.scene}
              onChange={(event) => setForm((current) => ({ ...current, scene: event.target.value as PrintScene }))}
            >
              <option value="list">{translate('print.designer.sceneList')}</option>
              <option value="detail">{translate('print.designer.sceneDetail')}</option>
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{translate('print.designer.templateNameLabel')}</label>
            <input
              className="w-full rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-500"
              value={form.name}
              onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{translate('print.designer.templateCodeLabel')}</label>
            <input
              className="w-full rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-500"
              value={form.templateCode}
              onChange={(event) => setForm((current) => ({ ...current, templateCode: event.target.value }))}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{translate('print.designer.remarkLabel')}</label>
            <input
              className="w-full rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-500"
              value={form.remark}
              onChange={(event) => setForm((current) => ({ ...current, remark: event.target.value }))}
            />
          </div>
        </div>

        <div className="grid min-h-0 flex-1 gap-4 xl:grid-cols-[minmax(0,1fr)_320px]">
          <div className="min-h-0 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm">
            {form.menuCode === 'system:file' ? (
              <div className="border-b border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700">
                {formatMessage(translate('print.designer.systemFileBanner'), {
                  count: effectiveElementCount,
                  status: templateLoadStatus
                    ? (templateLoadStatus.loaded
                      ? formatMessage(translate('print.designer.systemFileLoadingSuccess'), { attempts: templateLoadStatus.attempts })
                      : formatMessage(translate('print.designer.systemFileLoadingFailed'), { attempts: templateLoadStatus.attempts }))
                    : translate('print.designer.systemFileLoadingPending')
                })}
              </div>
            ) : null}
            <PrintDesignerHost
              key={effectiveDesignerDataKey}
              ref={hostRef}
              availableVariables={availableVariables}
              crudContext={form.menuCode ? { menuCode: form.menuCode, scene: form.scene } : undefined}
              onTemplateLoadStatus={setTemplateLoadStatus}
              templateData={effectiveDesignerData}
              testData={testData}
              variables={testData}
            />
          </div>
          <div className="space-y-4 overflow-y-auto">
            <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
              <h3 className="text-sm font-semibold text-gray-900">{translate('print.designer.currentVariablesTitle')}</h3>
              <div className="mt-3 space-y-2 text-sm text-gray-600">
                {availableVariables.map((node) => (
                  <div key={node.id} className="rounded border border-gray-200 px-3 py-2">
                    <div className="font-medium text-gray-800">{node.label}</div>
                    <div className="mt-1 text-xs text-gray-500">{node.id}{node.isArray ? ' []' : ''}</div>
                  </div>
                ))}
                {availableVariables.length === 0 ? <div className="text-xs text-gray-500">{translate('print.designer.selectMenuToLoadVariables')}</div> : null}
              </div>
            </div>
            <PrintAssetPicker />
          </div>
        </div>
      </div>
    </CrudPage>
  );
}
