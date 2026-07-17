import type { ApplicationListItemDto } from '../../api/platform/platform.types';
import { useI18n } from '../../core/i18n/I18nProvider';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { AppIcon } from '../../shared/icons/AppIcon';

interface ApplicationQuickActionsPanelProps {
  application: ApplicationListItemDto | null;
  entering?: boolean;
  onClose: () => void;
  onEdit: (application: ApplicationListItemDto) => void;
  onEnter: (application: ApplicationListItemDto) => void;
  onPublish: (application: ApplicationListItemDto) => void;
  onPublishRecords: (application: ApplicationListItemDto) => void;
}

export function ApplicationQuickActionsPanel({
  application,
  entering,
  onClose,
  onEdit,
  onEnter,
  onPublish,
  onPublishRecords
}: ApplicationQuickActionsPanelProps) {
  const { translate } = useI18n();

  if (!application) {
    return null;
  }

  const isEnabled = application.status === 'Enabled';

  return (
    <aside className="absolute bottom-4 right-4 top-4 z-20 flex w-[320px] flex-col rounded border border-gray-200 bg-white shadow-xl">
      <div className="flex items-start justify-between gap-3 border-b border-gray-100 px-4 py-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="inline-flex h-8 w-8 items-center justify-center rounded bg-emerald-600 text-xs font-semibold text-white">
              {application.appCode.slice(0, 3)}
            </span>
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-gray-900">{application.appName}</div>
              <div className="truncate text-xs text-gray-500">{application.appCode}</div>
            </div>
          </div>
        </div>
        <button className="text-gray-400 hover:text-gray-700" type="button" onClick={onClose}>
          <AppIcon name="x" />
        </button>
      </div>

      <div className="flex flex-1 flex-col gap-2 overflow-y-auto p-3 text-sm">
        <PermissionButton
          code="platform:application:enter"
          className="flex items-center gap-3 rounded border border-emerald-500 bg-emerald-50 px-3 py-3 text-left text-emerald-800 transition-colors hover:bg-emerald-100 disabled:cursor-not-allowed disabled:opacity-60"
          disabled={!isEnabled || entering}
          type="button"
          onClick={() => onEnter(application)}
        >
          <AppIcon name={entering ? 'refresh' : 'rocket'} className={entering ? 'animate-spin' : ''} />
          <span className="min-w-0">
            <span className="block font-medium">{translate('page.platformApplications.quick.enter')}</span>
            <span className="block text-xs text-emerald-700">{translate('page.platformApplications.quick.enterDesc')}</span>
          </span>
        </PermissionButton>

        <ActionButton icon="info" title={translate('page.platformApplications.quick.info')} description={application.remark || application.defaultRoutePath || '-'} />
        <ActionButton icon="pencil-simple" title={translate('page.platformApplications.quick.edit')} description={translate('page.platformApplications.quick.editDesc')} onClick={() => onEdit(application)} />
        <ActionButton icon="rocket" title={translate('page.platformApplications.quick.publish')} description={translate('page.platformApplications.quick.publishDesc')} onClick={() => onPublish(application)} />
        <ActionButton icon="sidebar-simple" title={translate('page.platformApplications.quick.publishRecords')} description={translate('page.platformApplications.quick.publishRecordsDesc')} onClick={() => onPublishRecords(application)} />
      </div>
    </aside>
  );
}

function ActionButton({
  description,
  icon,
  onClick,
  title
}: {
  description: string;
  icon: string;
  onClick?: () => void;
  title: string;
}) {
  return (
    <button
      className="flex items-start gap-3 rounded border border-gray-200 bg-white px-3 py-3 text-left transition-colors hover:border-primary-200 hover:bg-primary-50/40 disabled:cursor-default"
      disabled={!onClick}
      type="button"
      onClick={onClick}
    >
      <AppIcon className="mt-0.5 text-gray-500" name={icon} />
      <span className="min-w-0">
        <span className="block font-medium text-gray-900">{title}</span>
        <span className="line-clamp-2 text-xs text-gray-500">{description}</span>
      </span>
    </button>
  );
}
