import { ChevronDown, ChevronRight, Edit3, ExternalLink, FileCode2, PencilRuler, RefreshCw, Rocket, Trash2 } from 'lucide-react';
import { Fragment, useMemo, useState } from 'react';

import type {
  ApplicationDevelopmentModuleTreeNode,
  ApplicationDevelopmentPageListItem
} from '../../../api/application-development-center/applicationDevelopmentCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { WorkspaceEmptyState } from '../workspace-shell/WorkspaceEmptyState';
import { WorkspacePanel } from '../workspace-shell/WorkspacePanel';

import { collectModuleSubtreeIds, flattenModuleTree } from './applicationDevelopmentModuleTreeUtils';

interface PageTreeNode {
  children: PageTreeNode[];
  page: ApplicationDevelopmentPageListItem;
}

const designerPermissions = {
  edit: 'app:development-center:designer:edit',
  delete: 'app:development-center:designer:delete',
  preview: 'app:development-center:designer:preview',
  publish: 'app:development-center:designer:publish',
  view: 'app:development-center:designer:view'
} as const;

interface PageBoardProps {
  errorMessage?: string | null;
  isLoading?: boolean;
  modules: ApplicationDevelopmentModuleTreeNode[];
  pages: ApplicationDevelopmentPageListItem[];
  publishingPageId?: string | null;
  deletingPageId?: string | null;
  refreshingPageId?: string | null;
  selectedModuleId: string | null;
  onCreatePage: () => void;
  onEditPage: (page: ApplicationDevelopmentPageListItem) => void;
  onDeletePage: (page: ApplicationDevelopmentPageListItem) => void;
  onOpenDesigner: (page: ApplicationDevelopmentPageListItem) => void;
  onOpenPreview: (page: ApplicationDevelopmentPageListItem) => void;
  onPublish: (page: ApplicationDevelopmentPageListItem) => void;
  onRefreshPreview: (page: ApplicationDevelopmentPageListItem) => void;
}

