import { Link } from 'react-router-dom';

import type { ApplicationConsoleDevelopmentShortcutDto } from '../../../api/application-console/applicationConsole.types';
import { AppIcon } from '../../../shared/icons/AppIcon';
import type { ApplicationConsoleRecentVisitInput } from '../applicationConsoleRecentVisits';

interface DevelopmentShortcutCardProps {
  buildVisitState: (shortcut: ApplicationConsoleDevelopmentShortcutDto) => { recentVisit: ApplicationConsoleRecentVisitInput };
  shortcut: ApplicationConsoleDevelopmentShortcutDto;
}

export function DevelopmentShortcutCard({ buildVisitState, shortcut }: DevelopmentShortcutCardProps) {
  return (
    <Link
      state={buildVisitState(shortcut)}
      to={shortcut.routePath}
      className="group rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition hover:border-primary-200 hover:bg-primary-50/40"
    >
      <div className="flex items-start justify-between gap-3">
        <span className={`inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${resolveAccentClass(shortcut.accent)}`}>
          <AppIcon className="h-5 w-5" name={normalizeIconName(shortcut.icon)} />
        </span>
        {typeof shortcut.count === 'number' ? (
          <span className="rounded-full bg-slate-100 px-2.5 py-1 text-[11px] font-semibold text-slate-600">
            {shortcut.count}
            {shortcut.countLabel ? shortcut.countLabel : ''}
          </span>
        ) : null}
      </div>
      <div className="mt-3 text-sm font-semibold text-slate-950">{shortcut.title}</div>
      <div className="mt-1 text-xs leading-5 text-slate-500">{shortcut.description}</div>
      {shortcut.recentTargetTitle ? (
        <div className="mt-2 truncate text-[11px] font-medium text-primary-700">最近对象：{shortcut.recentTargetTitle}</div>
      ) : null}
      <div className="mt-4 inline-flex items-center gap-2 text-xs font-semibold text-primary-700">
        <span>{shortcut.actionText}</span>
        <AppIcon className="h-3.5 w-3.5 transition group-hover:translate-x-0.5" name="arrowRight" />
      </div>
    </Link>
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
