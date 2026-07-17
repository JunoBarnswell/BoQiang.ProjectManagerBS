import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { WorkflowBindingDto, WorkflowHistoricProcessDto, WorkflowStartInstanceRequest } from '../../api/workflow/workflows.api';
import { getWorkflowBindings, getWorkflowHistoryProcesses, startWorkflowInstance } from '../../api/workflow/workflows.api';
import { translateCurrentLocale, useI18n } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { AppIcon } from '../../shared/icons/AppIcon';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { buildWorkspaceRoute } from './workflowWorkspaceRoutes';

interface WorkflowBusinessActionsProps {
  binding?: WorkflowBindingDto | null;
  businessKey: string;
  businessType: string;
  menuCode: string;
  title: string;
  variables?: Record<string, unknown>;
}

interface WorkflowStartFormState {
  comment: string;
  title: string;
  variablesJson: string;
}

function parseVariables(json: string, comment: string): Record<string, unknown> {
  const trimmed = json.trim();
  const variables = trimmed ? JSON.parse(trimmed) as Record<string, unknown> : {};
  return comment.trim() ? { ...variables, startComment: comment.trim() } : variables;
}

export function WorkflowBusinessActions({ binding: providedBinding, businessKey, businessType, menuCode, title, variables: initialVariables }: WorkflowBusinessActionsProps) {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const message = useMessage();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const [open, setOpen] = useState(false);
  const [formState, setFormState] = useState<WorkflowStartFormState>({
    comment: '',
    title,
    variablesJson: ''
  });
  const startMutation = useApiMutation({ mutationFn: startWorkflowInstance });
  const bindingQuery = useApiQuery({
    enabled: open && !providedBinding && Boolean(workspace?.tenantId && workspace.appCode),
    queryFn: ({ signal }) => getWorkflowBindings({
      appCode: workspace?.appCode,
      keyword: businessType,
      pageIndex: 1,
      pageSize: 20,
      tenantId: workspace?.tenantId
    }, signal),
    queryKey: ['workflows', 'business-action', 'bindings', workspace?.tenantId, workspace?.appCode, menuCode, businessType]
  });
  const historyQuery = useApiQuery({
    enabled: open,
    queryFn: ({ signal }) => getWorkflowHistoryProcesses({ keyword: businessKey, pageIndex: 1, pageSize: 3 }, signal),
    queryKey: ['workflows', 'business-action', 'history', businessKey]
  });
  const binding = providedBinding ?? resolveBinding(bindingQuery.data?.data.items ?? [], menuCode, businessType);
  const recentHistory = (historyQuery.data?.data.items ?? []).filter((item) => item.businessKey === businessKey).slice(0, 3);
  const unavailableReason = resolveUnavailableReason(workspace, binding);
  const startFields: FormFieldConfig<WorkflowStartFormState>[] = [
    { label: translate('workflow.business.approvalTitle'), name: 'title', placeholder: translate('workflow.business.enterApprovalTitle'), required: true, span: 2, type: 'text' },
    { label: translate('workflow.business.startDescription'), name: 'comment', placeholder: translate('workflow.business.enterStartDescription'), rows: 3, span: 2, type: 'textarea' },
    { label: translate('workflow.business.variablesJson'), name: 'variablesJson', placeholder: '{"amount":1000}', rows: 4, span: 2, type: 'textarea' }
  ];

  const openStart = () => {
    setFormState({ comment: '', title, variablesJson: initialVariables ? JSON.stringify(initialVariables, null, 2) : '' });
    setOpen(true);
  };

  const handleStart = async () => {
    if (!workspace?.tenantId || !workspace.appCode) {
      message.error(translate('workflow.business.noWorkspace'));
      return;
    }

    if (!binding) {
      message.error(translate('workflow.business.noBinding'));
      return;
    }

    if (!formState.title.trim()) {
      message.error(translate('workflow.business.enterApprovalTitle'));
      return;
    }

    let variables: Record<string, unknown>;
    try {
      variables = parseVariables(formState.variablesJson, formState.comment);
    } catch {
      message.error(translate('workflow.business.invalidJson'));
      return;
    }

    const request: WorkflowStartInstanceRequest = {
      appCode: workspace.appCode,
      businessKey,
      businessType,
      menuCode,
      tenantId: workspace.tenantId,
      title: formState.title.trim(),
      variables
    };

    try {
      const response = await startMutation.mutateAsync(request);
      setOpen(false);
      message.success(translate('workflow.business.started'));
      navigate(buildWorkspaceRoute(`/workflows/instances/${response.data.processInstanceId}`, workspace));
    } catch (error) {
      message.error(getErrorMessage(error, translate('workflow.business.startFailed')));
    }
  };

  return (
    <>
      <PermissionButton className="hover:text-primary-600" code="workflow:instance:start" title={translate('workflow.business.startApproval')} type="button" onClick={openStart}>
        <AppIcon className="text-base" name="play-circle" />
      </PermissionButton>
      <PermissionButton className="hover:text-primary-600" code="workflow:history:query" title={translate('workflow.business.history')} type="button" onClick={() => navigate(buildWorkspaceRoute(`/workflows/history?businessKey=${encodeURIComponent(businessKey)}`, workspace))}>
        <AppIcon className="text-base" name="clock-counter-clockwise" />
      </PermissionButton>

      <ModalForm
        actions={[
          { label: translate('workflow.drawer.cancel'), onClick: () => setOpen(false), variant: 'ghost' },
          { disabled: Boolean(unavailableReason), label: translate('workflow.business.startButton'), loading: startMutation.isPending, onClick: () => void handleStart(), type: 'button', variant: 'primary' }
        ]}
        fields={startFields}
        open={open}
        onClose={() => setOpen(false)}
        onValueChange={(name, value) => setFormState((current) => ({ ...current, [name]: String(value ?? '') }))}
        title={translate('workflow.business.startApproval')}
        value={formState}
      >
        <div className="grid gap-3 text-sm">
          <div className="grid grid-cols-2 gap-2">
            <InfoPill label={translate('workflow.business.businessType')} value={businessType} />
            <InfoPill label={translate('workflow.business.businessKey')} value={businessKey} />
            <InfoPill label={translate('workflow.business.bindingProcess')} value={binding?.processDefinitionKey ?? '-'} />
            <InfoPill label={translate('workflow.business.currentStatus')} value={recentHistory[0]?.status ?? '-'} />
          </div>
          {unavailableReason ? <div className="border border-amber-200 bg-amber-50 text-amber-700 rounded px-3 py-2">{unavailableReason}</div> : null}
          <RecentHistoryList histories={recentHistory} />
        </div>
      </ModalForm>
    </>
  );
}

