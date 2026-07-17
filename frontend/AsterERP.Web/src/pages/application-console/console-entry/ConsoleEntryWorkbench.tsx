import type { ApplicationConsoleDevelopmentShortcutDto } from '../../../api/application-console/applicationConsole.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import type { ApplicationConsoleRecentVisitInput } from '../applicationConsoleRecentVisits';

import { DevelopmentShortcutCard } from './DevelopmentShortcutCard';

interface ConsoleEntryWorkbenchProps {
  buildShortcutVisitState: (shortcut: ApplicationConsoleDevelopmentShortcutDto) => { recentVisit: ApplicationConsoleRecentVisitInput };
  shortcuts: ApplicationConsoleDevelopmentShortcutDto[];
}

export function ConsoleEntryWorkbench({ buildShortcutVisitState, shortcuts }: ConsoleEntryWorkbenchProps) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-3">
        <div className="text-sm font-semibold text-slate-950">{translateCurrentLiteral("开发中心推荐入口")}</div>
        <div className="mt-1 text-xs leading-5 text-slate-500">{translateCurrentLiteral("围绕页面设计、数据建模与微流逻辑，直接进入当前应用最常用的开发工作区。")}</div>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        {shortcuts.map((shortcut) => (
          <DevelopmentShortcutCard key={shortcut.key} buildVisitState={buildShortcutVisitState} shortcut={shortcut} />
        ))}
      </div>
    </section>
  );
}
