import type { ReactNode, SetStateAction } from 'react';

import type {
  HttpCallbackConfigDto,
  ScheduleConfigDto,
  ScheduledJobDetailDto,
  ScheduledJobListItemDto,
  ScheduledJobScheduleKind,
  ScheduledJobStatus,
  ScheduledJobType,
  ScheduledJobUpsertRequest
} from '../../../api/system/scheduled-jobs.api';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import type { DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';

export interface ScheduledJobSearchState {
  jobType: string;
  keyword: string;
  keywordDraft: string;
  result: string;
  status: string;
}

export interface ScheduledJobFormState {
  code: string;
  httpBodyJson: string;
  httpMethod: 'GET' | 'POST';
  httpUrl: string;
  intervalValue: number;
  jobType: ScheduledJobType;
  monthDays: number[];
  name: string;
  parameters: string;
  presetJobCode: string;
  remark: string;
  scheduleKind: ScheduledJobScheduleKind;
  status: ScheduledJobStatus;
  timeOfDay: string;
  timeZone: string;
  weekDays: number[];
}

export interface ScheduledJobsPageState {
  editingId: string | null;
  formOpen: boolean;
  formState: ScheduledJobFormState;
  logJobId: string | null;
  logPageIndex: number;
  logPageSize: number;
  logResult: string;
  logSorts: DataTableSortRule[];
  logTableQuery: DataTableQueryState;
  pageIndex: number;
  pageSize: number;
  searchState: ScheduledJobSearchState;
  sorts: DataTableSortRule[];
  tableQuery: DataTableQueryState;
}

type TranslateFn = (key: string) => string;

export const defaultSearchState: ScheduledJobSearchState = {
  jobType: '',
  keyword: '',
  keywordDraft: '',
  result: '',
  status: ''
};

export const defaultFormState: ScheduledJobFormState = {
  code: '',
  httpBodyJson: '',
  httpMethod: 'GET',
  httpUrl: 'http://localhost:5000/api/health',
  intervalValue: 30,
  jobType: 'Preset',
  monthDays: [1],
  name: '',
  parameters: '',
  presetJobCode: 'system.health-check',
  remark: '',
  scheduleKind: 'EveryMinutes',
  status: 'Enabled',
  timeOfDay: '09:00',
  timeZone: 'China Standard Time',
  weekDays: [1]
};

const createWeekDayOptions = (translate: TranslateFn) => [
  { label: translate('page.systemScheduledJobs.weekDay.sunday'), value: 0 },
  { label: translate('page.systemScheduledJobs.weekDay.monday'), value: 1 },
  { label: translate('page.systemScheduledJobs.weekDay.tuesday'), value: 2 },
  { label: translate('page.systemScheduledJobs.weekDay.wednesday'), value: 3 },
  { label: translate('page.systemScheduledJobs.weekDay.thursday'), value: 4 },
  { label: translate('page.systemScheduledJobs.weekDay.friday'), value: 5 },
  { label: translate('page.systemScheduledJobs.weekDay.saturday'), value: 6 }
];

const monthDayOptions = Array.from({ length: 31 }, (_item, index) => index + 1);
const editorInputClass = 'w-full border border-gray-300 rounded bg-white px-3 py-2 text-sm text-gray-800 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-100';
export const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

export const createJobTypeFilterOptions = (translate: TranslateFn) => [
  { label: translate('page.systemScheduledJobs.jobType.preset'), value: 'Preset' },
  { label: translate('page.systemScheduledJobs.jobType.httpCallback'), value: 'HttpCallback' }
];

export const createJobStatusFilterOptions = (translate: TranslateFn) => [
  { label: translate('page.systemScheduledJobs.status.enabled'), value: 'Enabled' },
  { label: translate('page.systemScheduledJobs.status.paused'), value: 'Paused' }
];

export const createJobResultFilterOptions = (translate: TranslateFn) => [
  { label: translate('page.systemScheduledJobs.result.queued'), value: 'Queued' },
  { label: translate('page.systemScheduledJobs.result.success'), value: 'Success' },
  { label: translate('page.systemScheduledJobs.result.failed'), value: 'Failed' }
];

export const createTriggerTypeFilterOptions = (translate: TranslateFn) => [
  { label: translate('page.systemScheduledJobs.trigger.manual'), value: 'Manual' },
  { label: translate('page.systemScheduledJobs.trigger.automatic'), value: 'Automatic' }
];

export function ScheduledJobSearchBar({
  onChange,
  onReset,
  onSearch,
  translate,
  value
}: {
  onChange: (value: SetStateAction<ScheduledJobSearchState>) => void;
  onReset: () => void;
  onSearch: () => void;
  translate: TranslateFn;
  value: ScheduledJobSearchState;
}) {
  const updateField = (name: keyof ScheduledJobSearchState, fieldValue: string) => onChange((current) => ({ ...current, [name]: fieldValue }));

  return (
    <div className="flex flex-wrap gap-3 items-center">
      <input
        className="border border-gray-300 rounded bg-white px-3 py-1.5 text-sm w-56 focus:outline-none focus:border-primary-500"
        placeholder={translate('page.systemScheduledJobs.search.placeholder')}
        value={value.keywordDraft}
        onChange={(event) => updateField('keywordDraft', event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            onSearch();
          }
        }}
      />
      <select className="border border-gray-300 rounded bg-white px-2 py-1.5 text-sm" value={value.jobType} onChange={(event) => updateField('jobType', event.target.value)}>
        <option value="">{translate('page.systemScheduledJobs.search.allTypes')}</option>
        <option value="Preset">{translate('page.systemScheduledJobs.jobType.preset')}</option>
        <option value="HttpCallback">{translate('page.systemScheduledJobs.jobType.httpCallback')}</option>
      </select>
      <select className="border border-gray-300 rounded bg-white px-2 py-1.5 text-sm" value={value.status} onChange={(event) => updateField('status', event.target.value)}>
        <option value="">{translate('page.systemScheduledJobs.search.allStatus')}</option>
        <option value="Enabled">{translate('page.systemScheduledJobs.status.enabled')}</option>
        <option value="Paused">{translate('page.systemScheduledJobs.status.paused')}</option>
      </select>
      <select className="border border-gray-300 rounded bg-white px-2 py-1.5 text-sm" value={value.result} onChange={(event) => updateField('result', event.target.value)}>
        <option value="">{translate('page.systemScheduledJobs.search.allResults')}</option>
        <option value="Success">{translate('page.systemScheduledJobs.result.success')}</option>
        <option value="Failed">{translate('page.systemScheduledJobs.result.failed')}</option>
        <option value="Queued">{translate('page.systemScheduledJobs.result.queued')}</option>
      </select>
      <PermissionButton code="system:scheduled-job:query" className="bg-white border border-gray-300 text-gray-700 px-4 py-1.5 rounded text-sm hover:bg-primary-50 hover:text-primary-600 transition-colors shadow-sm" type="button" onClick={onSearch}>
        {translate('common.query')}
      </PermissionButton>
      <button className="text-gray-500 px-2 py-1.5 text-sm hover:text-gray-700 transition-colors" type="button" onClick={onReset}>
        {translate('common.reset')}
      </button>
    </div>
  );
}

