import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

import type {
  ApplicationConsoleDevelopmentShortcutDto,
  ApplicationConsoleSummaryDto
} from '../../../api/application-console/applicationConsole.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';
import type { ApplicationConsoleRecentVisitInput } from '../applicationConsoleRecentVisits';

interface ConsoleEntryRightRailProps {
  buildShortcutVisitState: (shortcut: ApplicationConsoleDevelopmentShortcutDto) => { recentVisit: ApplicationConsoleRecentVisitInput };
  dataModelingShortcut?: ApplicationConsoleDevelopmentShortcutDto | null;
  microflowShortcut?: ApplicationConsoleDevelopmentShortcutDto | null;
  onRefresh: () => void;
  onReturnToPlatform: () => void;
  onViewRecentPublishes: () => void;
  pageDesignShortcut?: ApplicationConsoleDevelopmentShortcutDto | null;
  summary: ApplicationConsoleSummaryDto;
}

export function ConsoleEntryRightRail({
  buildShortcutVisitState,
  dataModelingShortcut,
  microflowShortcut,
  onRefresh,
  onReturnToPlatform,
  onViewRecentPublishes,
  pageDesignShortcut,
  summary
}: ConsoleEntryRightRailProps) {
  return (
    <aside className="space-y-3">
      <Panel title={translateCurrentLiteral("版本上下文")} description={summary.versionContext.summary}>
        <div className="space-y-2 text-sm">
          <InfoRow label="草稿版本" value={String(summary.versionContext.draftVersionCount)} />
          <InfoRow label="已发布版本" value={String(summary.versionContext.publishedVersionCount)} />
          <InfoRow
            label="最新草稿"
            value={summary.versionContext.latestDraftVersion ? `${summary.versionContext.latestDraftVersion.versionName} / ${summary.versionContext.latestDraftVersion.versionCode}` : '暂无'}
          />
          <InfoRow
            label="最近正式发布"
            value={summary.versionContext.latestPublishTime ? formatDate(summary.versionContext.latestPublishTime) : '暂无'}
          />
        </div>
      </Panel>

      <Panel title={translateCurrentLiteral("数据库状态")} description={summary.databaseBinding.message ?? "应用数据库绑定状态"}>
        <div className="space-y-2 text-sm">
          <InfoRow label="状态" value={summary.databaseBinding.isReachable ? '已连接' : summary.databaseBinding.isBound ? '连接异常' : '未绑定'} />
          <InfoRow label="类型" value={summary.databaseBinding.provider ?? '-'} />
          <InfoRow label="名称" value={summary.databaseBinding.displayName ?? '-'} />
          <InfoRow label="库名" value={summary.databaseBinding.databaseName ?? '-'} />
        </div>
      </Panel>

      <Panel title={translateCurrentLiteral("快捷动作")} description="从控制台首页直接进入最常用的开发承接页。">
        <div className="space-y-2">
          {pageDesignShortcut ? (
            <ShortcutLink buildShortcutVisitState={buildShortcutVisitState} shortcut={pageDesignShortcut} titleOverride="进入页面设计" />
          ) : null}
          {dataModelingShortcut ? (
            <ShortcutLink buildShortcutVisitState={buildShortcutVisitState} shortcut={dataModelingShortcut} titleOverride="进入数据建模" />
          ) : null}
          {microflowShortcut ? (
            <ShortcutLink buildShortcutVisitState={buildShortcutVisitState} shortcut={microflowShortcut} titleOverride="进入微流管理" />
          ) : null}

          <button className="secondary-button h-9 w-full justify-start px-3 text-xs" type="button" onClick={onRefresh}>
            <AppIcon className="h-3.5 w-3.5" name="refresh" />{translateCurrentLiteral("刷新摘要")}</button>
          <button className="secondary-button h-9 w-full justify-start px-3 text-xs" type="button" onClick={onViewRecentPublishes}>
            <AppIcon className="h-3.5 w-3.5" name="clock" />{translateCurrentLiteral("查看最近发布")}</button>
          <button className="secondary-button h-9 w-full justify-start px-3 text-xs" type="button" onClick={onReturnToPlatform}>
            <AppIcon className="h-3.5 w-3.5" name="arrowRight" />{translateCurrentLiteral("返回平台级")}</button>
        </div>
      </Panel>
    </aside>
  );
}

function Panel({
  children,
  description,
  title
}: {
  children: ReactNode;
  description: string;
  title: string;
}) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-3">
        <div className="text-sm font-semibold text-slate-950">{title}</div>
        <div className="mt-1 text-xs leading-5 text-slate-500">{description}</div>
      </div>
      {children}
    </section>
  );
}

function ShortcutLink({
  buildShortcutVisitState,
  shortcut,
  titleOverride
}: {
  buildShortcutVisitState: (shortcut: ApplicationConsoleDevelopmentShortcutDto) => { recentVisit: ApplicationConsoleRecentVisitInput };
  shortcut: ApplicationConsoleDevelopmentShortcutDto;
  titleOverride: string;
}) {
  return (
    <Link
      className="secondary-button h-9 w-full justify-start px-3 text-xs"
      state={buildShortcutVisitState(shortcut)}
      to={shortcut.routePath}
    >
      <AppIcon className="h-3.5 w-3.5" name={shortcut.icon as Parameters<typeof AppIcon>[0]['name']} />
      {titleOverride}
    </Link>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-3 rounded-lg border border-slate-100 bg-slate-50 px-3 py-2">
      <span className="text-xs text-slate-500">{label}</span>
      <span className="text-right text-xs font-medium text-slate-900">{value}</span>
    </div>
  );
}

function formatDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
