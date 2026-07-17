import type {
  WorkflowMessageTemplateDto,
  WorkflowMessageTemplateUpsertRequest,
  WorkflowNodeNotificationRuleDto,
  WorkflowNotificationChannelDto,
  WorkflowNotificationChannelUpsertRequest
} from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import { AppIcon } from '../../shared/icons/AppIcon';
import { TableActions } from '../../shared/table/TableActions';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import type { ConfigModal, TranslateFn, WorkflowNodeNotificationRuleForm } from './workflowNotificationsTypes';

export function renderChannelActions(
  row: WorkflowNotificationChannelDto,
  setForm: (value: WorkflowNotificationChannelUpsertRequest) => void,
  setModal: (value: ConfigModal) => void,
  confirm: ReturnType<typeof useConfirm>,
  deleteItem: (id: string) => Promise<unknown>,
  refresh: () => Promise<void>,
  message: ReturnType<typeof useMessage>,
  translate: TranslateFn
) {
  return (
    <ConfigActions
      deletePermission="workflow:notification:channel:delete"
      deleteTitle={formatMessage(translate('page.workflowNotifications.action.deleteItem'), { name: row.channelName })}
      editPermission="workflow:notification:channel:edit"
      editTitle={formatMessage(translate('page.workflowNotifications.action.editItem'), { name: row.channelName })}
      onDelete={() => deleteWithConfirm(row.id, row.channelName, confirm, deleteItem, refresh, message, translate)}
      onEdit={() => {
        setForm({ ...row });
        setModal('channel');
      }}
    />
  );
}

export function renderTemplateActions(
  row: WorkflowMessageTemplateDto,
  setForm: (value: WorkflowMessageTemplateUpsertRequest) => void,
  setModal: (value: ConfigModal) => void,
  confirm: ReturnType<typeof useConfirm>,
  deleteItem: (id: string) => Promise<unknown>,
  refresh: () => Promise<void>,
  message: ReturnType<typeof useMessage>,
  translate: TranslateFn
) {
  return (
    <ConfigActions
      deletePermission="workflow:notification:template:delete"
      deleteTitle={formatMessage(translate('page.workflowNotifications.action.deleteItem'), { name: row.templateName })}
      editPermission="workflow:notification:template:edit"
      editTitle={formatMessage(translate('page.workflowNotifications.action.editItem'), { name: row.templateName })}
      onDelete={() => deleteWithConfirm(row.id, row.templateName, confirm, deleteItem, refresh, message, translate)}
      onEdit={() => {
        setForm({ ...row });
        setModal('template');
      }}
    />
  );
}

export function renderRuleActions(
  row: WorkflowNodeNotificationRuleDto,
  setForm: (value: WorkflowNodeNotificationRuleForm) => void,
  setModal: (value: ConfigModal) => void,
  confirm: ReturnType<typeof useConfirm>,
  deleteItem: (id: string) => Promise<unknown>,
  refresh: () => Promise<void>,
  message: ReturnType<typeof useMessage>,
  translate: TranslateFn
) {
  return (
    <ConfigActions
      deletePermission="workflow:notification:rule:delete"
      deleteTitle={formatMessage(translate('page.workflowNotifications.action.deleteItem'), { name: row.nodeId })}
      editPermission="workflow:notification:rule:edit"
      editTitle={formatMessage(translate('page.workflowNotifications.action.editItem'), { name: row.nodeId })}
      onDelete={() => deleteWithConfirm(row.id, row.nodeId, confirm, deleteItem, refresh, message, translate)}
      onEdit={() => {
        setForm({ ...row, channelCodesText: JSON.stringify(row.channelCodes) });
        setModal('rule');
      }}
    />
  );
}

function ConfigActions({ deletePermission, deleteTitle, editPermission, editTitle, onDelete, onEdit }: {
  deletePermission: string;
  deleteTitle: string;
  editPermission: string;
  editTitle: string;
  onDelete: () => void;
  onEdit: () => void;
}) {
  return (
    <TableActions>
      <PermissionButton code={editPermission} className="hover:text-primary-600" title={editTitle} type="button" onClick={onEdit}><AppIcon className="text-base" name="pencil-simple" /></PermissionButton>
      <PermissionButton code={deletePermission} className="hover:text-red-600" title={deleteTitle} type="button" onClick={onDelete}><AppIcon className="text-base" name="trash" /></PermissionButton>
    </TableActions>
  );
}

function deleteWithConfirm(
  id: string,
  label: string,
  confirm: ReturnType<typeof useConfirm>,
  deleteItem: (id: string) => Promise<unknown>,
  refresh: () => Promise<void>,
  message: ReturnType<typeof useMessage>,
  translate: TranslateFn
) {
  confirm({
    confirmText: translate('common.delete'),
    content: formatMessage(translate('page.workflowNotifications.confirm.deleteContent'), { name: label }),
    onConfirm: async () => {
      try {
        await deleteItem(id);
        await refresh();
        message.success(translate('page.workflowNotifications.success.delete'));
      } catch (error) {
        message.error(getErrorMessage(error, translate('page.workflowNotifications.error.deleteFailed')));
      }
    },
    title: translate('page.workflowNotifications.confirm.deleteTitle')
  });
}
