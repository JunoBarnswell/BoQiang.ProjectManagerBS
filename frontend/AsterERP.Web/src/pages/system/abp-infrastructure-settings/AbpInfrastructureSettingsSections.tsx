import type { ReactNode } from 'react';
import { useMemo } from 'react';

import type { InfrastructureSettingsDto, InfrastructureTestResultDto } from '../../../api/system/abp-infrastructure-settings.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { AppIcon } from '../../../shared/icons/AppIcon';

import {
  cacheProviders,
  objectStorageProviders,
  smsProviders,
  type InfrastructureFormState,
  type MessageLogSearchState,
  type SectionKey,
  type TestFormState
} from './abpInfrastructureTypes';

export function SettingsSection({
  activeSection,
  formState,
  lastTestResult,
  onChange,
  onRunEmailTest,
  onRunObjectStorageTest,
  onRunSmsTest,
  onTestFormChange,
  settings,
  testForm,
  testing
}: {
  activeSection: SectionKey;
  formState: InfrastructureFormState;
  lastTestResult: InfrastructureTestResultDto | null;
  onChange: <K extends keyof InfrastructureFormState>(key: K, value: InfrastructureFormState[K]) => void;
  onRunEmailTest: () => void;
  onRunObjectStorageTest: () => void;
  onRunSmsTest: () => void;
  onTestFormChange: (value: TestFormState | ((current: TestFormState) => TestFormState)) => void;
  settings: InfrastructureSettingsDto;
  testForm: TestFormState;
  testing: { email: boolean; objectStorage: boolean; sms: boolean };
}) {
  const { translate } = useI18n();
  const smsProviderOptions = useMemo(() => smsProviders.map((provider) => ({ label: translate(provider.labelKey), value: provider.value })), [translate]);
  const objectStorageProviderOptions = useMemo(
    () => objectStorageProviders.map((provider) => ({ label: translate(provider.labelKey), value: provider.value })),
    [translate]
  );
  const cacheProviderOptions = useMemo(() => cacheProviders.map((provider) => ({ label: translate(provider.labelKey), value: provider.value })), [translate]);

  if (activeSection === 'email') {
    return (
      <div className="grid grid-cols-1 xl:grid-cols-[1fr_320px] gap-4">
        <FormGrid>
          <SwitchField label={translate('page.abpInfrastructureSettings.field.emailEnabled')} checked={formState.emailEnabled} onChange={(value) => onChange('emailEnabled', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.smtpHost')} value={formState.smtpHost} onChange={(value) => onChange('smtpHost', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.smtpPort')} type="number" value={formState.smtpPort} onChange={(value) => onChange('smtpPort', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.smtpUser')} value={formState.emailUserName} onChange={(value) => onChange('emailUserName', value)} />
          <SecretField
            clear={formState.emailPasswordClear}
            configured={settings.email.password.isConfigured}
            clearLabel={translate('common.clear')}
            configuredLabel={translate('common.configured')}
            notConfiguredLabel={translate('common.notConfigured')}
            label={translate('page.abpInfrastructureSettings.field.smtpPassword')}
            value={formState.emailPassword}
            onChange={(value) => onChange('emailPassword', value)}
            onClearChange={(value) => onChange('emailPasswordClear', value)}
          />
          <TextField label={translate('page.abpInfrastructureSettings.field.defaultFromAddress')} value={formState.defaultFromAddress} onChange={(value) => onChange('defaultFromAddress', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.defaultFromDisplayName')} value={formState.defaultFromDisplayName} onChange={(value) => onChange('defaultFromDisplayName', value)} />
          <SwitchField label={translate('page.abpInfrastructureSettings.field.emailEnableSsl')} checked={formState.emailEnableSsl} onChange={(value) => onChange('emailEnableSsl', value)} />
        </FormGrid>
        <TestPanel
          buttonLabel={testing.email ? translate('page.abpInfrastructureSettings.test.running') : translate('page.abpInfrastructureSettings.test.sendEmail')}
          disabled={testing.email}
          failureLabel={translate('page.abpInfrastructureSettings.result.failed')}
          permissionCode="system:abp-setting:test"
          result={lastTestResult}
          successLabel={translate('page.abpInfrastructureSettings.result.success')}
          title={translate('page.abpInfrastructureSettings.test.title')}
          onRun={onRunEmailTest}
        >
          <TextField label={translate('page.abpInfrastructureSettings.field.emailRecipient')} value={testForm.emailTo} onChange={(value) => onTestFormChange((current) => ({ ...current, emailTo: value }))} />
        </TestPanel>
      </div>
    );
  }

  if (activeSection === 'sms') {
    return (
      <div className="grid grid-cols-1 xl:grid-cols-[1fr_320px] gap-4">
        <FormGrid>
          <SwitchField label={translate('page.abpInfrastructureSettings.field.smsEnabled')} checked={formState.smsEnabled} onChange={(value) => onChange('smsEnabled', value)} />
          <SelectField label={translate('page.abpInfrastructureSettings.field.smsProvider')} options={smsProviderOptions} value={formState.smsProvider} onChange={(value) => onChange('smsProvider', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.aliyunAccessKeyId')} value={formState.aliyunAccessKeyId} onChange={(value) => onChange('aliyunAccessKeyId', value)} />
          <SecretField
            clear={formState.aliyunAccessKeySecretClear}
            configured={settings.sms.aliyunAccessKeySecret.isConfigured}
            clearLabel={translate('common.clear')}
            configuredLabel={translate('common.configured')}
            notConfiguredLabel={translate('common.notConfigured')}
            label={translate('page.abpInfrastructureSettings.field.aliyunAccessKeySecret')}
            value={formState.aliyunAccessKeySecret}
            onChange={(value) => onChange('aliyunAccessKeySecret', value)}
            onClearChange={(value) => onChange('aliyunAccessKeySecretClear', value)}
          />
          <TextField label={translate('page.abpInfrastructureSettings.field.aliyunSignName')} value={formState.aliyunSignName} onChange={(value) => onChange('aliyunSignName', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.aliyunTemplateCode')} value={formState.aliyunTemplateCode} onChange={(value) => onChange('aliyunTemplateCode', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.aliyunTemplateParamName')} value={formState.aliyunTemplateParamName} onChange={(value) => onChange('aliyunTemplateParamName', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.tencentSecretId')} value={formState.tencentSecretId} onChange={(value) => onChange('tencentSecretId', value)} />
          <SecretField
            clear={formState.tencentSecretKeyClear}
            configured={settings.sms.tencentSecretKey.isConfigured}
            clearLabel={translate('common.clear')}
            configuredLabel={translate('common.configured')}
            notConfiguredLabel={translate('common.notConfigured')}
            label={translate('page.abpInfrastructureSettings.field.tencentSecretKey')}
            value={formState.tencentSecretKey}
            onChange={(value) => onChange('tencentSecretKey', value)}
            onClearChange={(value) => onChange('tencentSecretKeyClear', value)}
          />
          <TextField label={translate('page.abpInfrastructureSettings.field.tencentSdkAppId')} value={formState.tencentSdkAppId} onChange={(value) => onChange('tencentSdkAppId', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.tencentSignName')} value={formState.tencentSignName} onChange={(value) => onChange('tencentSignName', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.tencentTemplateId')} value={formState.tencentTemplateId} onChange={(value) => onChange('tencentTemplateId', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.tencentRegion')} value={formState.tencentRegion} onChange={(value) => onChange('tencentRegion', value)} />
        </FormGrid>
        <TestPanel
          buttonLabel={testing.sms ? translate('page.abpInfrastructureSettings.test.running') : translate('page.abpInfrastructureSettings.test.sendSms')}
          disabled={testing.sms}
          failureLabel={translate('page.abpInfrastructureSettings.result.failed')}
          permissionCode="system:abp-setting:test"
          result={lastTestResult}
          successLabel={translate('page.abpInfrastructureSettings.result.success')}
          title={translate('page.abpInfrastructureSettings.test.title')}
          onRun={onRunSmsTest}
        >
          <TextField label={translate('page.abpInfrastructureSettings.field.smsPhoneNumber')} value={testForm.smsPhoneNumber} onChange={(value) => onTestFormChange((current) => ({ ...current, smsPhoneNumber: value }))} />
        </TestPanel>
      </div>
    );
  }

  if (activeSection === 'objectStorage') {
    return (
      <div className="grid grid-cols-1 xl:grid-cols-[1fr_320px] gap-4">
        <FormGrid>
          <SelectField label={translate('page.abpInfrastructureSettings.field.objectStorageProvider')} options={objectStorageProviderOptions} value={formState.objectStorageProvider} onChange={(value) => onChange('objectStorageProvider', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.fileSystemBasePath')} value={formState.fileSystemBasePath} onChange={(value) => onChange('fileSystemBasePath', value)} />
          <SwitchField label={translate('page.abpInfrastructureSettings.field.fileSystemAppendContainerName')} checked={formState.fileSystemAppendContainerName} onChange={(value) => onChange('fileSystemAppendContainerName', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.ossAliyunEndpoint')} value={formState.ossAliyunEndpoint} onChange={(value) => onChange('ossAliyunEndpoint', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.ossAliyunBucketName')} value={formState.ossAliyunBucketName} onChange={(value) => onChange('ossAliyunBucketName', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.ossAliyunAccessKeyId')} value={formState.ossAliyunAccessKeyId} onChange={(value) => onChange('ossAliyunAccessKeyId', value)} />
          <SecretField
            clear={formState.ossAliyunAccessKeySecretClear}
            configured={settings.objectStorage.aliyunAccessKeySecret.isConfigured}
            clearLabel={translate('common.clear')}
            configuredLabel={translate('common.configured')}
            notConfiguredLabel={translate('common.notConfigured')}
            label={translate('page.abpInfrastructureSettings.field.ossAliyunAccessKeySecret')}
            value={formState.ossAliyunAccessKeySecret}
            onChange={(value) => onChange('ossAliyunAccessKeySecret', value)}
            onClearChange={(value) => onChange('ossAliyunAccessKeySecretClear', value)}
          />
          <TextField label={translate('page.abpInfrastructureSettings.field.minioEndpoint')} value={formState.minioEndpoint} onChange={(value) => onChange('minioEndpoint', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.minioBucketName')} value={formState.minioBucketName} onChange={(value) => onChange('minioBucketName', value)} />
          <TextField label={translate('page.abpInfrastructureSettings.field.minioAccessKey')} value={formState.minioAccessKey} onChange={(value) => onChange('minioAccessKey', value)} />
          <SecretField
            clear={formState.minioSecretKeyClear}
            configured={settings.objectStorage.minioSecretKey.isConfigured}
            clearLabel={translate('common.clear')}
            configuredLabel={translate('common.configured')}
            notConfiguredLabel={translate('common.notConfigured')}
            label={translate('page.abpInfrastructureSettings.field.minioSecretKey')}
            value={formState.minioSecretKey}
            onChange={(value) => onChange('minioSecretKey', value)}
            onClearChange={(value) => onChange('minioSecretKeyClear', value)}
          />
          <SwitchField label={translate('page.abpInfrastructureSettings.field.minioWithSsl')} checked={formState.minioWithSsl} onChange={(value) => onChange('minioWithSsl', value)} />
        </FormGrid>
        <TestPanel
          buttonLabel={testing.objectStorage ? translate('page.abpInfrastructureSettings.test.running') : translate('page.abpInfrastructureSettings.test.testObjectStorage')}
          disabled={testing.objectStorage}
          failureLabel={translate('page.abpInfrastructureSettings.result.failed')}
          permissionCode="system:abp-setting:test"
          result={lastTestResult}
          successLabel={translate('page.abpInfrastructureSettings.result.success')}
          title={translate('page.abpInfrastructureSettings.test.title')}
          onRun={onRunObjectStorageTest}
        >
          <SelectField
            label={translate('page.abpInfrastructureSettings.field.objectStorageProvider')}
            options={objectStorageProviderOptions}
            value={testForm.objectStorageProvider}
            onChange={(value) => onTestFormChange((current) => ({ ...current, objectStorageProvider: value }))}
          />
        </TestPanel>
      </div>
    );
  }

  if (activeSection === 'cache') {
    return (
      <FormGrid>
        <SelectField label={translate('page.abpInfrastructureSettings.field.cacheProvider')} options={cacheProviderOptions} value={formState.cacheProvider} onChange={(value) => onChange('cacheProvider', value)} />
        <SecretField
          clear={formState.redisConfigurationClear}
          configured={settings.cache.redisConfiguration.isConfigured}
          clearLabel={translate('common.clear')}
          configuredLabel={translate('common.configured')}
          notConfiguredLabel={translate('common.notConfigured')}
          label={translate('page.abpInfrastructureSettings.field.redisConfiguration')}
          value={formState.redisConfiguration}
          onChange={(value) => onChange('redisConfiguration', value)}
          onClearChange={(value) => onChange('redisConfigurationClear', value)}
        />
        <TextField
          label={translate('page.abpInfrastructureSettings.field.cacheDefaultExpirationMinutes')}
          type="number"
          value={formState.cacheDefaultExpirationMinutes}
          onChange={(value) => onChange('cacheDefaultExpirationMinutes', value)}
        />
      </FormGrid>
    );
  }

  if (activeSection === 'jobs') {
    return (
      <FormGrid>
        <SwitchField label={translate('page.abpInfrastructureSettings.field.abpBackgroundJobsEnabled')} checked={formState.abpBackgroundJobsEnabled} onChange={(value) => onChange('abpBackgroundJobsEnabled', value)} />
        <SwitchField label={translate('page.abpInfrastructureSettings.field.messagingJobsEnabled')} checked={formState.messagingJobsEnabled} onChange={(value) => onChange('messagingJobsEnabled', value)} />
        <TextField label={translate('page.abpInfrastructureSettings.field.jobsTestTimeoutSeconds')} type="number" value={formState.jobsTestTimeoutSeconds} onChange={(value) => onChange('jobsTestTimeoutSeconds', value)} />
      </FormGrid>
    );
  }

  return (
    <FormGrid>
      <SwitchField label={translate('page.abpInfrastructureSettings.field.operationLogEnabled')} checked={formState.operationLogEnabled} onChange={(value) => onChange('operationLogEnabled', value)} />
      <SwitchField label={translate('page.abpInfrastructureSettings.field.captureQueryString')} checked={formState.captureQueryString} onChange={(value) => onChange('captureQueryString', value)} />
      <TextField label={translate('page.abpInfrastructureSettings.field.auditQueueCapacity')} type="number" value={formState.auditQueueCapacity} onChange={(value) => onChange('auditQueueCapacity', value)} />
    </FormGrid>
  );
}

export function RuntimeSummary({ settings }: { settings: InfrastructureSettingsDto }) {
  const { translate } = useI18n();
  return (
    <div className="flex flex-wrap gap-2 text-xs">
      <StatusPill active={settings.email.enabled} label={translate('page.abpInfrastructureSettings.section.email')} />
      <StatusPill active={settings.sms.enabled} label={`${translate('page.abpInfrastructureSettings.section.sms')} ${translateProvider(settings.sms.provider, translate)}`} />
      <StatusPill active label={`${translate('page.abpInfrastructureSettings.section.objectStorage')} ${translateProvider(settings.objectStorage.provider, translate)}`} />
      <StatusPill active={settings.cache.provider === 'Redis'} label={`${translate('page.abpInfrastructureSettings.section.cache')} ${translateProvider(settings.cache.provider, translate)}`} />
      <StatusPill active={settings.audit.operationLogEnabled} label={translate('page.abpInfrastructureSettings.section.audit')} />
    </div>
  );
}

export function MessageLogSearchBar({
  onChange,
  onReset,
  onSearch,
  value
}: {
  onChange: (value: MessageLogSearchState) => void;
  onReset: () => void;
  onSearch: () => void;
  value: MessageLogSearchState;
}) {
  const { translate } = useI18n();
  const update = (key: keyof MessageLogSearchState, nextValue: string) => {
    onChange({ ...value, [key]: nextValue });
  };

  return (
    <div className="flex flex-wrap gap-2 items-center">
      <SmallInput label={translate('page.abpInfrastructureSettings.search.channel')} value={value.channel} onChange={(nextValue) => update('channel', nextValue)} onEnter={onSearch} />
      <SmallInput label={translate('page.abpInfrastructureSettings.search.provider')} value={value.provider} onChange={(nextValue) => update('provider', nextValue)} onEnter={onSearch} />
      <SmallInput label={translate('page.abpInfrastructureSettings.search.result')} value={value.result} onChange={(nextValue) => update('result', nextValue)} onEnter={onSearch} />
      <SmallInput label={translate('page.abpInfrastructureSettings.search.traceId')} value={value.traceId} onChange={(nextValue) => update('traceId', nextValue)} onEnter={onSearch} />
      <PermissionButton
        code="system:abp-setting:query"
        className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-primary-50 hover:text-primary-600 transition-colors"
        type="button"
        onClick={onSearch}
      >
        {translate('page.abpInfrastructureSettings.search.query')}
      </PermissionButton>
      <button className="text-gray-500 px-2 py-1.5 text-sm hover:text-gray-700 transition-colors" type="button" onClick={onReset}>
        {translate('page.abpInfrastructureSettings.search.reset')}
      </button>
    </div>
  );
}

export function ChannelBadge({ value }: { value: string }) {
  const isEmail = value.toLowerCase() === 'email';
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${isEmail ? 'bg-sky-50 text-sky-700' : 'bg-violet-50 text-violet-700'}`}>
      {value}
    </span>
  );
}

export function ResultBadge({ success, value }: { success: boolean; value: string }) {
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${success ? 'bg-emerald-50 text-emerald-700' : 'bg-red-50 text-red-700'}`}>
      {value}
    </span>
  );
}

function FormGrid({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-1 md:grid-cols-2 2xl:grid-cols-3 gap-4">{children}</div>;
}

function TextField({
  label,
  onChange,
  placeholder,
  type = 'text',
  value
}: {
  label: string;
  onChange: (value: string) => void;
  placeholder?: string;
  type?: 'number' | 'password' | 'text';
  value: string;
}) {
  return (
    <label className="flex flex-col gap-1.5 text-sm text-gray-700">
      <span>{label}</span>
      <input
        className="border border-gray-300 rounded bg-white px-3 py-2 text-sm focus:outline-none focus:border-primary-500"
        placeholder={placeholder}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function SelectField({
  label,
  onChange,
  options,
  value
}: {
  label: string;
  onChange: (value: string) => void;
  options: Array<{ label: string; value: string }>;
  value: string;
}) {
  return (
    <label className="flex flex-col gap-1.5 text-sm text-gray-700">
      <span>{label}</span>
      <select
        className="border border-gray-300 rounded bg-white px-3 py-2 text-sm focus:outline-none focus:border-primary-500"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}

function SwitchField({
  checked,
  label,
  onChange
}: {
  checked: boolean;
  label: string;
  onChange: (value: boolean) => void;
}) {
  return (
    <label className="flex items-center justify-between gap-3 rounded border border-gray-200 px-3 py-2 text-sm text-gray-700">
      <span>{label}</span>
      <input
        checked={checked}
        className="h-4 w-4 accent-primary-600"
        type="checkbox"
        onChange={(event) => onChange(event.target.checked)}
      />
    </label>
  );
}

function SecretField({
  clear,
  configured,
  clearLabel,
  configuredLabel,
  notConfiguredLabel,
  label,
  onChange,
  onClearChange,
  value
}: {
  clear: boolean;
  configured: boolean;
  clearLabel: string;
  configuredLabel: string;
  notConfiguredLabel: string;
  label: string;
  onChange: (value: string) => void;
  onClearChange: (value: boolean) => void;
  value: string;
}) {
  return (
    <div className="flex flex-col gap-1.5 text-sm text-gray-700">
      <div className="flex items-center justify-between gap-2">
        <span>{label}</span>
        <span className={`rounded px-2 py-0.5 text-xs ${configured ? 'bg-emerald-50 text-emerald-700' : 'bg-gray-100 text-gray-500'}`}>
          {configured ? configuredLabel : notConfiguredLabel}
        </span>
      </div>
      <input
        className="border border-gray-300 rounded bg-white px-3 py-2 text-sm focus:outline-none focus:border-primary-500"
        type="password"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      <label className="inline-flex items-center gap-2 text-xs text-gray-500">
        <input
          checked={clear}
          className="h-3.5 w-3.5 accent-primary-600"
          type="checkbox"
          onChange={(event) => onClearChange(event.target.checked)}
        />
        {clearLabel}
      </label>
    </div>
  );
}

function TestPanel({
  buttonLabel,
  children,
  disabled,
  failureLabel,
  onRun,
  permissionCode,
  result,
  successLabel,
  title
}: {
  buttonLabel: string;
  children: ReactNode;
  disabled: boolean;
  failureLabel: string;
  onRun: () => void;
  permissionCode: string;
  result: InfrastructureTestResultDto | null;
  successLabel: string;
  title: string;
}) {
  return (
    <div className="rounded border border-gray-200 bg-gray-50 p-3 flex flex-col gap-3">
      <div className="text-sm font-semibold text-gray-800">{title}</div>
      {children}
      <PermissionButton
        code={permissionCode}
        className="inline-flex items-center justify-center gap-1 rounded bg-white border border-gray-300 px-3 py-2 text-sm text-gray-700 hover:bg-primary-50 hover:text-primary-700 disabled:opacity-60"
        disabled={disabled}
        type="button"
        onClick={onRun}
      >
        <AppIcon name="play" /> {buttonLabel}
      </PermissionButton>
      {result ? (
        <div className="rounded border border-gray-200 bg-white px-3 py-2 text-xs text-gray-600">
          <div className="flex items-center justify-between gap-2">
            <ResultBadge success={result.success} value={result.success ? successLabel : failureLabel} />
            <span>{result.durationMs} ms</span>
          </div>
          <div className="mt-2 break-all">{result.message}</div>
          <div className="mt-1 font-mono break-all text-gray-500">{result.traceId}</div>
        </div>
      ) : null}
    </div>
  );
}

function SmallInput({
  label,
  onChange,
  onEnter,
  value
}: {
  label: string;
  onChange: (value: string) => void;
  onEnter: () => void;
  value: string;
}) {
  return (
    <label className="flex items-center gap-1.5 text-sm text-gray-600">
      {label}
      <input
        className="border border-gray-300 rounded bg-white px-2 py-1.5 text-sm w-28 focus:outline-none focus:border-primary-500"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            onEnter();
          }
        }}
      />
    </label>
  );
}

function StatusPill({ active, label }: { active: boolean; label: string }) {
  return (
    <span className={`rounded border px-2 py-1 ${active ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : 'border-gray-200 bg-gray-50 text-gray-500'}`}>
      {label}
    </span>
  );
}

function translateProvider(value: string, translate: (key: string) => string) {
  switch (value) {
    case 'Aliyun':
      return translate('page.abpInfrastructureSettings.provider.aliyun');
    case 'Tencent':
      return translate('page.abpInfrastructureSettings.provider.tencent');
    case 'FileSystem':
      return translate('page.abpInfrastructureSettings.provider.fileSystem');
    case 'Minio':
      return translate('page.abpInfrastructureSettings.provider.minio');
    case 'Memory':
      return translate('page.abpInfrastructureSettings.provider.memory');
    case 'Redis':
      return translate('page.abpInfrastructureSettings.provider.redis');
    case 'Null':
      return translate('page.abpInfrastructureSettings.provider.null');
    default:
      return value;
  }
}
