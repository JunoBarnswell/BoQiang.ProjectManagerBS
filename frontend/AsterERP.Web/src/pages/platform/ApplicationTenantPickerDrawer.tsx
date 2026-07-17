import type { WorkspaceDto } from '../../api/platform/auth.types';
import type { ApplicationListItemDto } from '../../api/platform/platform.types';
import { useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../../shared/icons/AppIcon';
import { ResponsiveModal } from '../../shared/responsive/ResponsiveModal';

interface ApplicationTenantPickerDrawerProps {
  application: ApplicationListItemDto | null;
  enteringTenantId?: string | null;
  open: boolean;
  tenants: WorkspaceDto[];
  onClose: () => void;
  onSelect: (workspace: WorkspaceDto) => void;
}

export function ApplicationTenantPickerDrawer({
  application,
  enteringTenantId,
  open,
  tenants,
  onClose,
  onSelect
}: ApplicationTenantPickerDrawerProps) {
  const { translate } = useI18n();

  return (
    <ResponsiveModal
      mode="drawer"
      open={open}
      title={application ? `${application.appName} / ${translate('page.platformApplications.tenantPicker.title')}` : translate('page.platformApplications.tenantPicker.title')}
      description={translate('page.platformApplications.tenantPicker.description')}
      onClose={onClose}
    >
      <div className="flex flex-col gap-2">
        {tenants.map((tenant) => (
          <button
            className="flex items-center justify-between gap-3 rounded border border-gray-200 bg-white px-4 py-3 text-left transition-colors hover:border-primary-300 hover:bg-primary-50 disabled:cursor-wait disabled:opacity-60"
            disabled={Boolean(enteringTenantId)}
            key={tenant.workspaceId}
            type="button"
            onClick={() => onSelect(tenant)}
          >
            <span className="min-w-0">
              <span className="block text-sm font-medium text-gray-900">{tenant.systemName || tenant.appName}</span>
              <span className="block truncate text-xs text-gray-500">{tenant.tenantName} / {tenant.appCode}</span>
            </span>
            {enteringTenantId === tenant.tenantId ? <AppIcon className="animate-spin text-primary-600" name="refresh" /> : <AppIcon className="text-gray-400" name="arrow-right" />}
          </button>
        ))}
      </div>
    </ResponsiveModal>
  );
}
