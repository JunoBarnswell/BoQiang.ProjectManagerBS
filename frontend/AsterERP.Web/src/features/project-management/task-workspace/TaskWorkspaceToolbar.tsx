import { useEffect, useState } from 'react';

import type { ProjectManagementSavedView, ProjectManagementTaskView } from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { ProjectManagementReversibleCommandControls } from '../components/ProjectManagementReversibleCommandControls';
import { taskWorkspaceVisibleColumns, type TaskWorkspaceState } from '../state/taskWorkspaceState';

interface TaskWorkspaceToolbarProps {
  onExport: () => void;
  onOpenBatch: () => void;
  onSelectAll: () => void;
  onCreateTask: () => void;
  onViewChange: (view: ProjectManagementTaskView) => void;
  onSaveView: (name: string, isShared: boolean) => void;
  onSelectSavedView: (view: ProjectManagementSavedView) => void;
  onStateChange: (next: Partial<TaskWorkspaceState>) => void;
  savedViews: ProjectManagementSavedView[];
  savingView: boolean;
  selectedCount: number;
  state: TaskWorkspaceState;
  total: number;
}

const viewRoutes: Array<{ key: ProjectManagementTaskView; label: string }> = [
  { key: 'tree', label: '树' },
  { key: 'list', label: '列表' },
  { key: 'card', label: '卡片' },
  { key: 'board', label: '看板' },
  { key: 'gantt', label: '甘特' },
  { key: 'calendar', label: '日历' },
];

export function TaskWorkspaceToolbar({
  onExport,
  onOpenBatch,
  onSelectAll,
  onCreateTask,
  onViewChange,
  onSaveView,
  onSelectSavedView,
  onStateChange,
  savedViews,
  savingView,
  selectedCount,
  state,
  total,
}: TaskWorkspaceToolbarProps) {
  const [keywordDraft, setKeywordDraft] = useState(state.keyword);
  const [viewName, setViewName] = useState('');
  const [shareView, setShareView] = useState(false);

  useEffect(() => setKeywordDraft(state.keyword), [state.keyword]);

  return (
    <div className="flex flex-col gap-3">
      <nav aria-label="任务视图" className="flex flex-wrap gap-2">
        {viewRoutes.map((view) => (
          <button
            aria-current={view.key === state.viewKey ? 'page' : undefined}
            className={view.key === state.viewKey ? 'rounded bg-blue-600 px-3 py-1 text-sm text-white' : 'rounded border border-gray-300 px-3 py-1 text-sm'}
            key={view.key}
            onClick={() => onViewChange(view.key)}
            type="button"
          >
            {view.label}
          </button>
        ))}
      </nav>
      <div className="flex flex-wrap items-center gap-2">
        <form
          className="flex items-center gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            onStateChange({ keyword: keywordDraft.trim(), pageIndex: 1 });
          }}
        >
          <input
            aria-label="搜索任务"
            onChange={(event) => setKeywordDraft(event.target.value)}
            placeholder="搜索编码、标题或描述"
            value={keywordDraft}
          />
          <button type="submit">搜索</button>
          {state.keyword ? (
            <button type="button" onClick={() => { setKeywordDraft(''); onStateChange({ keyword: '', pageIndex: 1 }); }}>
              清空
            </button>
          ) : null}
        </form>
        <select
          aria-label="任务状态筛选"
          onChange={(event) => onStateChange({ pageIndex: 1, status: event.target.value || undefined })}
          value={state.status ?? ''}
        >
          <option value="">全部状态</option>
          {['Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled'].map((status) => <option key={status} value={status}>{status}</option>)}
        </select>
        <select
          aria-label="任务分组"
          onChange={(event) => onStateChange({ groupBy: event.target.value as TaskWorkspaceState['groupBy'] || undefined })}
          value={state.groupBy ?? ''}
        >
          <option value="">不分组</option>
          <option value="status">状态</option>
          <option value="priority">优先级</option>
          <option value="assignee">负责人</option>
          <option value="milestone">里程碑</option>
          <option value="parent">父任务</option>
        </select>
        <label className="flex items-center gap-1 text-sm">
          <input
            checked={state.includeCompleted}
            onChange={(event) => onStateChange({ includeCompleted: event.target.checked, pageIndex: 1 })}
            type="checkbox"
          />
          包含已完成
        </label>
        <span className="text-sm text-gray-500">共 {total} 个任务</span>
        <button type="button" onClick={onSelectAll}>选择当前页</button>
        <PermissionButton code="project-management:task:edit" disabled={selectedCount === 0} onClick={onOpenBatch}>批量更新{selectedCount ? ` (${selectedCount})` : ''}</PermissionButton>
        <PermissionButton code="project-management:report:export" onClick={onExport}>导出当前筛选</PermissionButton>
        <PermissionButton code="project-management:task:add" onClick={onCreateTask}>新建任务</PermissionButton>
        <ProjectManagementReversibleCommandControls />
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <select
          aria-label="保存视图"
          defaultValue=""
          onChange={(event) => {
            const view = savedViews.find((item) => item.id === event.target.value);
            if (view) onSelectSavedView(view);
          }}
        >
          <option value="">恢复保存视图</option>
          {savedViews.map((item) => (
            <option key={item.id} value={item.id}>{item.viewName}{item.isDefault ? ' · 默认' : ''}</option>
          ))}
        </select>
        <input
          aria-label="视图名称"
          onChange={(event) => setViewName(event.target.value)}
          placeholder="视图名称"
          value={viewName}
        />
        <label className="flex items-center gap-1 text-sm"><input checked={shareView} onChange={(event) => setShareView(event.target.checked)} type="checkbox" />共享给项目</label>
        <PermissionButton
          code="project-management:task:edit"
          disabled={!viewName.trim() || savingView}
          onClick={() => {
            onSaveView(viewName.trim(), shareView);
            setViewName('');
            setShareView(false);
          }}
        >
          {savingView ? '保存中…' : '保存视图'}
        </PermissionButton>
      </div>
      {(state.viewKey === 'tree' || state.viewKey === 'list') ? <fieldset className="flex flex-wrap items-center gap-2 text-sm"><legend className="px-1">保存列显示</legend>{taskWorkspaceVisibleColumns.map((column) => <label className="flex items-center gap-1" key={column}><input checked={state.visibleColumns.includes(column)} onChange={(event) => onStateChange({ visibleColumns: event.target.checked ? [...state.visibleColumns, column] : state.visibleColumns.filter((item) => item !== column) })} type="checkbox" />{column}</label>)}</fieldset> : null}
    </div>
  );
}