export function PageBoard({
  modules,
  onCreatePage,
  onEditPage,
  onOpenDesigner,
  onDeletePage,
  onOpenPreview,
  onPublish,
  onRefreshPreview,
  errorMessage,
  isLoading,
  pages,
  publishingPageId,
  deletingPageId,
  refreshingPageId,
  selectedModuleId
}: PageBoardProps) {
  const allMenusLabel = translateCurrentLiteral("全部菜单");
  const unassignedMenuLabel = translateCurrentLiteral("未归属菜单");
  const flattenedModules = useMemo(() => flattenModuleTree(modules), [modules]);
  const selectedModule = selectedModuleId ? flattenedModules.find((module) => module.id === selectedModuleId) ?? null : null;
  const visibleModuleIds = useMemo(
    () => selectedModuleId ? collectModuleSubtreeIds(modules, selectedModuleId) : new Set<string>(),
    [modules, selectedModuleId]
  );
  const filteredPages = selectedModuleId
    ? pages.filter((page) => page.moduleId && visibleModuleIds.has(page.moduleId))
    : pages;
  const pageTree = useMemo(() => buildPageTree(filteredPages), [filteredPages]);
  const [collapsedPageIds, setCollapsedPageIds] = useState<Set<string>>(() => new Set());
  const moduleName = selectedModule?.moduleName ?? allMenusLabel;

  function toggleCollapsed(pageId: string) {
    setCollapsedPageIds((current) => {
      const next = new Set(current);
      if (next.has(pageId)) {
        next.delete(pageId);
      } else {
        next.add(pageId);
      }

      return next;
    });
  }

  function renderPageNode(node: PageTreeNode, depth = 0) {
    const page = node.page;
    const hasChildren = node.children.length > 0;
    const collapsed = collapsedPageIds.has(page.id);
    const pageModuleName = page.moduleId
      ? flattenedModules.find((module) => module.id === page.moduleId)?.moduleName ?? unassignedMenuLabel
      : unassignedMenuLabel;
    const isAuxiliaryPage = page.pageType === 'dialog' || page.pageType === 'drawer';

    return (
      <Fragment key={page.id}>
      <article
        className={`rounded border bg-white px-2.5 py-1.5 shadow-sm transition hover:border-primary-200 hover:bg-slate-50 ${
          depth > 0 ? 'border-dashed border-slate-200 bg-slate-50/60' : 'border-slate-200'
        }`}
      >
        <div className="flex min-w-0 items-center gap-2">
          <button
            className={`flex h-7 w-6 shrink-0 items-center justify-center rounded text-slate-400 ${hasChildren ? 'hover:bg-slate-100 hover:text-slate-700' : 'cursor-default opacity-40'}`}
            disabled={!hasChildren}
            type="button"
            onClick={() => toggleCollapsed(page.id)}
          >
            {hasChildren && !collapsed ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
          </button>
          <div className="min-w-0 flex-1">
            <div className="grid min-w-0 items-center gap-2 text-xs md:grid-cols-[minmax(120px,1fr)_88px_64px_52px]">
              <span className="flex min-w-0 items-center gap-1.5">
                <FileCode2 className={`h-3.5 w-3.5 shrink-0 ${isAuxiliaryPage ? 'text-sky-500' : 'text-primary-500'}`} />
                <span className="truncate font-semibold text-slate-950">{page.pageName}</span>
              </span>
              <span className="truncate text-slate-500">{pageModuleName}</span>
              <span className={`w-fit rounded-full px-1.5 py-0.5 font-medium ${
                page.pageType === 'standard'
                  ? 'bg-slate-100 text-slate-600'
                  : page.pageType === 'dialog'
                    ? 'bg-sky-100 text-sky-700'
                    : 'bg-indigo-100 text-indigo-700'
              }`}>
                {formatPageType(page.pageType)}
              </span>
              <span className={`w-fit rounded-full px-1.5 py-0.5 font-medium ${page.status === 'Published' ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'}`}>
                {page.status === 'Published' ? '已发布' : '草稿'}
              </span>
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-1">
            <PermissionButton code={designerPermissions.edit} className="primary-button h-7 w-7 px-0" title={translateCurrentLiteral("设计")} type="button" onClick={() => onOpenDesigner(page)}>
              <PencilRuler className="h-3.5 w-3.5" />
            </PermissionButton>
            <PermissionButton code={designerPermissions.edit} className="secondary-button h-7 w-7 px-0" title={translateCurrentLiteral("信息")} type="button" onClick={() => onEditPage(page)}>
              <Edit3 className="h-3.5 w-3.5" />
            </PermissionButton>
            <PermissionButton
              code={designerPermissions.delete}
              className="secondary-button h-7 w-7 px-0 text-red-600 hover:text-red-700"
              disabled={deletingPageId === page.id}
              title={translateCurrentLiteral("删除")}
              type="button"
              onClick={() => onDeletePage(page)}
            >
              <Trash2 className="h-3.5 w-3.5" />
            </PermissionButton>
            <PermissionButton code={designerPermissions.preview} className="secondary-button h-7 w-7 px-0" disabled={!page.previewRoutePath} title={translateCurrentLiteral("预览")} type="button" onClick={() => onOpenPreview(page)}>
              <ExternalLink className="h-3.5 w-3.5" />
            </PermissionButton>
            <PermissionButton
              code={designerPermissions.preview}
              className="secondary-button h-7 w-7 px-0"
              disabled={refreshingPageId === page.id}
              title={translateCurrentLiteral("刷新预览菜单")}
              type="button"
              onClick={() => onRefreshPreview(page)}
            >
              <RefreshCw className="h-3.5 w-3.5" />
            </PermissionButton>
            <PermissionButton
              code={designerPermissions.publish}
              className="primary-button h-7 w-7 px-0"
              disabled={publishingPageId === page.id}
              title={translateCurrentLiteral("发布")}
              type="button"
              onClick={() => onPublish(page)}
            >
              <Rocket className="h-3.5 w-3.5" />
            </PermissionButton>
          </div>
        </div>
      </article>
      {hasChildren && !collapsed ? (
        <div className="ml-5 space-y-1.5 border-l border-dashed border-slate-200 pl-3">
          {node.children.map((child) => renderPageNode(child, depth + 1))}
        </div>
      ) : null}
      </Fragment>
    );
  }

  return (
    <WorkspacePanel
      actions={(
        <button className="primary-button h-8 text-xs" type="button" onClick={onCreatePage}>
          <FileCode2 className="h-3.5 w-3.5" />{translateCurrentLiteral("新建页面")}</button>
      )}
      bodyClassName="min-h-0 flex-1 overflow-auto p-3"
      className="flex min-h-0 flex-col"
      title={`${moduleName} · ${filteredPages.length} 个页面`}
    >
        {isLoading ? (
          <WorkspaceEmptyState className="min-h-[360px]">
            <div>
              <div className="font-semibold text-slate-700">{translateCurrentLiteral("正在获取页面数据")}</div>
              <div className="mt-1 text-xs text-slate-500">{translateCurrentLiteral("请稍候。")}</div>
            </div>
          </WorkspaceEmptyState>
        ) : errorMessage ? (
          <WorkspaceEmptyState className="min-h-[360px]">
            <div>
              <div className="font-semibold text-red-600">{translateCurrentLiteral("页面数据加载失败")}</div>
              <div className="mt-1 text-xs text-slate-500">{errorMessage}</div>
            </div>
          </WorkspaceEmptyState>
        ) : filteredPages.length === 0 ? (
          <WorkspaceEmptyState className="min-h-[360px]">
            <div>
              <div className="font-semibold text-slate-700">{translateCurrentLiteral("当前菜单还没有页面")}</div>
              <button className="primary-button mt-3 h-8 text-xs" type="button" onClick={onCreatePage}>{translateCurrentLiteral("新建页面")}</button>
            </div>
          </WorkspaceEmptyState>
        ) : (
          <div className="space-y-2">
            {pageTree.map((node) => renderPageNode(node))}
          </div>
        )}
    </WorkspacePanel>
  );
}

function buildPageTree(pages: ApplicationDevelopmentPageListItem[]): PageTreeNode[] {
  const byId = new Map<string, PageTreeNode>();
  const roots: PageTreeNode[] = [];

  for (const page of pages) {
    byId.set(page.id, { children: [], page });
  }

  for (const page of pages) {
    const node = byId.get(page.id);
    if (!node) {
      continue;
    }

    const parent = page.parentPageId ? byId.get(page.parentPageId) : null;
    if (parent && parent.page.id !== page.id) {
      parent.children.push(node);
      continue;
    }

    roots.push(node);
  }

  return roots;
}

function formatPageType(pageType: ApplicationDevelopmentPageListItem['pageType']) {
  if (pageType === 'dialog') {
    return '弹框页';
  }

  if (pageType === 'drawer') {
    return '抽屉页';
  }

  return '主页面';
}
