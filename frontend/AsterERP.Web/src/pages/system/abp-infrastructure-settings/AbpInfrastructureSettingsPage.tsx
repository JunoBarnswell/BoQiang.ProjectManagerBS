import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';

import {
  abpInfrastructureSettingsApi,
  type InfrastructureSettingsUpdateRequest,
  type InfrastructureTestResultDto,
  type MessageSendLogDto,
  type MessageSendLogQuery
} from '../../../api/system/abp-infrastructure-settings.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../../shared/feedback/useMessage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import type { DataTableColumn, DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

import {
  ChannelBadge,
  MessageLogSearchBar,
  ResultBadge,
  RuntimeSummary,
  SettingsSection
} from './AbpInfrastructureSettingsSections';
import {
  defaultMessageLogSearch,
  defaultTableQuery,
  defaultTestForm,
  sections,
  type InfrastructureFormState,
  type MessageLogSearchState,
  type SectionKey,
  type TestFormState
} from './abpInfrastructureTypes';
import {
  buildUpdateRequest,
  formatDateTime,
  normalizeLogSearch,
  toFormState,
  trimToUndefined,
  validateFormState
} from './abpInfrastructureUtils';

export function AbpInfrastructureSettingsPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [activeSection, setActiveSection] = useState<SectionKey>('email');
  const [formState, setFormState] = useState<InfrastructureFormState | null>(null);
  const [testForm, setTestForm] = useState<TestFormState>(defaultTestForm);
  const [lastTestResult, setLastTestResult] = useState<InfrastructureTestResultDto | null>(null);
  const [logPageIndex, setLogPageIndex] = useState(1);
  const [logPageSize, setLogPageSize] = useState(20);
  const [logSearchDraft, setLogSearchDraft] = useState<MessageLogSearchState>(defaultMessageLogSearch);
  const [logSearch, setLogSearch] = useState<MessageLogSearchState>(defaultMessageLogSearch);
  const [logSorts, setLogSorts] = useState<DataTableSortRule[]>([]);
  const [logTableQuery, setLogTableQuery] = useState<DataTableQueryState>(defaultTableQuery);

  const settingsQuery = useApiQuery({
    queryFn: ({ signal }) => abpInfrastructureSettingsApi.get(signal),
    queryKey: queryKeys.abpInfrastructureSettings.detail()
  });

  useEffect(() => {
    const payload = settingsQuery.data?.data;
    if (!payload) {
      return;
    }

    setFormState(toFormState(payload));
    setTestForm((current) => ({
      ...current,
      objectStorageProvider: current.objectStorageProvider || payload.objectStorage.provider
    }));
  }, [settingsQuery.data]);

  const messageLogQueryParams = useMemo<MessageSendLogQuery>(
    () => ({
      channel: trimToUndefined(logSearch.channel),
      filters: logTableQuery.conditions,
      pageIndex: logPageIndex,
      pageSize: logPageSize,
      provider: trimToUndefined(logSearch.provider),
      result: trimToUndefined(logSearch.result),
      sorts: logSorts,
      traceId: trimToUndefined(logSearch.traceId)
    }),
    [logPageIndex, logPageSize, logSearch, logSorts, logTableQuery]
  );

  const messageLogsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => abpInfrastructureSettingsApi.messageLogs(messageLogQueryParams, signal),
    queryKey: queryKeys.abpInfrastructureSettings.messageLogs(messageLogQueryParams)
  });

  const saveMutation = useApiMutation({
    mutationFn: (request: InfrastructureSettingsUpdateRequest) => abpInfrastructureSettingsApi.update(request)
  });
  const emailTestMutation = useApiMutation({
    mutationFn: () => abpInfrastructureSettingsApi.testEmail({
      body: translate('page.abpInfrastructureSettings.validation.testEmailBody'),
      isBodyHtml: false,
      subject: translate('page.abpInfrastructureSettings.validation.testEmailSubject'),
      to: testForm.emailTo.trim()
    })
  });
  const smsTestMutation = useApiMutation({
    mutationFn: () => abpInfrastructureSettingsApi.testSms({
      phoneNumber: testForm.smsPhoneNumber.trim(),
      text: translate('page.abpInfrastructureSettings.validation.testSmsText')
    })
  });
  const storageTestMutation = useApiMutation({
    mutationFn: () => abpInfrastructureSettingsApi.testObjectStorage({
      provider: testForm.objectStorageProvider || formState?.objectStorageProvider || undefined
    })
  });

  const settings = settingsQuery.data?.data ?? null;
  const logRows = messageLogsQuery.data?.data.items ?? [];
  const logTotal = messageLogsQuery.data?.data.total ?? 0;

  const logColumns: DataTableColumn<MessageSendLogDto>[] = useMemo(
    () => [
      { key: 'createdTime', title: translate('page.abpInfrastructureSettings.field.time'), width: '170px', responsivePriority: 100, sortable: true, filterable: true, filterType: 'date', render: (row) => formatDateTime(row.createdTime) },
      { key: 'channel', title: translate('page.abpInfrastructureSettings.field.channel'), width: '90px', align: 'center', responsivePriority: 96, sortable: true, filterable: true, filterType: 'text', render: (row) => <ChannelBadge value={row.channel} /> },
      { key: 'provider', title: translate('page.abpInfrastructureSettings.field.provider'), width: '110px', responsivePriority: 94, sortable: true, filterable: true, filterType: 'text' },
      { key: 'maskedTarget', title: translate('page.abpInfrastructureSettings.field.target'), width: '150px', responsivePriority: 92, sortable: false, filterable: true, filterType: 'text', render: (row) => row.maskedTarget || '-' },
      { key: 'result', title: translate('page.abpInfrastructureSettings.field.result'), width: '96px', align: 'center', responsivePriority: 96, sortable: true, filterable: true, filterType: 'text', render: (row) => <ResultBadge success={row.result.toLowerCase().includes('success') || row.result.toLowerCase().includes('succeed')} value={row.result} /> },
      { key: 'durationMs', title: translate('page.abpInfrastructureSettings.field.duration'), width: '90px', align: 'right', responsivePriority: 80, sortable: true, filterable: true, filterType: 'number', render: (row) => `${row.durationMs} ms` },
      { key: 'traceId', title: translate('page.abpInfrastructureSettings.field.traceId'), width: '190px', responsivePriority: 75, sortable: true, filterable: true, filterType: 'text', render: (row) => <span className="font-mono text-xs">{row.traceId}</span> },
      { key: 'errorSummary', title: translate('page.abpInfrastructureSettings.field.errorSummary'), responsivePriority: 70, sortable: false, render: (row) => row.errorSummary || '-' }
    ],
    [translate]
  );

  const setFormField = <K extends keyof InfrastructureFormState>(key: K, value: InfrastructureFormState[K]) => {
    setFormState((current) => (current ? { ...current, [key]: value } : current));
  };

  const refreshAll = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.abpInfrastructureSettings.root() }),
      messageLogsQuery.refetch()
    ]);
  };

  const handleSave = async () => {
    if (!formState) {
      return;
    }

    const validation = validateFormState(formState, translate);
    if (validation) {
      message.error(validation);
      return;
    }

    try {
      const response = await saveMutation.mutateAsync(buildUpdateRequest(formState));
      setFormState(toFormState(response.data));
      setTestForm((current) => ({ ...current, objectStorageProvider: current.objectStorageProvider || response.data.objectStorage.provider }));
      await refreshAll();
      message.success(translate('page.abpInfrastructureSettings.saveSettings'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.abpInfrastructureSettings.validation.saveFailed')));
    }
  };

  const runEmailTest = async () => {
    if (!testForm.emailTo.trim()) {
      message.error(translate('page.abpInfrastructureSettings.validation.emailRecipientRequired'));
      return;
    }

    await runTest(emailTestMutation.mutateAsync, translate('page.abpInfrastructureSettings.test.emailSuccess'));
  };

  const runSmsTest = async () => {
    if (!testForm.smsPhoneNumber.trim()) {
      message.error(translate('page.abpInfrastructureSettings.validation.smsRecipientRequired'));
      return;
    }

    await runTest(smsTestMutation.mutateAsync, translate('page.abpInfrastructureSettings.test.smsSuccess'));
  };

  const runObjectStorageTest = async () => {
    await runTest(storageTestMutation.mutateAsync, translate('page.abpInfrastructureSettings.test.objectStorageSuccess'));
  };

  const runTest = async (action: () => Promise<{ data: InfrastructureTestResultDto }>, successMessage: string) => {
    try {
      const response = await action();
      setLastTestResult(response.data);
      await messageLogsQuery.refetch();
      if (response.data.success) {
        message.success(successMessage);
      } else {
        message.error(response.data.message);
      }
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.abpInfrastructureSettings.test.failed')));
    }
  };

  const handleLogSearch = () => {
    setLogPageIndex(1);
    setLogSearch(normalizeLogSearch(logSearchDraft));
  };

  const handleLogReset = () => {
    setLogPageIndex(1);
    setLogTableQuery(defaultTableQuery);
    setLogSearchDraft(defaultMessageLogSearch);
    setLogSearch(defaultMessageLogSearch);
  };

  const actionNode = (
    <div className="flex flex-wrap items-center gap-2">
      <button
        className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors"
        type="button"
        onClick={() => void refreshAll()}
      >
        <AppIcon name="arrows-clockwise" /> {translate('page.abpInfrastructureSettings.refresh')}
      </button>
      <PermissionButton
        code="system:abp-setting:edit"
        className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors disabled:opacity-60"
        disabled={saveMutation.isPending || !formState}
        type="button"
        onClick={() => void handleSave()}
      >
        <AppIcon name="floppy-disk" /> {translate('page.abpInfrastructureSettings.saveSettings')}
      </PermissionButton>
    </div>
  );

  return (
    <CrudPage
      actions={actionNode}
      description={translate('page.abpInfrastructureSettings.description')}
      eyebrow={translate('page.abpInfrastructureSettings.eyebrow')}
      title={translate('page.abpInfrastructureSettings.title')}
    >
      <div className="flex-1 flex flex-col gap-3 min-h-0 overflow-hidden">
        <div className="bg-white border border-gray-200 rounded-lg shadow-sm overflow-hidden">
          <div className="border-b border-gray-200 px-4 py-3 flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-2">
              {sections.map((section) => (
                <button
                  key={section.key}
                  className={`inline-flex items-center gap-1.5 rounded border px-3 py-1.5 text-sm transition-colors ${
                    activeSection === section.key
                      ? 'border-primary-500 bg-primary-50 text-primary-700'
                      : 'border-gray-200 bg-white text-gray-600 hover:border-gray-300 hover:bg-gray-50'
                  }`}
                  type="button"
                  onClick={() => setActiveSection(section.key)}
                >
                  <i className={section.icon}></i>
                  {translate(section.labelKey)}
                </button>
              ))}
            </div>
            {settings ? <RuntimeSummary settings={settings} /> : null}
          </div>

          <div className="p-4">
            {settingsQuery.isLoading || !formState || !settings ? (
              <div className="text-sm text-gray-500">{translate('page.abpInfrastructureSettings.loading')}</div>
            ) : (
              <SettingsSection
                activeSection={activeSection}
                formState={formState}
                lastTestResult={lastTestResult}
                settings={settings}
                testForm={testForm}
                testing={{
                  email: emailTestMutation.isPending,
                  objectStorage: storageTestMutation.isPending,
                  sms: smsTestMutation.isPending
                }}
                onChange={setFormField}
                onRunEmailTest={() => void runEmailTest()}
                onRunObjectStorageTest={() => void runObjectStorageTest()}
                onRunSmsTest={() => void runSmsTest()}
                onTestFormChange={setTestForm}
              />
            )}
          </div>
        </div>

        <div className="flex-1 min-h-0 bg-white border border-gray-200 rounded-lg shadow-sm overflow-hidden">
          <div className="border-b border-gray-200 px-4 py-3 flex flex-wrap gap-3 items-center justify-between">
            <div>
              <h2 className="text-sm font-semibold text-gray-800">{translate('page.abpInfrastructureSettings.logsTitle')}</h2>
              <div className="text-xs text-gray-500 mt-0.5">{translate('page.abpInfrastructureSettings.logsDescription')}</div>
            </div>
            <MessageLogSearchBar
              value={logSearchDraft}
              onChange={setLogSearchDraft}
              onReset={handleLogReset}
              onSearch={handleLogSearch}
            />
          </div>
          <DataTable
            columnSettingsKey="system-abp-message-send-logs"
            columns={logColumns}
            emptyText={messageLogsQuery.isError ? translate('page.abpInfrastructureSettings.logsLoadFailed') : translate('page.abpInfrastructureSettings.noLogs')}
            fitScreen
            loading={messageLogsQuery.isLoading}
            onPageChange={setLogPageIndex}
            onPageSizeChange={(value) => {
              setLogPageIndex(1);
              setLogPageSize(value);
            }}
            onQueryChange={(nextQuery) => {
              setLogPageIndex(1);
              setLogTableQuery(nextQuery);
            }}
            onSortsChange={(nextSorts) => {
              setLogPageIndex(1);
              setLogSorts(nextSorts);
            }}
            pageSizeOptions={[10, 20, 50, 100]}
            pagination={{ current: logPageIndex, pageSize: logPageSize, total: logTotal }}
            rowKey={(row) => row.id}
            rows={logRows}
            sorts={logSorts}
            tableQuery={logTableQuery}
          />
        </div>
      </div>
    </CrudPage>
  );
}
