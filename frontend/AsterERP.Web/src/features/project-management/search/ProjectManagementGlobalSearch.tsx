import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  getProjectManagementSearchIndexStatus,
  searchProjectManagement,
} from '../../../api/project-management/projectManagement.api';
import type {
  ProjectManagementSearchIndexStatus,
  ProjectManagementSearchItem,
  ProjectManagementSearchResponse,
} from '../../../api/project-management/projectManagement.types';
import { isHttpError } from '../../../core/http/httpError';
import { normalizeProjectManagementTargetRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import { ProjectManagementEscapeStack, useProjectManagementEscapeLayer } from '../interactions/ProjectManagementEscapeStack';
import { useProjectManagementGlobalShortcuts } from '../interactions/useProjectManagementGlobalShortcuts';

const DEBOUNCE_MS = 280;

const groupDefinitions: Array<{
  key: keyof ProjectManagementSearchResponse;
  label: string;
}> = [
  { key: 'projects', label: '项目' },
  { key: 'tasks', label: '任务' },
  { key: 'milestones', label: '里程碑' },
  { key: 'labels', label: '标签' },
  { key: 'members', label: '成员' },
  { key: 'comments', label: '评论' },
];

interface SearchResultRow {
  groupLabel: string;
  item: ProjectManagementSearchItem;
}

export function ProjectManagementGlobalSearch() {
  return <ProjectManagementEscapeStack><ProjectManagementGlobalSearchContent /></ProjectManagementEscapeStack>;
}

function ProjectManagementGlobalSearchContent() {
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [submittedKeyword, setSubmittedKeyword] = useState('');
  const [activeIndex, setActiveIndex] = useState(0);

  const searchQuery = useQuery({
    enabled: open && scope.isAvailable && Boolean(submittedKeyword),
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'global-search', submittedKeyword] as const,
    queryFn: ({ signal }) => searchProjectManagement({ keyword: submittedKeyword, scope: 'all', limit: 50, pageIndex: 1 }, signal),
  });
  const indexStatusQuery = useQuery({
    enabled: open && scope.isAvailable,
    queryKey: ['astererp', 'project-management', scope.tenantId, scope.appCode, 'search-index-status'] as const,
    queryFn: ({ signal }) => getProjectManagementSearchIndexStatus(signal),
    staleTime: 5_000,
  });

  const resultRows = useMemo(() => flattenResults(searchQuery.data?.data), [searchQuery.data?.data]);
  const indexStatus = indexStatusQuery.data?.data;

  useEffect(() => {
    if (!open) return;
    inputRef.current?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const timer = window.setTimeout(() => setSubmittedKeyword(keyword.trim()), DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [keyword, open]);

  useEffect(() => {
    setActiveIndex((current) => Math.min(current, Math.max(resultRows.length - 1, 0)));
  }, [resultRows.length]);

  useProjectManagementEscapeLayer(open, () => setOpen(false));
  useProjectManagementGlobalShortcuts({ search: () => setOpen(true) }, { canExecute: (shortcut) => shortcut === 'search', enabled: scope.isAvailable });

  const openResult = (item: ProjectManagementSearchItem) => {
    // The destination page performs its own authenticated API load and permission check.
    navigate(normalizeProjectManagementTargetRoute(item.targetRoute));
    setOpen(false);
  };

  const handleInputKeyDown = (event: ReactKeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown' && resultRows.length > 0) {
      event.preventDefault();
      setActiveIndex((current) => (current + 1) % resultRows.length);
    } else if (event.key === 'ArrowUp' && resultRows.length > 0) {
      event.preventDefault();
      setActiveIndex((current) => (current - 1 + resultRows.length) % resultRows.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      if (resultRows[activeIndex]) openResult(resultRows[activeIndex].item);
      else setSubmittedKeyword(keyword.trim());
    } else if (event.key === 'Escape') {
      event.preventDefault();
      setOpen(false);
    }
  };

  return (
    <>
      <button
        aria-label="打开全局搜索"
        className="rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 shadow-sm hover:bg-gray-50"
        type="button"
        onClick={() => setOpen(true)}
      >
        全局搜索 <kbd className="ml-1 rounded border border-gray-200 bg-gray-50 px-1 text-xs text-gray-500">Ctrl+F</kbd>
      </button>
      {open ? (
        <div className="fixed inset-0 z-50 flex items-start justify-center bg-slate-950/40 px-4 pt-[12vh]" onMouseDown={() => setOpen(false)}>
          <section
            aria-label="全局搜索"
            aria-modal="true"
            className="w-full max-w-3xl overflow-hidden rounded-xl bg-white shadow-2xl"
            role="dialog"
            onMouseDown={(event) => event.stopPropagation()}
          >
            <div className="border-b border-gray-200 p-4">
              <div className="flex items-center gap-2">
                <input
                  ref={inputRef}
                  aria-activedescendant={resultRows[activeIndex] ? resultId(resultRows[activeIndex].item) : undefined}
                  aria-controls="project-management-global-search-results"
                  aria-label="全局搜索关键字"
                  aria-expanded="true"
                  className="min-w-0 flex-1 rounded border border-gray-300 px-3 py-2 text-base outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                  maxLength={200}
                  placeholder="搜索项目、任务、里程碑、标签、成员或评论"
                  role="combobox"
                  value={keyword}
                  onChange={(event) => setKeyword(event.target.value)}
                  onKeyDown={handleInputKeyDown}
                />
                <button aria-label="关闭全局搜索" className="rounded px-2 py-1 text-gray-500 hover:bg-gray-100" type="button" onClick={() => setOpen(false)}>Esc</button>
              </div>
              <IndexStatusLine status={indexStatus} error={indexStatusQuery.error} />
            </div>
            <div id="project-management-global-search-results" aria-live="polite" className="max-h-[60vh] overflow-y-auto p-4" role="listbox" aria-label="全局搜索结果">
              {!submittedKeyword ? <p className="py-8 text-center text-sm text-gray-500">输入关键字后自动搜索，Enter 可立即提交。</p> : null}
              {submittedKeyword && searchQuery.isLoading ? <p className="py-8 text-center text-sm text-gray-500">搜索中…</p> : null}
              {submittedKeyword && searchQuery.isError ? <p className="py-8 text-center text-sm text-red-600">{isHttpError(searchQuery.error) && searchQuery.error.status === 403 ? '当前账号没有搜索权限。' : '搜索失败，请稍后重试。'}</p> : null}
              {submittedKeyword && !searchQuery.isLoading && !searchQuery.isError && resultRows.length === 0 ? <p className="py-8 text-center text-sm text-gray-500">没有匹配的授权结果。</p> : null}
              {submittedKeyword && !searchQuery.isLoading && !searchQuery.isError ? groupDefinitions.map((group) => {
                const items = searchQuery.data?.data?.[group.key] ?? [];
                if (items.length === 0) return null;
                return (
                  <section key={group.key} aria-label={`${group.label}结果`} className="mb-4 last:mb-0">
                    <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">{group.label}（{items.length}）</h2>
                    <div className="space-y-1">
                      {items.map((item) => {
                        const rowIndex = resultRows.findIndex((row) => row.item.resultType === item.resultType && row.item.id === item.id);
                        const selected = rowIndex === activeIndex;
                        return (
                          <button
                            aria-selected={selected}
                            className={selected ? 'block w-full rounded-lg bg-blue-50 px-3 py-2 text-left ring-1 ring-blue-200' : 'block w-full rounded-lg px-3 py-2 text-left hover:bg-gray-50'}
                            id={resultId(item)}
                            key={`${item.resultType}-${item.id}`}
                            role="option"
                            type="button"
                            onClick={() => openResult(item)}
                            onMouseMove={() => setActiveIndex(rowIndex)}
                          >
                            <span className="block truncate font-medium text-gray-900">{item.title}</span>
                            <span className="block truncate text-sm text-gray-500">{item.summary ?? '无摘要'}</span>
                          </button>
                        );
                      })}
                    </div>
                  </section>
                );
              }) : null}
            </div>
            <footer className="flex items-center justify-between border-t border-gray-200 px-4 py-2 text-xs text-gray-500">
              <span>↑↓ 选择 · Enter 打开 · Esc 关闭</span>
              <span>{resultRows.length} 条结果</span>
            </footer>
          </section>
        </div>
      ) : null}
    </>
  );
}

function flattenResults(response?: ProjectManagementSearchResponse): SearchResultRow[] {
  if (!response) return [];
  return groupDefinitions.flatMap((group) => (response[group.key] ?? []).map((item) => ({ groupLabel: group.label, item })));
}

function resultId(item: ProjectManagementSearchItem): string {
  return `project-management-global-search-${item.resultType}-${item.id}`;
}

function IndexStatusLine({ status, error }: { status?: ProjectManagementSearchIndexStatus; error: unknown }) {
  if (error) return <p className="mt-2 text-xs text-amber-600">索引状态暂不可用，搜索仍按当前授权结果返回。</p>;
  if (!status) return <p className="mt-2 text-xs text-gray-500">正在读取索引状态…</p>;
  const lag = Math.max(status.targetSequenceNo - status.appliedSequenceNo, 0);
  const label = status.status === 'Ready' && lag === 0 ? '已就绪' : status.status === 'Failed' ? '需要恢复' : status.status === 'Rebuilding' ? '重建中' : '增量同步中';
  return <p className="mt-2 text-xs text-gray-500">全文索引：{label}{lag > 0 ? ` · 待处理 ${lag} 条变更` : ''}{status.lastError ? ` · ${status.lastError}` : ''}</p>;
}
