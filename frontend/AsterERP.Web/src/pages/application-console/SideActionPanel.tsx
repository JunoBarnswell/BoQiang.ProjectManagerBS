import { Link } from 'react-router-dom';

import { AppIcon } from '../../shared/icons/AppIcon';

import { applicationConsoleNavItems } from './applicationConsoleCatalog';

interface SideActionPanelProps {
  appCode: string;
  tenantId: string;
  title: string;
}

export function SideActionPanel({ appCode, tenantId, title }: SideActionPanelProps) {
  const normalizedAppCode = appCode.toUpperCase();
  const appAdminPrefix = `/tenants/${encodeURIComponent(tenantId)}/apps/${normalizedAppCode}/admin`;

  return (
    <div className="rounded-md border border-gray-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex items-center gap-2 border-b border-gray-100 pb-3">
        <AppIcon className="h-4 w-4 text-primary-500" name="activity" />
        <div className="font-semibold text-gray-950">{title}</div>
      </div>
      <div className="space-y-1">
        {applicationConsoleNavItems
          .filter((item) => item.key !== 'home')
          .map((item) => (
            <Link
              key={item.key}
              to={`${appAdminPrefix}/${item.slug}`}
              className="flex items-center justify-between rounded-md px-2 py-2 text-sm text-gray-700 hover:bg-gray-50 hover:text-primary-600"
            >
              <span className="flex min-w-0 items-center gap-2">
                <AppIcon className="h-4 w-4 shrink-0 text-gray-400" name={item.icon} />
                <span className="truncate">{item.title}</span>
              </span>
              <AppIcon className="h-4 w-4 shrink-0 text-gray-300" name="caret-right" />
            </Link>
          ))}
      </div>
    </div>
  );
}
