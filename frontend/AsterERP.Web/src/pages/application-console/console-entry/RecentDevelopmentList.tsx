import { Link } from 'react-router-dom';

import type { ApplicationConsoleRecentDevelopmentItemDto } from '../../../api/application-console/applicationConsole.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';
import type { ApplicationConsoleRecentVisitInput } from '../applicationConsoleRecentVisits';

interface RecentDevelopmentListProps {
  buildContinueVisitState: (item: ApplicationConsoleRecentDevelopmentItemDto) => { recentVisit: ApplicationConsoleRecentVisitInput };
  buildPreviewVisitState: (item: ApplicationConsoleRecentDevelopmentItemDto) => { recentVisit: ApplicationConsoleRecentVisitInput };
  items: ApplicationConsoleRecentDevelopmentItemDto[];
  onPublish: (item: ApplicationConsoleRecentDevelopmentItemDto) => void;
  publishingPageId?: string | null;
}

export function RecentDevelopmentList({
  buildContinueVisitState,
  buildPreviewVisitState,
  items,
  onPublish,
  publishingPageId
}: RecentDevelopmentListProps) {
  if (items.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50 px-4 py-10 text-center text-sm text-slate-500">{translateCurrentLiteral("暂无最近编辑页面，进入页面设计后会在这里显示可继续处理的对象。")}</div>
    );
  }

  return (
    <div className="space-y-3">
      {items.map((item) => (
        <article key={`${item.pageId}:${item.updatedTime}`} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <AppIcon className="h-4.5 w-4.5 text-primary-600" name="files" />
                <h3 className="truncate text-sm font-semibold text-slate-950">{item.title}</h3>
                <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${resolveStatusClass(item.status)}`}>
                  {item.status}
                </span>
              </div>
              <div className="mt-1 truncate font-mono text-[11px] text-slate-500">{item.pageCode}</div>
              <div className="mt-2 text-xs leading-5 text-slate-500">{item.description}</div>
              <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-slate-500">
                {item.moduleName ? <span>菜单：{item.moduleName}</span> : null}
                {item.versionName ? <span>版本：{item.versionName}</span> : null}
                <span>更新时间：{formatDate(item.updatedTime)}</span>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              {item.canContinueDesign ? (
                <Link
                  className="primary-button h-8 px-3 text-xs"
                  state={buildContinueVisitState(item)}
                  to={item.continueRoutePath}
                >
                  <AppIcon className="h-3.5 w-3.5" name="edit" />{translateCurrentLiteral("继续设计")}</Link>
              ) : (
                <button className="primary-button h-8 px-3 text-xs opacity-60" disabled type="button">
                  <AppIcon className="h-3.5 w-3.5" name="edit" />{translateCurrentLiteral("继续设计")}</button>
              )}
              {item.canPreview && item.previewRoutePath ? (
                <Link
                  className="secondary-button h-8 px-3 text-xs"
                  state={buildPreviewVisitState(item)}
                  to={item.previewRoutePath}
                >
                  <AppIcon className="h-3.5 w-3.5" name="eye" />{translateCurrentLiteral("预览")}</Link>
              ) : (
                <button className="secondary-button h-8 px-3 text-xs opacity-60" disabled type="button">
                  <AppIcon className="h-3.5 w-3.5" name="eye" />{translateCurrentLiteral("预览")}</button>
              )}
              {item.canPublish ? (
                <button
                  className="secondary-button h-8 px-3 text-xs"
                  disabled={publishingPageId === item.pageId}
                  type="button"
                  onClick={() => onPublish(item)}
                >
                  <AppIcon className={`h-3.5 w-3.5 ${publishingPageId === item.pageId ? 'animate-spin' : ''}`} name="rocket" />
                  {publishingPageId === item.pageId ? '发布中' : '发布'}
                </button>
              ) : null}
            </div>
          </div>
        </article>
      ))}
    </div>
  );
}

function formatDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function resolveStatusClass(status: string) {
  if (status === 'Published') {
    return 'bg-emerald-100 text-emerald-700';
  }

  if (status === 'Draft') {
    return 'bg-amber-100 text-amber-700';
  }

  return 'bg-slate-100 text-slate-700';
}
