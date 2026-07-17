import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  deleteApplicationPublishArtifact,
  downloadApplicationPublishArtifact,
  getApplicationPublishArtifacts,
  getApplicationPublishLogs,
  getApplicationPublishTask,
  getApplicationPublishTasks,
  packageApplicationPublishTask,
  publishApplication,
  type ApplicationPublishArtifactDto,
  type ApplicationPublishLogDto,
  type ApplicationPublishRequest,
  type ApplicationPublishTaskDto
} from '../../api/platform/application-publish.api';
import { createApplication, deleteApplication, getApplications, updateApplication } from '../../api/platform/platform-management.api';
import type { ApplicationListItemDto, ApplicationUpsertRequest } from '../../api/platform/platform.types';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useAuthStore } from '../../core/state/authStore';
import { useWorkspaceStore } from '../../core/state/workspaceStore';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { AppIcon } from '../../shared/icons/AppIcon';
import { ResponsiveModal } from '../../shared/responsive/ResponsiveModal';
import { DataTable } from '../../shared/table/DataTable';
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn, DataTableQueryState } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { ApplicationQuickActionsPanel } from './ApplicationQuickActionsPanel';
import { ApplicationTenantPickerDrawer } from './ApplicationTenantPickerDrawer';
import { PlatformResourcePage, type PlatformResourceController } from './PlatformResourcePage';

interface PublishFormState {
  backendHost: string;
  backendPort: number | '';
  cleanOutput: boolean;
  frontendApiBaseUrl: string;
  frontendBasePath: string;
  includeBackend: boolean;
  includeFrontend: boolean;
  remark: string;
  tenantId: string;
  version: string;
}

interface ApplicationSearchState {
  keyword: string;
  status: string;
}

type PublishPanelTab = 'artifacts' | 'logs' | 'tasks';

const defaultFormState: ApplicationUpsertRequest = {
  appCode: '',
  appName: '',
  appType: 'Business',
  adminDefaultRoutePath: '/console',
  defaultRoutePath: '/console',
  icon: '',
  remark: '',
  runtimeDefaultRoutePath: '/runtime',
  status: 'Enabled',
  version: '1.0.0'
};

const defaultPublishFormState: PublishFormState = {
  backendHost: '127.0.0.1',
  backendPort: 5000,
  cleanOutput: false,
  frontendApiBaseUrl: '/api',
  frontendBasePath: '',
  includeBackend: true,
  includeFrontend: true,
  remark: '',
  tenantId: '',
  version: ''
};

const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