function resolveBinding(bindings: WorkflowBindingDto[], menuCode: string, businessType: string) {
  return bindings.find((item) => item.isEnabled && item.menuCode === menuCode && item.businessType === businessType);
}

function resolveUnavailableReason(workspace: { appCode?: string | null; tenantId?: string | null } | null | undefined, binding?: WorkflowBindingDto) {
  if (!workspace?.tenantId || !workspace.appCode) {
    return translateCurrentLocale('workflow.business.noWorkspace');
  }

  if (!binding) {
    return translateCurrentLocale('workflow.business.unavailable');
  }

  return null;
}

function InfoPill({ label, value }: { label: string; value: string }) {
  return (
    <div className="border border-gray-200 rounded px-3 py-2">
      <div className="text-xs text-gray-500">{label}</div>
      <div className="mt-1 text-gray-900 break-all">{value}</div>
    </div>
  );
}

function RecentHistoryList({ histories }: { histories: WorkflowHistoricProcessDto[] }) {
  if (histories.length === 0) {
    return <div className="text-gray-500">{translateCurrentLocale('workflow.business.noRecentHistory')}</div>;
  }

  return (
    <div className="grid gap-2">
      {histories.map((item) => (
        <div key={item.id} className="border border-gray-100 rounded px-3 py-2">
          <div className="text-gray-900">{item.processName ?? item.processDefinitionId ?? item.id}</div>
          <div className="text-xs text-gray-500">{item.status ?? '-'} · {item.startTime ? new Date(item.startTime).toLocaleString() : '-'}</div>
        </div>
      ))}
    </div>
  );
}