export function SummaryStrip({ summary, translate }: { summary: { enabled: number; failed: number; paused: number; success: number; total: number } | null; translate: TranslateFn }) {
  const items = [
    { label: translate('page.systemScheduledJobs.summary.total'), value: summary?.total ?? 0 },
    { label: translate('page.systemScheduledJobs.summary.enabled'), value: summary?.enabled ?? 0 },
    { label: translate('page.systemScheduledJobs.summary.paused'), value: summary?.paused ?? 0 },
    { label: translate('page.systemScheduledJobs.summary.success'), value: summary?.success ?? 0 },
    { label: translate('page.systemScheduledJobs.summary.failed'), value: summary?.failed ?? 0 }
  ];

  return (
    <div className="grid grid-cols-2 md:grid-cols-5 gap-3 mb-3">
      {items.map((item) => (
        <div key={item.label} className="border border-gray-200 bg-white px-4 py-3 rounded shadow-sm">
          <div className="text-xs text-gray-500">{item.label}</div>
          <div className="text-xl font-semibold text-gray-900 mt-1">{item.value}</div>
        </div>
      ))}
    </div>
  );
}

export function ScheduledJobEditor({
  formState,
  isSaving,
  jobTypes,
  onChange,
  onClose,
  onSave,
  open,
  translate,
  title
}: {
  formState: ScheduledJobFormState;
  isSaving: boolean;
  jobTypes: { presetJobs: Array<{ code: string; description: string; name: string }> } | null;
  onChange: (value: SetStateAction<ScheduledJobFormState>) => void;
  onClose: () => void;
  onSave: () => void;
  open: boolean;
  translate: TranslateFn;
  title: string;
}) {
  const setField = <K extends keyof ScheduledJobFormState>(key: K, value: ScheduledJobFormState[K]) => onChange((current) => ({ ...current, [key]: value }));

  return (
    <ResponsiveModal
      footer={
        <div className="flex justify-end gap-2">
          <button className="border border-gray-300 text-gray-700 px-4 py-2 rounded text-sm hover:bg-gray-50" type="button" onClick={onClose}>
            {translate('common.cancel')}
          </button>
          <button className="bg-primary-600 text-white px-4 py-2 rounded text-sm hover:bg-primary-700 disabled:opacity-60" disabled={isSaving} type="button" onClick={onSave}>
            {translate('common.save')}
          </button>
        </div>
      }
      mode="drawer"
      open={open}
      title={title}
      description={translate('page.systemScheduledJobs.editor.description')}
      onClose={onClose}
    >
      <div className="space-y-5 text-sm">
        <EditorSection title={translate('page.systemScheduledJobs.section.basicInfo')}>
          <FormGrid>
            <Field label={translate('page.systemScheduledJobs.field.name')} required>
              <input className={editorInputClass} value={formState.name} onChange={(event) => setField('name', event.target.value)} />
            </Field>
            <Field label={translate('page.systemScheduledJobs.field.code')} required>
              <input className={`${editorInputClass} font-mono`} placeholder={translate('page.systemScheduledJobs.placeholder.code')} value={formState.code} onChange={(event) => setField('code', event.target.value)} />
            </Field>
            <Field label={translate('page.systemScheduledJobs.field.jobType')} required>
              <select className={editorInputClass} value={formState.jobType} onChange={(event) => setField('jobType', event.target.value as ScheduledJobType)}>
                <option value="Preset">{translate('page.systemScheduledJobs.jobType.preset')}</option>
                <option value="HttpCallback">{translate('page.systemScheduledJobs.jobType.httpCallback')}</option>
              </select>
            </Field>
            <Field label={translate('page.systemScheduledJobs.field.status')} required>
              <select className={editorInputClass} value={formState.status} onChange={(event) => setField('status', event.target.value as ScheduledJobStatus)}>
                <option value="Enabled">{translate('page.systemScheduledJobs.status.enabled')}</option>
                <option value="Paused">{translate('page.systemScheduledJobs.status.paused')}</option>
              </select>
            </Field>
          </FormGrid>
        </EditorSection>

        {formState.jobType === 'Preset' ? (
          <EditorSection title={translate('page.systemScheduledJobs.section.preset')}>
            <Field label={translate('page.systemScheduledJobs.field.presetJobCode')} required>
              <select className={editorInputClass} value={formState.presetJobCode} onChange={(event) => setField('presetJobCode', event.target.value)}>
                {(jobTypes?.presetJobs ?? []).map((item) => (
                  <option key={item.code} value={item.code}>
                    {item.name}
                  </option>
                ))}
              </select>
            </Field>
            <div className="text-xs text-gray-500 mt-2">
              {(jobTypes?.presetJobs ?? []).find((item) => item.code === formState.presetJobCode)?.description ?? translate('page.systemScheduledJobs.preset.defaultDescription')}
            </div>
          </EditorSection>
        ) : (
          <EditorSection title={translate('page.systemScheduledJobs.section.httpCallback')}>
            <FormGrid>
              <Field label={translate('page.systemScheduledJobs.field.httpMethod')} required>
                <select className={editorInputClass} value={formState.httpMethod} onChange={(event) => setField('httpMethod', event.target.value as 'GET' | 'POST')}>
                  <option value="GET">GET</option>
                  <option value="POST">POST</option>
                </select>
              </Field>
              <Field label={translate('page.systemScheduledJobs.field.httpUrl')} required>
                <input className={editorInputClass} placeholder={translate('page.systemScheduledJobs.placeholder.httpUrl')} value={formState.httpUrl} onChange={(event) => setField('httpUrl', event.target.value)} />
              </Field>
            </FormGrid>
            {formState.httpMethod === 'POST' ? (
              <Field label={translate('page.systemScheduledJobs.field.httpBodyJson')}>
                <textarea className={`${editorInputClass} min-h-[96px] font-mono text-xs`} placeholder={translate('page.systemScheduledJobs.placeholder.httpBodyJson')} value={formState.httpBodyJson} onChange={(event) => setField('httpBodyJson', event.target.value)} />
              </Field>
            ) : null}
          </EditorSection>
        )}

        <EditorSection title={translate('page.systemScheduledJobs.section.schedule')}>
          <FormGrid>
            <Field label={translate('page.systemScheduledJobs.field.scheduleKind')} required>
              <select className={editorInputClass} value={formState.scheduleKind} onChange={(event) => setField('scheduleKind', event.target.value as ScheduledJobScheduleKind)}>
                <option value="EveryMinutes">{translate('page.systemScheduledJobs.schedule.everyMinutes')}</option>
                <option value="EveryHours">{translate('page.systemScheduledJobs.schedule.everyHours')}</option>
                <option value="Daily">{translate('page.systemScheduledJobs.schedule.daily')}</option>
                <option value="Weekly">{translate('page.systemScheduledJobs.schedule.weekly')}</option>
                <option value="Monthly">{translate('page.systemScheduledJobs.schedule.monthly')}</option>
              </select>
            </Field>
            {formState.scheduleKind === 'EveryMinutes' || formState.scheduleKind === 'EveryHours' ? (
              <Field label={formState.scheduleKind === 'EveryMinutes' ? translate('page.systemScheduledJobs.field.intervalMinutes') : translate('page.systemScheduledJobs.field.intervalHours')} required>
                <input className={editorInputClass} min={1} max={formState.scheduleKind === 'EveryMinutes' ? 59 : 23} type="number" value={formState.intervalValue} onChange={(event) => setField('intervalValue', Number(event.target.value))} />
              </Field>
            ) : (
              <Field label={translate('page.systemScheduledJobs.field.timeOfDay')} required>
                <input className={editorInputClass} type="time" value={formState.timeOfDay} onChange={(event) => setField('timeOfDay', event.target.value)} />
              </Field>
            )}
          </FormGrid>
          {formState.scheduleKind === 'Weekly' ? (
            <OptionGroup
              label={translate('page.systemScheduledJobs.field.weekDays')}
              options={createWeekDayOptions(translate)}
              value={formState.weekDays}
              onChange={(nextValue) => setField('weekDays', nextValue)}
            />
          ) : null}
          {formState.scheduleKind === 'Monthly' ? (
            <OptionGroup
              label={translate('page.systemScheduledJobs.field.monthDays')}
              options={monthDayOptions.map((value) => ({ label: translate('page.systemScheduledJobs.daySuffix').replace('{day}', String(value)), value }))}
              value={formState.monthDays}
              onChange={(nextValue) => setField('monthDays', nextValue)}
            />
          ) : null}
          <div className="text-xs text-gray-500 mt-2">{translate('page.systemScheduledJobs.preview.currentSetting').replace('{schedule}', previewFriendlySchedule(formState, translate))}</div>
        </EditorSection>

        <EditorSection title={translate('page.systemScheduledJobs.section.params')}>
          <Field label={translate('page.systemScheduledJobs.field.parameters')}>
            <textarea className={`${editorInputClass} min-h-[92px] font-mono text-xs`} placeholder={translate('page.systemScheduledJobs.placeholder.parameters')} value={formState.parameters} onChange={(event) => setField('parameters', event.target.value)} />
          </Field>
          <Field label={translate('page.systemScheduledJobs.field.remark')}>
            <textarea className={`${editorInputClass} min-h-[72px]`} value={formState.remark} onChange={(event) => setField('remark', event.target.value)} />
          </Field>
        </EditorSection>
      </div>
    </ResponsiveModal>
  );
}

