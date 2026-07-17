import { useEffect, useId, useRef, useState } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';

import type {
  ApplicationConnectionDiagnosticStage,
  ApplicationDataCenterObjectUpsertRequest,
  ApplicationDataCenterTemplate,
  ApplicationDataCenterTypeOption
} from '../../../api/application-data-center/applicationDataCenter.types';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { translateCurrentLiteral, translateCurrentLocale } from '../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { AppIcon } from '../../../shared/icons/AppIcon';

import { getConfigFormSchema } from './config-forms/configFormRegistry';
import { ConfigFormRenderer } from './config-forms/ConfigFormRenderer';
import { ConfigJsonAdvancedPanel } from './config-forms/ConfigJsonAdvancedPanel';
import { buildDefaultConfigJson } from './config-forms/configJsonCodec';
import { ConfigObjectSelect } from './config-forms/ConfigObjectSelect';
import { ConfigTypePicker } from './config-forms/ConfigTypePicker';
import type { DataCenterModuleConfig } from './dataCenterModuleConfig';
import { DataSourceWizardStepper } from './DataSourceWizardStepper';
import type { DataSourceWizardStep } from './DataSourceWizardStepper';
import type { DataSourceWorkspaceContext } from './dataSourceWorkspaceTypes';

interface DataCenterWizardDrawerProps {
  config: DataCenterModuleConfig;
  dataSourceContext?: DataSourceWorkspaceContext | null;
  editingId?: string | null;
  form: ApplicationDataCenterObjectUpsertRequest;
  loading?: boolean;
  open: boolean;
  publicConfigJson?: string | null;
  templates: ApplicationDataCenterTemplate[];
  typeOptions: ApplicationDataCenterTypeOption[];
  onChange: (next: ApplicationDataCenterObjectUpsertRequest) => void;
  onClose: () => void;
  onDiagnose?: () => void;
  onSubmit: () => void;
  diagnosticStages?: ApplicationConnectionDiagnosticStage[];
  diagnosticSuccess?: boolean;
  diagnosing?: boolean;
}

