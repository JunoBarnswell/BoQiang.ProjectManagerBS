import { Link } from 'react-router-dom';

import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../../shared/icons/AppIcon';

import { applicationConsoleNavItems } from './applicationConsoleCatalog';
import { ApplicationConsolePageFrame } from './ApplicationConsolePageFrame';

export function ApplicationHomePage() {
  return (
    <ApplicationConsolePageFrame pageKey="home">
      {({ summary }) => (
        <div className="space-y-4">
          <div className="rounded-md border border-gray-200 bg-white p-4 shadow-sm">
            <div className="mb-3 font-semibold text-gray-950">{translateCurrentLiteral("快捷入口")}</div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {applicationConsoleNavItems
                .filter((item) => item.key !== 'home')
                .map((item) => {
                  const appAdminPrefix = `/tenants/${encodeURIComponent(summary.application.tenantId)}/apps/${summary.application.appCode.toUpperCase()}/admin`;
                  return (
                    <Link
                      key={item.key}
                      to={`${appAdminPrefix}/${item.slug}`}
                      className="flex items-start gap-3 rounded-md border border-gray-200 p-3 hover:border-primary-200 hover:bg-primary-50"
                    >
                      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-white text-primary-500 shadow-sm">
                        <AppIcon className="h-5 w-5" name={item.icon} />
                      </span>
                      <span className="min-w-0">
                        <span className="block font-medium text-gray-950">{item.title}</span>
                        <span className="mt-1 block text-sm leading-5 text-gray-600">{item.description}</span>
                      </span>
                    </Link>
                  );
                })}
            </div>
          </div>
        </div>
      )}
    </ApplicationConsolePageFrame>
  );
}