function EditorSection({ children, title }: { children: ReactNode; title: string }) {
  return (
    <section className="space-y-3">
      <h4 className="text-sm font-semibold text-gray-900">{title}</h4>
      {children}
    </section>
  );
}

function FormGrid({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-1 md:grid-cols-2 gap-3">{children}</div>;
}

function Field({ children, label, required = false }: { children: ReactNode; label: string; required?: boolean }) {
  return (
    <label className="block">
      <span className="block text-xs text-gray-600 mb-1">
        {label}
        {required ? <span className="text-red-500 ml-1">*</span> : null}
      </span>
      {children}
    </label>
  );
}

function OptionGroup({
  label,
  onChange,
  options,
  value
}: {
  label: string;
  onChange: (value: number[]) => void;
  options: Array<{ label: string; value: number }>;
  value: number[];
}) {
  const toggle = (target: number) => {
    onChange(value.includes(target) ? value.filter((item) => item !== target) : [...value, target].sort((left, right) => left - right));
  };

  return (
    <div>
      <div className="text-xs text-gray-600 mb-2">{label}</div>
      <div className="flex flex-wrap gap-2">
        {options.map((option) => (
          <button
            key={option.value}
            className={`border px-2.5 py-1 rounded text-xs transition-colors ${value.includes(option.value) ? 'border-primary-500 bg-primary-50 text-primary-700' : 'border-gray-300 text-gray-600 hover:bg-gray-50'}`}
            type="button"
            onClick={() => toggle(option.value)}
          >
            {option.label}
          </button>
        ))}
      </div>
    </div>
  );
}

export function JobNameCell({ row }: { row: ScheduledJobListItemDto }) {
  return (
    <div>
      <div className="font-medium text-gray-900">{row.name}</div>
      <div className="font-mono text-xs text-gray-500">{row.code}</div>
    </div>
  );
}

export function JobTypeBadge({ translate, type }: { translate: TranslateFn; type: ScheduledJobType }) {
  return (
    <span className="inline-flex rounded border border-gray-200 bg-gray-50 px-2 py-0.5 text-xs text-gray-700">
      {type === 'Preset' ? translate('page.systemScheduledJobs.jobType.preset') : translate('page.systemScheduledJobs.jobType.httpCallback')}
    </span>
  );
}

export function StatusBadge({ status, translate }: { status: ScheduledJobStatus; translate: TranslateFn }) {
  return (
    <span className={`inline-flex rounded px-2 py-0.5 text-xs font-medium ${status === 'Enabled' ? 'bg-emerald-50 text-emerald-700' : 'bg-gray-100 text-gray-600'}`}>
      {status === 'Enabled' ? translate('page.systemScheduledJobs.status.enabled') : translate('page.systemScheduledJobs.status.paused')}
    </span>
  );
}

export function ResultBadge({ result, translate }: { result?: string | null; translate: TranslateFn }) {
  if (!result) {
    return <span className="text-xs text-gray-400">-</span>;
  }

  const className = result === 'Success' ? 'bg-emerald-50 text-emerald-700' : result === 'Failed' ? 'bg-red-50 text-red-700' : 'bg-amber-50 text-amber-700';
  const text = result === 'Success' ? translate('page.systemScheduledJobs.result.success') : result === 'Failed' ? translate('page.systemScheduledJobs.result.failed') : translate('page.systemScheduledJobs.result.queued');
  return <span className={`inline-flex rounded px-2 py-0.5 text-xs font-medium ${className}`}>{text}</span>;
}

export function SyncBadge({ translate, value }: { translate: TranslateFn; value: string }) {
  const text = value === 'Synced' ? translate('page.systemScheduledJobs.sync.synced') : value === 'Paused' ? translate('page.systemScheduledJobs.sync.paused') : value === 'Failed' ? translate('page.systemScheduledJobs.sync.failed') : translate('page.systemScheduledJobs.sync.pending');
  const className = value === 'Failed' ? 'bg-red-50 text-red-700' : value === 'Synced' ? 'bg-blue-50 text-blue-700' : 'bg-gray-100 text-gray-600';
  return <span className={`inline-flex rounded px-2 py-0.5 text-xs font-medium ${className}`}>{text}</span>;
}

export function mapDetailToForm(detail: ScheduledJobDetailDto): ScheduledJobFormState {
  return {
    code: detail.code,
    httpBodyJson: detail.httpCallback?.bodyJson ?? '',
    httpMethod: detail.httpCallback?.method ?? 'GET',
    httpUrl: detail.httpCallback?.url ?? 'http://localhost:5000/api/health',
    intervalValue: detail.schedule.intervalValue ?? 30,
    jobType: detail.jobType,
    monthDays: detail.schedule.monthDays ?? [1],
    name: detail.name,
    parameters: detail.parameters ?? '',
    presetJobCode: detail.presetJobCode ?? 'system.health-check',
    remark: detail.remark ?? '',
    scheduleKind: detail.schedule.kind,
    status: detail.status,
    timeOfDay: detail.schedule.timeOfDay ?? '09:00',
    timeZone: detail.schedule.timeZone ?? 'China Standard Time',
    weekDays: detail.schedule.weekDays ?? [1]
  };
}

export function buildUpsertRequest(formState: ScheduledJobFormState): ScheduledJobUpsertRequest {
  const schedule: ScheduleConfigDto = {
    intervalValue: formState.scheduleKind === 'EveryMinutes' || formState.scheduleKind === 'EveryHours' ? Number(formState.intervalValue) : null,
    kind: formState.scheduleKind,
    monthDays: formState.scheduleKind === 'Monthly' ? formState.monthDays : null,
    timeOfDay: formState.scheduleKind === 'Daily' || formState.scheduleKind === 'Weekly' || formState.scheduleKind === 'Monthly' ? formState.timeOfDay : null,
    timeZone: formState.timeZone,
    weekDays: formState.scheduleKind === 'Weekly' ? formState.weekDays : null
  };

  const httpCallback: HttpCallbackConfigDto | null = formState.jobType === 'HttpCallback'
    ? {
        bodyJson: formState.httpMethod === 'POST' ? trimToNull(formState.httpBodyJson) : null,
        headers: null,
        method: formState.httpMethod,
        url: formState.httpUrl.trim()
      }
    : null;

  return {
    code: formState.code.trim(),
    httpCallback,
    jobType: formState.jobType,
    name: formState.name.trim(),
    parameters: trimToNull(formState.parameters),
    presetJobCode: formState.jobType === 'Preset' ? formState.presetJobCode : null,
    remark: trimToNull(formState.remark),
    schedule,
    status: formState.status
  };
}

export function validateRequest(request: ScheduledJobUpsertRequest, translate: TranslateFn): string | null {
  if (!request.name || !request.code) {
    return translate('page.systemScheduledJobs.validation.completeInfo');
  }

  if (request.jobType === 'Preset' && !request.presetJobCode) {
    return translate('page.systemScheduledJobs.validation.presetRequired');
  }

  if (request.jobType === 'HttpCallback' && !request.httpCallback?.url) {
    return translate('page.systemScheduledJobs.validation.httpUrlRequired');
  }

  if ((request.schedule.kind === 'Weekly' && (request.schedule.weekDays?.length ?? 0) === 0) ||
      (request.schedule.kind === 'Monthly' && (request.schedule.monthDays?.length ?? 0) === 0)) {
    return translate('page.systemScheduledJobs.validation.scheduleDateRequired');
  }

  return null;
}

function previewFriendlySchedule(formState: ScheduledJobFormState, translate: TranslateFn) {
  if (formState.scheduleKind === 'EveryMinutes') {
    return translate('page.systemScheduledJobs.preview.everyMinutes').replace('{value}', String(formState.intervalValue || 1));
  }

  if (formState.scheduleKind === 'EveryHours') {
    return translate('page.systemScheduledJobs.preview.everyHours').replace('{value}', String(formState.intervalValue || 1));
  }

  if (formState.scheduleKind === 'Daily') {
    return translate('page.systemScheduledJobs.preview.daily').replace('{time}', formState.timeOfDay);
  }

  if (formState.scheduleKind === 'Weekly') {
    const text = createWeekDayOptions(translate).filter((item) => formState.weekDays.includes(item.value)).map((item) => item.label).join('、') || translate('page.systemScheduledJobs.preview.notSelected');
    return translate('page.systemScheduledJobs.preview.weekly').replace('{days}', text).replace('{time}', formState.timeOfDay);
  }

  const text = formState.monthDays.join('、') || translate('page.systemScheduledJobs.preview.notSelected');
  return translate('page.systemScheduledJobs.preview.monthly').replace('{days}', text).replace('{time}', formState.timeOfDay);
}

export function formatDateTime(value?: string | null) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

function trimToNull(value: string) {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}
