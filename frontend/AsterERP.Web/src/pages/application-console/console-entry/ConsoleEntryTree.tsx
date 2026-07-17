import { Link } from 'react-router-dom';

import type {
  ApplicationConsoleEntryTreeGroupDto,
  ApplicationConsoleEntryTreeItemDto
} from '../../../api/application-console/applicationConsole.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';
import type { ApplicationConsoleRecentVisitInput } from '../applicationConsoleRecentVisits';

interface ConsoleEntryTreeProps {
  activePath: string;
  buildVisitState: (item: ApplicationConsoleEntryTreeItemDto) => { recentVisit: ApplicationConsoleRecentVisitInput };
  groups: ApplicationConsoleEntryTreeGroupDto[];
}

export function ConsoleEntryTree({ activePath, buildVisitState, groups }: ConsoleEntryTreeProps) {
  return (
    <aside className="rounded-xl border border-slate-200 bg-white shadow-sm">
      <div className="border-b border-slate-200 px-4 py-3">
        <div className="text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-500">CodeWave Entry</div>
        <h2 className="mt-1 text-sm font-semibold text-slate-950">{translateCurrentLiteral("开发入口树")}</h2>
      </div>
      <div className="space-y-4 p-3">
        {groups.map((group) => (
          <section key={group.key} className="space-y-2">
            <div className="px-1">
              <div className="flex items-center gap-2 text-xs font-semibold text-slate-700">
                <AppIcon className="h-4 w-4 text-slate-400" name={normalizeIconName(group.icon)} />
                <span>{group.title}</span>
              </div>
              <div className="mt-1 text-[11px] leading-5 text-slate-500">{group.description}</div>
            </div>
            <div className="space-y-1.5">
              {group.items.map((item) => {
                const active = activePath.startsWith(item.routePath);
                return (
                  <Link
                    key={item.key}
                    state={buildVisitState(item)}
                    to={item.routePath}
                    className={[
                      'block rounded-lg border px-3 py-2.5 transition',
                      active
                        ? 'border-primary-300 bg-primary-50 shadow-sm'
                        : 'border-slate-200 bg-slate-50/70 hover:border-primary-200 hover:bg-white'
                    ].join(' ')}
                  >
                    <div className="flex items-start gap-3">
                      <span className={`mt-0.5 inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${resolveAccentClass(item.accent)}`}>
                        <AppIcon className="h-4.5 w-4.5" name={normalizeIconName(item.icon)} />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="flex flex-wrap items-center gap-2">
                          <span className="truncate text-sm font-semibold text-slate-900">{item.title}</span>
                          {typeof item.count === 'number' ? (
                            <span className="rounded-full bg-white px-2 py-0.5 text-[11px] font-medium text-slate-600 shadow-sm">
                              {item.count}
                              {item.countLabel ? item.countLabel : ''}
                            </span>
                          ) : null}
                        </span>
                        <span className="mt-1 block text-xs leading-5 text-slate-500">{item.description}</span>
                        {item.recentTargetTitle ? (
                          <span className="mt-1 block truncate text-[11px] font-medium text-primary-700">
                            最近对象：{item.recentTargetTitle}
                          </span>
                        ) : null}
                      </span>
                    </div>
                  </Link>
                );
              })}
            </div>
          </section>
        ))}
      </div>
    </aside>
  );
}

function normalizeIconName(value: string) {
  return value as Parameters<typeof AppIcon>[0]['name'];
}

function resolveAccentClass(accent: string) {
  switch (accent) {
    case 'emerald':
      return 'bg-emerald-50 text-emerald-700';
    case 'purple':
      return 'bg-violet-50 text-violet-700';
    case 'blue':
      return 'bg-sky-50 text-sky-700';
    default:
      return 'bg-slate-100 text-slate-700';
  }
}