export function DataCenterWizardDrawer({
  config,
  dataSourceContext,
  editingId,
  form,
  loading,
  open,
  publicConfigJson,
  templates,
  typeOptions,
  onChange,
  onClose,
  onSubmit,
  onDiagnose,
  diagnosticStages = [],
  diagnosticSuccess,
  diagnosing
}: DataCenterWizardDrawerProps) {
  const dialogRef = useRef<HTMLElement>(null);
  const wasOpenRef = useRef(false);
  const previouslyFocusedElementRef = useRef<HTMLElement | null>(null);
  const idPrefix = useId().replace(/:/g, '');
  const titleId = `${idPrefix}-title`;
  const descriptionId = `${idPrefix}-description`;
  const [activeStep, setActiveStep] = useState<DataSourceWizardStep>('type');

  useEffect(() => {
    if (open && !wasOpenRef.current) {
      previouslyFocusedElementRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      wasOpenRef.current = true;
      setActiveStep('type');
      dialogRef.current?.focus();
      return;
    }

    if (!open && wasOpenRef.current) {
      wasOpenRef.current = false;
      const previouslyFocusedElement = previouslyFocusedElementRef.current;
      previouslyFocusedElementRef.current = null;
      if (previouslyFocusedElement && document.contains(previouslyFocusedElement)) {
        previouslyFocusedElement.focus();
      }
    }
  }, [open]);

  if (!open) {
    return null;
  }

  const updateField = <TKey extends keyof ApplicationDataCenterObjectUpsertRequest>(
    key: TKey,
    value: ApplicationDataCenterObjectUpsertRequest[TKey]
  ) => onChange({ ...form, [key]: value });

  const schema = getConfigFormSchema(config.moduleKey, form.objectType);
  const isDataSource = config.moduleKey === 'data-source';
  const diagnosisReady = !isDataSource || diagnosticSuccess === true;
  const jsonDiffersFromPublic = publicConfigJson != null && publicConfigJson !== form.configJson;

  const exportConfigJson = () => {
    const blob = new Blob([form.configJson], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${form.objectCode || 'data-source'}-config.json`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const importConfigJson = async (file: File | undefined) => {
    if (!file) return;
    const imported = await file.text();
    try {
      JSON.parse(imported);
      onChange({ ...form, configJson: imported });
    } catch {
      // ConfigFormRenderer will surface the JSON validation error without writing invalid data.
    }
  };

  const applyTemplate = (templateCode: string) => {
    const template = templates.find((item) => item.templateCode === templateCode);
    if (!template) {
      return;
    }

    onChange({
      ...form,
      configJson: template.configJson,
      objectType: template.objectType
    });
  };

  const updateType = (objectType: string) => {
    const nextSchema = getConfigFormSchema(config.moduleKey, objectType);
    onChange({
      ...form,
      configJson: buildDefaultConfigJson(nextSchema),
      objectType,
      secretConfigJson: editingId ? null : ''
    });
  };

  const handleDialogKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }

    if (event.key !== 'Tab' || !dialogRef.current) {
      return;
    }

    const focusableElements = Array.from(
      dialogRef.current.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
      )
    ).filter((element) => !element.hasAttribute('hidden') && element.getAttribute('aria-hidden') !== 'true');

    if (!focusableElements.length) {
      event.preventDefault();
      dialogRef.current.focus();
      return;
    }

    const firstElement = focusableElements[0];
    const lastElement = focusableElements[focusableElements.length - 1];
    const activeElement = document.activeElement;
    if (event.shiftKey && (activeElement === firstElement || activeElement === dialogRef.current)) {
      event.preventDefault();
      lastElement.focus();
    } else if (!event.shiftKey && (activeElement === lastElement || activeElement === dialogRef.current)) {
      event.preventDefault();
      firstElement.focus();
    }
  };

  const durationText = (durationMs: number) =>
    durationMs < 1000
      ? formatMessage(translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.duration.milliseconds'), { duration: durationMs })
      : formatMessage(translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.duration.seconds'), { duration: (durationMs / 1000).toFixed(1) });

  const diagnosticStageText = (stage: ApplicationConnectionDiagnosticStage) =>
    formatMessage(translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.stage'), {
      code: stage.code,
      duration: durationText(stage.durationMs),
      message: stage.message,
      status: stage.status
    });

  const title = translateCurrentLocale(editingId ? 'applicationConsole.dataCenter.wizard.editTitle' : 'applicationConsole.dataCenter.wizard.createTitle');
  const diagnosticTitle = translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.title');

  return (
    <>
      <div aria-hidden="true" className="fixed inset-0 z-40 bg-slate-900/35" role="presentation" onClick={onClose} />
      <section
        ref={dialogRef}
        aria-describedby={descriptionId}
        aria-labelledby={titleId}
        aria-modal="true"
        className="fixed right-0 top-0 z-50 flex h-full w-full max-w-[560px] flex-col bg-white shadow-2xl"
        role="dialog"
        tabIndex={-1}
        onKeyDown={handleDialogKeyDown}
      >
        <header className="flex h-14 shrink-0 items-center justify-between border-b border-slate-200 bg-slate-50 px-5">
          <div className="min-w-0">
            <h2 className="truncate text-base font-semibold text-slate-900" id={titleId}>{title}</h2>
            <p className="truncate text-xs text-slate-500" id={descriptionId}>{translateCurrentLocale('applicationConsole.dataCenter.wizard.description')}</p>
          </div>
          <button
            aria-label={translateCurrentLocale('applicationConsole.dataCenter.wizard.close')}
            className="rounded p-1.5 text-slate-500 hover:bg-slate-200 hover:text-slate-700"
            type="button"
            onClick={onClose}
          >
            <AppIcon className="h-4 w-4" name="x" />
          </button>
        </header>

        <div className="flex-1 overflow-y-auto p-5">
          <div className="space-y-5">
            {isDataSource ? <DataSourceWizardStepper activeStep={activeStep} diagnosisReady={diagnosisReady} onChange={setActiveStep} /> : null}
            {(!isDataSource || activeStep === 'type') ? <>
            <ConfigTypePicker activeType={form.objectType} options={typeOptions} onChange={updateType} />

            <section className="grid grid-cols-2 gap-3">
              <TextField label="对象编码" required value={form.objectCode} onChange={(value) => updateField('objectCode', value)} />
              <TextField label="对象名称" required value={form.objectName} onChange={(value) => updateField('objectName', value)} />
              <TextField label="环境" value={form.environment ?? ''} onChange={(value) => updateField('environment', value)} />
              <TextField label="端点/路径" value={form.endpoint ?? ''} onChange={(value) => updateField('endpoint', value)} />
              <TopFieldShell label="负责人">
                <ConfigObjectSelect
                  configValues={{}}
                  dataSourceContext={dataSourceContext}
                  field={{ component: 'userSelect', label: translateCurrentLiteral("负责人"), name: 'ownerUserId' }}
                  value={form.ownerUserId ?? ''}
                  onChange={(value) => updateField('ownerUserId', value)}
                />
              </TopFieldShell>
              <TopFieldShell label="风险确认字段">
                <ConfigObjectSelect
                  configValues={{}}
                  dataSourceContext={dataSourceContext}
                  field={{ component: 'riskFieldSelect', label: translateCurrentLiteral("风险确认字段"), name: 'confirmedRiskFields' }}
                  value={(form.confirmedRiskFields ?? []).join(',')}
                  onChange={(value) => updateField('confirmedRiskFields', splitCsv(value))}
                />
              </TopFieldShell>
            </section>

            <section className="rounded-md border border-slate-200 bg-white p-3">
              <div className="mb-2 flex items-center justify-between gap-2">
                <label className="text-sm font-medium text-slate-700" htmlFor="application-data-center-template">{translateCurrentLiteral("配置模板")}</label>
                <select
                  id="application-data-center-template"
                  className="h-8 max-w-[260px] rounded border border-slate-300 bg-white px-2 text-xs text-slate-700"
                  defaultValue=""
                  onChange={(event) => applyTemplate(event.target.value)}
                >
                  <option value="">{translateCurrentLiteral("选择模板填充配置")}</option>
                  {templates.map((template) => (
                    <option key={template.templateCode} value={template.templateCode}>
                      {template.templateName}
                    </option>
                  ))}
                </select>
              </div>
              <div className="text-xs leading-5 text-slate-500">{translateCurrentLiteral("模板会填充当前类型的配置值，保存时仍写入现有后端配置字段。")}</div>
            </section>

            </> : null}

            {(!isDataSource || activeStep === 'connection') ? <>
            <ConfigFormRenderer
              dataSourceContext={dataSourceContext}
              form={form}
              mode={editingId ? 'edit' : 'create'}
              publicConfigJson={publicConfigJson}
              schema={schema}
              onChange={onChange}
            />

            <ConfigJsonAdvancedPanel label="高级 JSON（仅查看）" readOnly value={form.configJson} />
            <div className="flex items-center gap-2 text-xs text-slate-500">
              <button className="secondary-button h-7 text-xs" type="button" onClick={exportConfigJson}>导出 JSON</button>
              <label className="secondary-button h-7 cursor-pointer text-xs">
                导入 JSON
                <input className="hidden" accept="application/json" type="file" onChange={(event) => void importConfigJson(event.target.files?.[0])} />
              </label>
              <span>{jsonDiffersFromPublic ? '与当前公开配置存在差异' : '配置差异为空'}</span>
            </div>

            </> : null}

            {config.moduleKey === 'data-source' && activeStep === 'diagnosis' && onDiagnose ? (
              <section className="rounded-md border border-slate-200 bg-slate-50 p-3">
                <div className="flex items-center justify-between gap-2">
                  <div>
                    <h3 className="text-sm font-semibold text-slate-900">{diagnosticTitle}</h3>
                    <p className="text-xs text-slate-500">{translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.description')}</p>
                  </div>
                  <button
                    aria-busy={diagnosing || undefined}
                    aria-label={diagnosing ? translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.running') : translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.run')}
                    className="secondary-button h-8 text-xs"
                    disabled={diagnosing}
                    type="button"
                    onClick={onDiagnose}
                  >
                    {diagnosing ? translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.running') : translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.run')}
                  </button>
                </div>
                {diagnosticSuccess !== undefined ? (
                  <div aria-live="polite" aria-atomic="true" className={diagnosticSuccess ? 'mt-2 text-xs text-emerald-600' : 'mt-2 text-xs text-rose-600'} role="status">
                    {diagnosticSuccess ? translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.passed') : translateCurrentLocale('applicationConsole.dataCenter.wizard.diagnostic.failed')}
                  </div>
                ) : null}
                {diagnosticStages.length ? (
                  <div aria-live="polite" aria-label={diagnosticTitle} className="mt-3 space-y-2">
                    {diagnosticStages.map((stage) => (
                      <div aria-label={diagnosticStageText(stage)} className="rounded border border-slate-200 bg-white px-3 py-2" key={stage.code} role="group">
                        <div className="flex items-center justify-between text-xs">
                          <span className="font-medium text-slate-800">{stage.code}</span>
                          <span aria-hidden="true" className={stage.status === 'Passed' || stage.status === 'NotApplicable' ? 'text-emerald-600' : stage.status === 'Blocked' ? 'text-amber-600' : 'text-rose-600'}>
                            {stage.status} · {durationText(stage.durationMs)}
                          </span>
                        </div>
                        <div className="mt-1 text-xs leading-5 text-slate-600">{stage.message}</div>
                        {stage.repairSuggestion ? <div className="mt-1 text-xs text-amber-700">{formatMessage(translateCurrentLocale('applicationConsole.dataCenter.diagnostic.recommendation'), { suggestion: stage.repairSuggestion })}</div> : null}
                      </div>
                    ))}
                  </div>
                ) : null}
              </section>
            ) : null}

            {(!isDataSource || activeStep === 'confirm') ? <section>
              <label className="mb-1 block text-sm font-medium text-slate-700" htmlFor="application-data-center-remark">{translateCurrentLiteral("备注")}</label>
              <textarea
                id="application-data-center-remark"
                className="min-h-[72px] w-full rounded border border-slate-300 px-3 py-2 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
                value={form.remark ?? ''}
                onChange={(event) => updateField('remark', event.target.value)}
              />
            </section> : null}
          </div>
        </div>

        <footer className="flex h-16 shrink-0 items-center justify-end gap-2 border-t border-slate-200 bg-white px-5">
          <button className="ghost-button" type="button" onClick={onClose}>{translateCurrentLiteral("取消")}</button>
          <PermissionButton
            className="primary-button"
            code={editingId ? config.permissions.edit : config.permissions.add}
            disabled={loading || (isDataSource && !diagnosisReady)}
            type="button"
            onClick={onSubmit}
          >
            {loading ? '保存中...' : '保存'}
          </PermissionButton>
        </footer>
      </section>
    </>
  );
}

function TopFieldShell({ children, label }: { children: ReactNode; label: string }) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium text-slate-700">{label}</span>
      {children}
    </label>
  );
}

function TextField({
  label,
  required,
  value,
  onChange
}: {
  label: string;
  required?: boolean;
  value: string;
  onChange: (value: string) => void;
}) {
  const id = `application-data-center-${label}`;
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium text-slate-700">
        {label}
        {required ? <span className="text-red-500"> *</span> : null}
      </span>
      <input
        id={id}
        className="h-9 w-full rounded border border-slate-300 px-3 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function splitCsv(value: string): string[] {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}