export function PlatformApplicationsPage() {
  const confirm = useConfirm();
  const message = useMessage();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { translate } = useI18n();
  const availableWorkspaces = useWorkspaceStore((state) => state.availableWorkspaces);
  const enterApplicationBackend = useAuthStore((state) => state.enterApplicationBackend);
  const refreshSession = useAuthStore((state) => state.refreshSession);
  const switchPlatform = useAuthStore((state) => state.switchPlatform);
  const [publishTarget, setPublishTarget] = useState<ApplicationListItemDto | null>(null);
  const [publishForm, setPublishForm] = useState<PublishFormState>(defaultPublishFormState);
  const [publishModalOpen, setPublishModalOpen] = useState(false);
  const [publishPanelOpen, setPublishPanelOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<PublishPanelTab>('tasks');
  const [activeTaskId, setActiveTaskId] = useState<string | null>(null);
  const [taskPageIndex, setTaskPageIndex] = useState(1);
  const [taskPageSize, setTaskPageSize] = useState(10);
  const [logPageIndex, setLogPageIndex] = useState(1);
  const [logPageSize, setLogPageSize] = useState(50);
  const [artifactPageIndex, setArtifactPageIndex] = useState(1);
  const [artifactPageSize, setArtifactPageSize] = useState(10);
  const [appTypeModalOpen, setAppTypeModalOpen] = useState(false);
  const [selectedAppType, setSelectedAppType] = useState<'Business' | 'LowCode'>('Business');
  const [resourceController, setResourceController] = useState<PlatformResourceController<ApplicationListItemDto, ApplicationUpsertRequest, ApplicationSearchState> | null>(null);
  const [selectedApplication, setSelectedApplication] = useState<ApplicationListItemDto | null>(null);
  const [tenantPickerApplication, setTenantPickerApplication] = useState<ApplicationListItemDto | null>(null);
  const [enteringAppCode, setEnteringAppCode] = useState<string | null>(null);
  const [enteringTenantId, setEnteringTenantId] = useState<string | null>(null);

  const appTypeFilterOptions = useMemo(
    () => [
      { label: translate('page.platformApplications.appType.business'), value: 'Business' },
      { label: translate('page.platformApplications.appType.lowCode'), value: 'LowCode' }
    ],
    [translate]
  );

  const statusFilterOptions = useMemo(
    () => [
      { label: translate('platform.common.enabled'), value: 'Enabled' },
      { label: translate('platform.common.disabled'), value: 'Disabled' }
    ],
    [translate]
  );

  const columns = useMemo<DataTableColumn<ApplicationListItemDto>[]>(
    () => [
      { key: 'appName', title: translate('page.platformApplications.field.appName'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      { key: 'appCode', title: translate('page.platformApplications.field.appCode'), width: '130px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      {
        key: 'appType',
        title: translate('page.platformApplications.field.appType'),
        width: '120px',
        sortable: true,
        filterable: true,
        filterType: 'select',
        filterOptions: appTypeFilterOptions,
        render: (row) => {
          if (row.appType === 'Platform') return translate('page.platformApplications.appType.platform');
          if (row.appType === 'Business') return translate('page.platformApplications.appType.business');
          if (row.appType === 'LowCode') return translate('page.platformApplications.appType.lowCode');
          return row.appType;
        }
      },
      { key: 'adminDefaultRoutePath', title: translate('page.platformApplications.field.adminDefaultRoutePath'), width: '160px', render: (row) => row.adminDefaultRoutePath ?? row.defaultRoutePath ?? '-', sortable: true, filterable: true, filterType: 'text' },
      { key: 'runtimeDefaultRoutePath', title: translate('page.platformApplications.field.runtimeDefaultRoutePath'), width: '160px', render: (row) => row.runtimeDefaultRoutePath ?? '-', sortable: true, filterable: true, filterType: 'text' },
      { key: 'status', title: translate('page.platformApplications.field.status'), width: '90px', align: 'center', render: (row) => (row.status === 'Enabled' ? translate('platform.common.enabled') : translate('platform.common.disabled')), sortable: true, filterable: true, filterType: 'select', filterOptions: statusFilterOptions },
      { key: 'version', title: translate('page.platformApplications.field.version'), width: '100px', render: (row) => row.version ?? '-', sortable: true, filterable: true, filterType: 'text' }
    ],
    [appTypeFilterOptions, statusFilterOptions, translate]
  );

  const fields = useMemo<FormFieldConfig<ApplicationUpsertRequest>[]>(
    () => [
      { label: translate('page.platformApplications.field.appName'), name: 'appName', required: true, span: 1, type: 'text', section: translate('page.platformApplications.section.basicInfo') },
      { label: translate('page.platformApplications.field.appCode'), name: 'appCode', required: true, span: 1, type: 'text', section: translate('page.platformApplications.section.basicInfo') },
      { label: translate('page.platformApplications.field.appType'), name: 'appType', required: true, span: 1, type: 'select', options: appTypeFilterOptions, section: translate('page.platformApplications.section.basicInfo') },
      { label: translate('page.platformApplications.field.status'), name: 'status', required: true, span: 1, type: 'select', options: statusFilterOptions, section: translate('page.platformApplications.section.basicInfo') },
      { label: translate('page.platformApplications.field.adminDefaultRoutePath'), name: 'adminDefaultRoutePath', span: 1, type: 'text', section: translate('page.platformApplications.section.displayConfig') },
      { label: translate('page.platformApplications.field.runtimeDefaultRoutePath'), name: 'runtimeDefaultRoutePath', span: 1, type: 'text', section: translate('page.platformApplications.section.displayConfig') },
      { label: translate('page.platformApplications.field.icon'), name: 'icon', span: 1, type: 'text', section: translate('page.platformApplications.section.displayConfig') },
      { label: translate('page.platformApplications.field.version'), name: 'version', span: 1, type: 'text', section: translate('page.platformApplications.section.releaseInfo') },
      { label: translate('page.platformApplications.field.remark'), name: 'remark', rows: 3, span: 2, type: 'textarea', section: translate('page.platformApplications.section.remark') }
    ],
    [appTypeFilterOptions, statusFilterOptions, translate]
  );

  const publishFields = useMemo<FormFieldConfig<PublishFormState>[]>(
    () => [
      { label: translate('page.platformApplications.publish.field.version'), name: 'version', span: 1, type: 'text', section: translate('page.platformApplications.publish.section.config'), helpText: translate('page.platformApplications.publish.help.version') },
      { label: translate('page.platformApplications.publish.field.tenantScope'), name: 'tenantId', span: 1, type: 'text', section: translate('page.platformApplications.publish.section.config'), helpText: translate('page.platformApplications.publish.help.tenantScope') },
      { label: translate('page.platformApplications.publish.field.backendHost'), name: 'backendHost', span: 1, type: 'text', section: translate('page.platformApplications.publish.section.runtime'), helpText: translate('page.platformApplications.publish.help.backendHost') },
      { label: translate('page.platformApplications.publish.field.backendPort'), name: 'backendPort', span: 1, type: 'number', section: translate('page.platformApplications.publish.section.runtime'), helpText: translate('page.platformApplications.publish.help.backendPort') },
      { label: translate('page.platformApplications.publish.field.frontendBasePath'), name: 'frontendBasePath', span: 1, type: 'text', section: translate('page.platformApplications.publish.section.runtime'), helpText: translate('page.platformApplications.publish.help.frontendBasePath') },
      { label: translate('page.platformApplications.publish.field.frontendApiBaseUrl'), name: 'frontendApiBaseUrl', span: 1, type: 'text', section: translate('page.platformApplications.publish.section.runtime'), helpText: translate('page.platformApplications.publish.help.frontendApiBaseUrl') },
      { label: translate('page.platformApplications.publish.field.includeBackend'), name: 'includeBackend', span: 1, type: 'switch', section: translate('page.platformApplications.publish.section.artifactScope'), helpText: translate('page.platformApplications.publish.help.includeBackend') },
      { label: translate('page.platformApplications.publish.field.includeFrontend'), name: 'includeFrontend', span: 1, type: 'switch', section: translate('page.platformApplications.publish.section.artifactScope'), helpText: translate('page.platformApplications.publish.help.includeFrontend') },
      { label: translate('page.platformApplications.publish.field.cleanOutput'), name: 'cleanOutput', span: 1, type: 'switch', section: translate('page.platformApplications.publish.section.securityPolicy'), helpText: translate('page.platformApplications.publish.help.cleanOutput') },
      { label: translate('page.platformApplications.publish.field.remark'), name: 'remark', rows: 3, span: 2, type: 'textarea', section: translate('page.platformApplications.publish.section.remark') }
    ],
    [translate]
  );

  const selectedAppId = publishTarget?.id ?? '';
  const tasksQuery = useApiQuery({
    enabled: publishPanelOpen && selectedAppId.length > 0,
    keepPreviousData: true,
    queryFn: ({ signal }) => getApplicationPublishTasks(selectedAppId, taskPageIndex, taskPageSize, signal),
    queryKey: queryKeys.platform.applicationPublishTasks(selectedAppId || 'none', taskPageIndex, taskPageSize)
  });
  const taskRows = useMemo(() => tasksQuery.data?.data.items ?? [], [tasksQuery.data?.data.items]);
  const selectedTaskFromList = taskRows.find((task) => task.id === activeTaskId) ?? taskRows[0] ?? null;
  const selectedTaskId = activeTaskId ?? selectedTaskFromList?.id ?? '';

  const taskDetailQuery = useApiQuery({
    enabled: publishPanelOpen && selectedTaskId.length > 0,
    queryFn: ({ signal }) => getApplicationPublishTask(selectedTaskId, signal),
    queryKey: queryKeys.platform.applicationPublishTask(selectedTaskId || 'none')
  });
  const activeTask = taskDetailQuery.data?.data ?? selectedTaskFromList;

  const logsQuery = useApiQuery({
    enabled: publishPanelOpen && selectedTaskId.length > 0,
    keepPreviousData: true,
    queryFn: ({ signal }) => getApplicationPublishLogs(selectedTaskId, logPageIndex, logPageSize, signal),
    queryKey: queryKeys.platform.applicationPublishLogs(selectedTaskId || 'none', logPageIndex, logPageSize)
  });

  const artifactsQuery = useApiQuery({
    enabled: publishPanelOpen && selectedAppId.length > 0,
    keepPreviousData: true,
    queryFn: ({ signal }) => getApplicationPublishArtifacts(selectedAppId, artifactPageIndex, artifactPageSize, signal),
    queryKey: queryKeys.platform.applicationPublishArtifacts(selectedAppId || 'none', artifactPageIndex, artifactPageSize)
  });

  const publishMutation = useApiMutation({
    mutationFn: ({ appId, request }: { appId: string; request: ApplicationPublishRequest }) => publishApplication(appId, request)
  });
  const deleteArtifactMutation = useApiMutation({
    mutationFn: (artifactId: string) => deleteApplicationPublishArtifact(artifactId)
  });
  const packageTaskMutation = useApiMutation({
    mutationFn: (taskId: string) => packageApplicationPublishTask(taskId)
  });

  const taskColumns: DataTableColumn<ApplicationPublishTaskDto>[] = useMemo(
    () => [
      { key: 'status', title: translate('page.platformApplications.publish.summary.taskStatus'), width: '100px', align: 'center', responsivePriority: 100, render: (row) => <PublishStatusBadge status={row.status} translate={translate} /> },
      { key: 'stage', title: translate('page.platformApplications.publish.summary.currentStage'), width: '110px', responsivePriority: 95, render: (row) => row.stage || '-' },
      { key: 'progressPercent', title: translate('page.platformApplications.publish.summary.progress'), width: '130px', responsivePriority: 100, render: (row) => <ProgressCell value={row.progressPercent} /> },
      { key: 'version', title: translate('page.platformApplications.field.version'), width: '110px', responsivePriority: 80, render: (row) => row.version ?? '-' },
      { key: 'createdTime', title: translate('page.platformApplications.publish.columns.createdTime'), width: '170px', responsivePriority: 75, render: (row) => formatDateTime(row.createdTime) },
      { key: 'durationMs', title: translate('page.platformApplications.publish.summary.duration'), width: '100px', align: 'right', responsivePriority: 70, render: (row) => formatDuration(row.durationMs) },
      { key: 'traceId', title: translate('page.platformApplications.publish.summary.traceId'), width: '180px', responsivePriority: 50, render: (row) => <span className="font-mono text-xs">{row.traceId}</span> }
    ],
    [translate]
  );

  const logColumns: DataTableColumn<ApplicationPublishLogDto>[] = useMemo(
    () => [
      { key: 'createdTime', title: translate('page.platformApplications.publish.columns.createdTime'), width: '170px', responsivePriority: 100, render: (row) => formatDateTime(row.createdTime) },
      { key: 'level', title: translate('page.platformApplications.publish.columns.level'), width: '90px', align: 'center', responsivePriority: 95, render: (row) => <LogLevelBadge level={row.level} /> },
      { key: 'stage', title: translate('page.platformApplications.publish.summary.currentStage'), width: '110px', responsivePriority: 85, render: (row) => row.stage || '-' },
      { key: 'message', title: translate('page.platformApplications.publish.columns.message'), responsivePriority: 100, render: (row) => <span className="inline-block max-w-[560px] truncate align-bottom" title={row.message}>{row.message}</span> },
      { key: 'traceId', title: translate('page.platformApplications.publish.summary.traceId'), width: '180px', responsivePriority: 45, render: (row) => <span className="font-mono text-xs">{row.traceId}</span> }
    ],
    [translate]
  );

  const artifactColumns: DataTableColumn<ApplicationPublishArtifactDto>[] = useMemo(
    () => [
      { key: 'fileName', title: translate('page.platformApplications.publish.columns.fileName'), responsivePriority: 100, render: (row) => <span className="font-medium text-gray-900">{row.fileName}</span> },
      { key: 'sizeBytes', title: translate('page.platformApplications.publish.columns.size'), width: '100px', align: 'right', responsivePriority: 95, render: (row) => formatBytes(row.sizeBytes) },
      { key: 'createdTime', title: translate('page.platformApplications.publish.columns.createdTime'), width: '170px', responsivePriority: 80, render: (row) => formatDateTime(row.createdTime) },
      { key: 'sha256', title: 'SHA256', width: '220px', responsivePriority: 60, render: (row) => <span className="font-mono text-xs" title={row.sha256}>{row.sha256.slice(0, 20)}...</span> }
    ],
    [translate]
  );

  const refetchTasks = tasksQuery.refetch;
  const refetchTaskDetail = taskDetailQuery.refetch;
  const refetchLogs = logsQuery.refetch;
  const refetchArtifacts = artifactsQuery.refetch;
  const applicationApi = useMemo(
    () => ({
      create: async (request: ApplicationUpsertRequest) => {
        const response = await createApplication(request);
        await refreshSession({ preserveTabs: true });
        return response;
      },
      delete: deleteApplication,
      list: getApplications,
      update: updateApplication
    }),
    [refreshSession]
  );

  useEffect(() => {
    if (activeTaskId || taskRows.length === 0) {
      return;
    }

    setActiveTaskId(taskRows[0].id);
  }, [activeTaskId, taskRows]);

  useEffect(() => {
    if (!activeTask || !isRunningStatus(activeTask.status)) {
      return;
    }

    const intervalId = window.setInterval(() => {
      void refetchTaskDetail();
      void refetchTasks();
      void refetchLogs();
    }, 2500);

    return () => window.clearInterval(intervalId);
  }, [activeTask, refetchLogs, refetchTaskDetail, refetchTasks]);

  useEffect(() => {
    if (!activeTask || isRunningStatus(activeTask.status)) {
      return;
    }

    void refetchArtifacts();
  }, [activeTask, refetchArtifacts]);

  const openPublishModal = (row: ApplicationListItemDto) => {
    setPublishTarget(row);
    setPublishForm({
      ...defaultPublishFormState,
      frontendBasePath: `/${row.appCode}`,
      version: row.version ?? ''
    });
    setPublishModalOpen(true);
  };

  const openPublishPanel = (row: ApplicationListItemDto, taskId?: string) => {
    setPublishTarget(row);
    setActiveTaskId(taskId ?? null);
    setTaskPageIndex(1);
    setLogPageIndex(1);
    setArtifactPageIndex(1);
    setActiveTab(taskId ? 'logs' : 'tasks');
    setPublishPanelOpen(true);
  };

  const handlePublish = async () => {
    if (!publishTarget) {
      return;
    }

    if (!publishForm.includeBackend && !publishForm.includeFrontend) {
      message.error(translate('page.platformApplications.publish.noRange'));
      return;
    }

    try {
      const response = await publishMutation.mutateAsync({
        appId: publishTarget.id,
        request: mapPublishFormToRequest(publishForm)
      });
      setPublishModalOpen(false);
      setActiveTaskId(response.data.id);
      setTaskPageIndex(1);
      setLogPageIndex(1);
      setArtifactPageIndex(1);
      setActiveTab('tasks');
      setPublishPanelOpen(true);
      message.success(formatMessage(translate('page.platformApplications.publish.createSuccess'), { id: response.data.id }));
      await queryClient.invalidateQueries({ queryKey: queryKeys.platform.applicationsRoot() });
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.platformApplications.publish.createFailed')));
    }
  };

  const handleDownloadArtifact = async (artifact: ApplicationPublishArtifactDto) => {
    try {
      const { blob, fileName } = await downloadApplicationPublishArtifact(artifact);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName || artifact.fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      message.success(translate('page.platformApplications.publish.downloadStarted'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.platformApplications.publish.downloadFailed')));
    }
  };

  const handleDeleteArtifact = (artifact: ApplicationPublishArtifactDto) => {
    confirm({
      title: translate('page.platformApplications.publish.deleteTitle'),
      content: formatMessage(translate('page.platformApplications.publish.deleteContent'), { fileName: artifact.fileName }),
      confirmText: translate('page.platformApplications.publish.deleteConfirm'),
      onConfirm: async () => {
        try {
          await deleteArtifactMutation.mutateAsync(artifact.id);
          await refetchArtifacts();
          message.success(translate('page.platformApplications.publish.deleteSuccess'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.platformApplications.publish.deleteFailed')));
        }
      }
    });
  };

  const handlePackageTask = async (task: ApplicationPublishTaskDto) => {
    try {
      await packageTaskMutation.mutateAsync(task.id);
      setActiveTaskId(task.id);
      setActiveTab('artifacts');
      await refetchTaskDetail();
      await refetchTasks();
      await refetchArtifacts();
      message.success(translate('page.platformApplications.publish.repackageSuccess'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.platformApplications.publish.repackageFailed')));
    }
  };

  const closePublishPanel = () => {
    setPublishPanelOpen(false);
    setActiveTaskId(null);
  };

  const handleAdd = (resource: PlatformResourceController<ApplicationListItemDto, ApplicationUpsertRequest, ApplicationSearchState>) => {
    setResourceController(resource);
    setSelectedAppType('Business');
    setAppTypeModalOpen(true);
  };

  const handleNext = () => {
    setAppTypeModalOpen(false);
    if (resourceController) {
      resourceController.handleCreate();
      resourceController.setFormState((current) => ({
        ...current,
        appType: selectedAppType
      }));
    }
  };

  const getCandidateWorkspaces = (application: ApplicationListItemDto) => {
    const normalizedAppCode = application.appCode.toUpperCase();
    const expectedWorkspaceLevel = normalizedAppCode === 'SYSTEM' ? 'platform' : 'application';

    return availableWorkspaces.filter(
      (workspace) =>
        workspace.isAvailable &&
        workspace.workspaceLevel === expectedWorkspaceLevel &&
        workspace.appCode.toUpperCase() === normalizedAppCode
    );
  };

  const handleEnterPlatformApplication = async (application: ApplicationListItemDto) => {
    setEnteringAppCode(application.appCode);
    try {
      const response = await switchPlatform({ target: 'application-center' });
      navigate(response.defaultRoutePath || response.currentWorkspace.defaultRoutePath || '/platform/applications', { replace: true });
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.platformApplications.enter.failed')));
    } finally {
      setEnteringAppCode(null);
    }
  };

  const handleEnterApplication = async (application: ApplicationListItemDto, tenantId?: string) => {
    if (application.status !== 'Enabled') {
      message.info(translate('page.platformApplications.enter.disabled'));
      return;
    }

    if (application.appCode.toUpperCase() === 'SYSTEM') {
      await handleEnterPlatformApplication(application);
      return;
    }

    const candidates = getCandidateWorkspaces(application);
    if (candidates.length === 0) {
      message.error(translate('page.platformApplications.enter.noPermission'));
      return;
    }

    if (!tenantId && candidates.length > 1) {
      setTenantPickerApplication(application);
      return;
    }

    const targetTenantId = tenantId ?? candidates[0]?.tenantId;
    if (!targetTenantId) {
      message.error(translate('page.platformApplications.enter.noPermission'));
      return;
    }

    const targetWorkspace = candidates.find((workspace) => workspace.tenantId === targetTenantId);
    if (targetWorkspace && !targetWorkspace.isDatabaseBound) {
      navigate(`/tenants/${encodeURIComponent(targetWorkspace.tenantId)}/apps/${targetWorkspace.appCode.toUpperCase()}/login`);
      return;
    }

    setEnteringAppCode(application.appCode);
    setEnteringTenantId(targetTenantId);
    setTenantPickerApplication(null);
    try {
      const response = await enterApplicationBackend(application.appCode, {
        source: 'platform-application-center',
        tenantId: targetTenantId
      });
      navigate(response.defaultRoutePath || response.currentWorkspace.defaultRoutePath || application.adminDefaultRoutePath || `/tenants/${encodeURIComponent(targetTenantId)}/apps/${application.appCode.toUpperCase()}/admin/console`, { replace: true });
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.platformApplications.enter.failed')));
    } finally {
      setEnteringAppCode(null);
      setEnteringTenantId(null);
    }
  };

  const tenantPickerWorkspaces = tenantPickerApplication ? getCandidateWorkspaces(tenantPickerApplication) : [];

  return (
    <>
      <PlatformResourcePage
        api={applicationApi}
        columnSettingsKey="platform-applications"
        columns={columns}
        defaultFormState={defaultFormState}
        defaultSearchState={{ keyword: '', status: '' }}
        description={translate('page.platformApplications.description')}
        fields={fields}
        getDisplayName={(item) => item.appName}
        itemName={translate('page.platformApplications.itemName')}
        onAdd={handleAdd}
        onRow={(row) => setSelectedApplication(row)}
        onRowDoubleClick={(row) => void handleEnterApplication(row)}
        permissionCodes={{ add: 'platform:application:add', delete: 'platform:application:delete', edit: 'platform:application:edit' }}
        queryKeyPrefix={queryKeys.platform.applicationsRoot()}
        renderExtraRowActions={(row) => (
          <PermissionButton
            code="platform:application:enter"
            className="hover:text-primary-600 transition-colors disabled:text-gray-300 disabled:hover:text-gray-300"
            disabled={row.status !== 'Enabled' || enteringAppCode === row.appCode}
            title={row.status === 'Enabled' ? translate('page.platformApplications.enter.action') : translate('page.platformApplications.enter.disabled')}
            type="button"
            onClick={() => void handleEnterApplication(row)}
          >
            <AppIcon className="text-base" name="arrow-square-out" />
          </PermissionButton>
        )}
        rowClassName={(row) => (row.id === selectedApplication?.id ? 'bg-primary-50/60' : '')}
        text={{
          actionCreate: formatMessage(translate('platform.actions.create'), { itemName: translate('page.platformApplications.itemName') }),
          actionDelete: translate('platform.actions.delete'),
          actionEdit: translate('platform.actions.edit'),
          actionRefresh: translate('platform.actions.refresh'),
          modalCreateTitle: formatMessage(translate('platform.modal.create'), { itemName: translate('page.platformApplications.itemName') }),
          modalEditTitle: formatMessage(translate('platform.modal.edit'), { itemName: translate('page.platformApplications.itemName') }),
          searchKeywordPlaceholder: formatMessage(translate('platform.search.keywordPlaceholder'), { itemName: translate('page.platformApplications.itemName') })
        }}
        renderSidePanel={(resource) => (
          <ApplicationQuickActionsPanel
            application={selectedApplication}
            entering={Boolean(selectedApplication && enteringAppCode === selectedApplication.appCode)}
            onClose={() => setSelectedApplication(null)}
            onEdit={(application) => resource.handleEdit(application, (item) => ({ ...item } as ApplicationUpsertRequest))}
            onEnter={(application) => void handleEnterApplication(application)}
            onPublish={openPublishModal}
            onPublishRecords={openPublishPanel}
          />
        )}
        title={translate('page.platformApplications.title')}
      />

      <ApplicationTenantPickerDrawer
        application={tenantPickerApplication}
        enteringTenantId={enteringTenantId}
        open={Boolean(tenantPickerApplication)}
        tenants={tenantPickerWorkspaces}
        onClose={() => setTenantPickerApplication(null)}
        onSelect={(workspace) => {
          if (tenantPickerApplication) {
            void handleEnterApplication(tenantPickerApplication, workspace.tenantId);
          }
        }}
      />

      <ResponsiveModal
        open={appTypeModalOpen}
        onClose={() => setAppTypeModalOpen(false)}
        title={translate('page.platformApplications.createType.title')}
        footer={
          <>
            <button
              className="rounded border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
              type="button"
              onClick={() => setAppTypeModalOpen(false)}
            >
              {translate('common.cancel')}
            </button>
            <button
              className="rounded bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 transition-colors flex items-center gap-1.5"
              type="button"
              onClick={handleNext}
            >
              {translate('common.next') || '下一步'}
              <AppIcon name="arrowRight" size={14} />
            </button>
          </>
        }
      >
        <div className="flex flex-col gap-4 py-2">
          <div className="text-sm text-gray-500">
            {translate('page.platformApplications.createType.description')}
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-2">
            <div
              className={`relative flex flex-col items-center text-center p-6 border rounded-xl cursor-pointer transition-all ${
                selectedAppType === 'Business'
                  ? 'border-primary-500 ring-2 ring-primary-500 bg-primary-50/5'
                  : 'border-gray-200 hover:border-gray-300 bg-white'
              }`}
              onClick={() => setSelectedAppType('Business')}
            >
              {selectedAppType === 'Business' && (
                <div className="absolute top-3 right-3 w-5 h-5 rounded-full border-2 border-primary-500 bg-primary-500 flex items-center justify-center">
                  <div className="w-1.5 h-1.5 bg-white rounded-full" />
                </div>
              )}
              <div className={`w-16 h-16 rounded-2xl flex items-center justify-center mb-4 transition-colors ${
                selectedAppType === 'Business' ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-500'
              }`}>
                <AppIcon name="module" size={32} />
              </div>
              <h4 className="text-base font-semibold text-gray-900 mb-2">
                {translate('page.platformApplications.createType.ordinary.title')}
              </h4>
              <p className="text-xs text-gray-500 leading-relaxed max-w-[240px]">
                {translate('page.platformApplications.createType.ordinary.desc')}
              </p>
            </div>

            <div
              className={`relative flex flex-col items-center text-center p-6 border rounded-xl cursor-pointer transition-all ${
                selectedAppType === 'LowCode'
                  ? 'border-primary-500 ring-2 ring-primary-500 bg-primary-50/5'
                  : 'border-gray-200 hover:border-gray-300 bg-white'
              }`}
              onClick={() => setSelectedAppType('LowCode')}
            >
              {selectedAppType === 'LowCode' && (
                <div className="absolute top-3 right-3 w-5 h-5 rounded-full border-2 border-primary-500 bg-primary-500 flex items-center justify-center">
                  <div className="w-1.5 h-1.5 bg-white rounded-full" />
                </div>
              )}
              <div className={`w-16 h-16 rounded-2xl flex items-center justify-center mb-4 transition-colors ${
                selectedAppType === 'LowCode' ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-500'
              }`}>
                <AppIcon name="moduleBox" size={32} />
              </div>
              <h4 className="text-base font-semibold text-gray-900 mb-2 flex items-center justify-center">
                {translate('page.platformApplications.createType.lowCode.title')}
                <span className="ml-2 inline-flex items-center rounded bg-emerald-50 px-2 py-0.5 text-[10px] font-medium text-emerald-700 ring-1 ring-emerald-600/10 ring-inset">
                  New
                </span>
              </h4>
              <p className="text-xs text-gray-500 leading-relaxed max-w-[240px]">
                {translate('page.platformApplications.createType.lowCode.desc')}
              </p>
            </div>
          </div>
        </div>
      </ResponsiveModal>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => setPublishModalOpen(false), variant: 'ghost' },
          { label: translate('page.platformApplications.publish.action.publish'), onClick: () => void handlePublish(), type: 'button', variant: 'primary', loading: publishMutation.isPending }
        ]}
        fields={publishFields}
        open={publishModalOpen}
        onClose={() => setPublishModalOpen(false)}
        onValueChange={(name, value) => setPublishForm((current) => ({ ...current, [name]: value }))}
        title={publishTarget ? formatMessage(translate('page.platformApplications.publish.titleWithName'), { appName: publishTarget.appName }) : translate('page.platformApplications.publish.title')}
        value={publishForm}
      >
        {translate('page.platformApplications.publish.description')}
      </ModalForm>

      <ResponsiveModal
        mode="drawer"
        open={publishPanelOpen}
        title={publishTarget ? formatMessage(translate('page.platformApplications.publish.panelTitleWithName'), { appName: publishTarget.appName }) : translate('page.platformApplications.publish.panelTitle')}
        description={publishTarget ? formatMessage(translate('page.platformApplications.publish.panelDescription'), { appCode: publishTarget.appCode }) : undefined}
        onClose={closePublishPanel}
      >
        <div className="flex h-full min-h-0 flex-col gap-4 text-sm">
          <PublishSummary task={activeTask} translate={translate} />
          <div className="flex flex-wrap items-center gap-2">
            <TabButton active={activeTab === 'tasks'} onClick={() => setActiveTab('tasks')}>{translate('page.platformApplications.publish.tab.tasks')}</TabButton>
            <TabButton active={activeTab === 'logs'} disabled={!selectedTaskId} onClick={() => setActiveTab('logs')}>{translate('page.platformApplications.publish.tab.logs')}</TabButton>
            <TabButton active={activeTab === 'artifacts'} onClick={() => setActiveTab('artifacts')}>{translate('page.platformApplications.publish.tab.artifacts')}</TabButton>
            <button className="ml-auto border border-gray-300 rounded px-3 py-1.5 text-sm hover:bg-gray-50" type="button" onClick={() => {
              void refetchTasks();
              void refetchTaskDetail();
              void refetchLogs();
              void refetchArtifacts();
            }}>
              <AppIcon name="arrows-clockwise" /> {translate('page.platformApplications.publish.refresh')}
            </button>
          </div>

          {activeTab === 'tasks' ? (
            <DataTable
              columnSettingsKey="platform-application-publish-tasks"
              columns={taskColumns}
              emptyText={tasksQuery.isError ? translate('page.platformApplications.publish.taskLoadFailed') : translate('page.platformApplications.publish.taskEmpty')}
              fitScreen
              loading={tasksQuery.isLoading}
              onPageChange={setTaskPageIndex}
              onPageSizeChange={(value) => {
                setTaskPageIndex(1);
                setTaskPageSize(value);
              }}
              pageSizeOptions={[10, 20, 50]}
              pagination={{ current: taskPageIndex, pageSize: taskPageSize, total: tasksQuery.data?.data.total ?? 0 }}
              rowActions={(row) => (
                <TableActions>
                  <PermissionButton code="platform:application:publish-task" className="hover:text-primary-600 transition-colors" title={translate('page.platformApplications.publish.action.selectTask')} type="button" onClick={() => setActiveTaskId(row.id)}>
                    <AppIcon className="text-base" name="target" />
                  </PermissionButton>
                  <PermissionButton code="platform:application:publish-log" className="hover:text-primary-600 transition-colors" title={translate('page.platformApplications.publish.action.viewLog')} type="button" onClick={() => {
                    setActiveTaskId(row.id);
                    setActiveTab('logs');
                  }}>
                    <AppIcon className="text-base" name="file-text" />
                  </PermissionButton>
                  {row.status === 'Succeeded' ? (
                    <PermissionButton code="platform:application:publish" className="hover:text-primary-600 transition-colors" disabled={packageTaskMutation.isPending} title={translate('page.platformApplications.publish.action.repackage')} type="button" onClick={() => void handlePackageTask(row)}>
                      <AppIcon className="text-base" name="package" />
                    </PermissionButton>
                  ) : null}
                </TableActions>
              )}
              rowClassName={(row) => (row.id === selectedTaskId ? 'bg-primary-50/60' : '')}
              rowKey={(row) => row.id}
              rows={taskRows}
              tableQuery={defaultTableQuery}
            />
          ) : null}

          {activeTab === 'logs' ? (
            <DataTable
              columnSettingsKey="platform-application-publish-logs"
              columns={logColumns}
              emptyText={logsQuery.isError ? translate('page.platformApplications.publish.logLoadFailed') : translate('page.platformApplications.publish.logEmpty')}
              fitScreen
              loading={logsQuery.isLoading}
              onPageChange={setLogPageIndex}
              onPageSizeChange={(value) => {
                setLogPageIndex(1);
                setLogPageSize(value);
              }}
              pageSizeOptions={[50, 100, 200]}
              pagination={{ current: logPageIndex, pageSize: logPageSize, total: logsQuery.data?.data.total ?? 0 }}
              rowKey={(row) => row.id}
              rows={logsQuery.data?.data.items ?? []}
              tableQuery={defaultTableQuery}
            />
          ) : null}

          {activeTab === 'artifacts' ? (
            <DataTable
              columnSettingsKey="platform-application-publish-artifacts"
              columns={artifactColumns}
              emptyText={artifactsQuery.isError ? translate('page.platformApplications.publish.artifactLoadFailed') : translate('page.platformApplications.publish.artifactEmpty')}
              fitScreen
              loading={artifactsQuery.isLoading}
              onPageChange={setArtifactPageIndex}
              onPageSizeChange={(value) => {
                setArtifactPageIndex(1);
                setArtifactPageSize(value);
              }}
              pageSizeOptions={[10, 20, 50]}
              pagination={{ current: artifactPageIndex, pageSize: artifactPageSize, total: artifactsQuery.data?.data.total ?? 0 }}
              rowActions={(row) => (
                <TableActions>
                  <PermissionButton code="platform:application:publish-artifact-download" className="hover:text-primary-600 transition-colors" title={translate('page.platformApplications.publish.action.download')} type="button" onClick={() => void handleDownloadArtifact(row)}>
                    <AppIcon className="text-base" name="download-simple" />
                  </PermissionButton>
                  <PermissionButton code="platform:application:publish-artifact-delete" className="hover:text-red-600 transition-colors" title={translate('page.platformApplications.publish.action.delete')} type="button" onClick={() => handleDeleteArtifact(row)}>
                    <AppIcon className="text-base" name="trash" />
                  </PermissionButton>
                </TableActions>
              )}
              rowKey={(row) => row.id}
              rows={artifactsQuery.data?.data.items ?? []}
              tableQuery={defaultTableQuery}
            />
          ) : null}
        </div>
      </ResponsiveModal>
    </>
  );
}

function mapPublishFormToRequest(form: PublishFormState): ApplicationPublishRequest {
  return {
    backendHost: trimToNull(form.backendHost),
    backendPort: typeof form.backendPort === 'number' ? form.backendPort : null,
    cleanOutput: form.cleanOutput,
    frontendApiBaseUrl: trimToNull(form.frontendApiBaseUrl),
    frontendBasePath: trimToNull(form.frontendBasePath),
    includeBackend: form.includeBackend,
    includeFrontend: form.includeFrontend,
    remark: trimToNull(form.remark),
    tenantId: trimToNull(form.tenantId),
    version: trimToNull(form.version)
  };
}

function PublishSummary({ task, translate }: { task: ApplicationPublishTaskDto | null; translate: (key: string) => string }) {
  if (!task) {
    return (
      <div className="rounded border border-gray-200 bg-white px-4 py-3 text-gray-500">
        {translate('page.platformApplications.publish.summaryHint')}
      </div>
    );
  }

  return (
    <div className="rounded border border-gray-200 bg-white px-4 py-3">
      <PublishProgressOverview task={task} translate={translate} />
      <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-4">
        <Metric label={translate('page.platformApplications.publish.summary.taskStatus')} value={<PublishStatusBadge status={task.status} translate={translate} />} />
        <Metric label={translate('page.platformApplications.publish.summary.currentStage')} value={task.stage || '-'} />
        <Metric label={translate('page.platformApplications.publish.summary.progress')} value={<ProgressCell value={task.progressPercent} />} />
        <Metric label={translate('page.platformApplications.publish.summary.duration')} value={formatDuration(task.durationMs)} />
        <Metric label={translate('page.platformApplications.publish.summary.backendAddress')} value={`${task.backendHost}:${task.backendPort}`} />
        <Metric label={translate('page.platformApplications.publish.summary.frontendPath')} value={task.frontendBasePath || '-'} />
        <Metric label={translate('page.platformApplications.publish.summary.apiAddress')} value={task.frontendApiBaseUrl || '-'} />
        <Metric label={translate('page.platformApplications.publish.summary.sourceDir')} value={task.sourceProjectPath || '-'} wide />
        <Metric label={translate('page.platformApplications.publish.summary.releaseDir')} value={task.releasePath || '-'} wide />
        <Metric label={translate('page.platformApplications.publish.summary.errorMessage')} value={task.errorMessage || '-'} wide />
        <Metric label={translate('page.platformApplications.publish.summary.traceId')} value={<span className="font-mono text-xs">{task.traceId}</span>} wide />
      </div>
    </div>
  );
}

function PublishProgressOverview({ task, translate }: { task: ApplicationPublishTaskDto; translate: (key: string) => string }) {
  const progress = clampProgress(task.progressPercent);
  const running = isRunningStatus(task.status);
  const progressColor =
    task.status === 'Succeeded'
      ? 'bg-emerald-500'
      : task.status === 'Failed'
        ? 'bg-red-500'
        : task.status === 'Blocked'
          ? 'bg-amber-500'
          : 'bg-primary-500';

  return (
    <div className="min-w-0">
      <div className="mb-2 flex items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="truncate text-sm font-medium text-gray-900">{task.stage || translate('page.platformApplications.publish.status.queued')}</div>
          <div className="mt-0.5 text-xs text-gray-500">{running ? translate('page.platformApplications.publish.progress.running') : translate('page.platformApplications.publish.progress.finished')}</div>
        </div>
        <div className="shrink-0 text-right">
          <div className="text-lg font-semibold text-gray-900">{progress}%</div>
          <PublishStatusBadge status={task.status} translate={translate} />
        </div>
      </div>
      <div
        aria-label={translate('page.platformApplications.publish.progress.ariaLabel')}
        aria-valuemax={100}
        aria-valuemin={0}
        aria-valuenow={progress}
        className="h-3 overflow-hidden rounded bg-gray-100"
        role="progressbar"
      >
        <div className={`h-full rounded ${progressColor} ${running ? 'transition-all duration-500' : ''}`} style={{ width: `${progress}%` }} />
      </div>
    </div>
  );
}

function Metric({ label, value, wide = false }: { label: string; value: ReactNode; wide?: boolean }) {
  return (
    <div className={wide ? 'md:col-span-2 min-w-0' : 'min-w-0'}>
      <div className="text-xs text-gray-500">{label}</div>
      <div className="mt-1 truncate text-sm text-gray-900" title={typeof value === 'string' ? value : undefined}>{value}</div>
    </div>
  );
}

function TabButton({ active, children, disabled = false, onClick }: { active: boolean; children: ReactNode; disabled?: boolean; onClick: () => void }) {
  return (
    <button
      className={`rounded border px-3 py-1.5 text-sm transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${active ? 'border-primary-500 bg-primary-50 text-primary-700' : 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50'}`}
      disabled={disabled}
      type="button"
      onClick={onClick}
    >
      {children}
    </button>
  );
}

function PublishStatusBadge({ status, translate }: { status: string; translate: (key: string) => string }) {
  const normalized = status.toLowerCase();
  const className =
    normalized === 'succeeded'
      ? 'bg-emerald-50 text-emerald-700'
      : normalized === 'failed'
        ? 'bg-red-50 text-red-700'
      : normalized === 'blocked'
        ? 'bg-amber-50 text-amber-700'
        : 'bg-blue-50 text-blue-700';
  const text = status === 'Succeeded'
    ? translate('page.platformApplications.publish.status.succeeded')
    : status === 'Failed'
      ? translate('page.platformApplications.publish.status.failed')
      : status === 'Blocked'
        ? translate('page.platformApplications.publish.status.blocked')
        : status === 'Running'
          ? translate('page.platformApplications.publish.status.running')
          : status === 'Pending'
            ? translate('page.platformApplications.publish.status.pending')
            : status;
  return <span className={`inline-flex rounded px-2 py-0.5 text-xs font-medium ${className}`}>{text}</span>;
}

function LogLevelBadge({ level }: { level: string }) {
  const normalized = level.toLowerCase();
  const className = normalized === 'error' ? 'bg-red-50 text-red-700' : normalized === 'warn' || normalized === 'warning' ? 'bg-amber-50 text-amber-700' : 'bg-gray-100 text-gray-700';
  return <span className={`inline-flex rounded px-2 py-0.5 text-xs font-medium ${className}`}>{level}</span>;
}

function ProgressCell({ value }: { value: number }) {
  const safeValue = clampProgress(value);
  return (
    <div className="flex min-w-[96px] items-center gap-2">
      <div className="h-1.5 flex-1 overflow-hidden rounded bg-gray-100">
        <div className="h-full bg-primary-500" style={{ width: `${safeValue}%` }} />
      </div>
      <span className="w-9 text-right text-xs text-gray-600">{safeValue}%</span>
    </div>
  );
}

function clampProgress(value: number) {
  return Math.min(100, Math.max(0, Number.isFinite(value) ? Math.round(value) : 0));
}

function isRunningStatus(status: string) {
  return status === 'Pending' || status === 'Running';
}

function formatDateTime(value?: string | null) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

function formatDuration(value?: number | null) {
  if (!value || value <= 0) {
    return '-';
  }

  if (value < 1000) {
    return `${value} ms`;
  }

  return `${(value / 1000).toFixed(1)} s`;
}

function formatBytes(value: number) {
  if (!Number.isFinite(value) || value <= 0) {
    return '0 B';
  }

  const units = ['B', 'KB', 'MB', 'GB'];
  let size = value;
  let unitIndex = 0;
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024;
    unitIndex += 1;
  }

  return `${size.toFixed(unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`;
}

function trimToNull(value: string) {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
