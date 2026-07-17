import { ChevronDown, Circle, Edit3, FolderTree, Plus, Search, Trash2 } from 'lucide-react';
import { useMemo, useState } from 'react';

import type {
  ApplicationDevelopmentModuleTreeNode,
  ApplicationDevelopmentModuleUpsertRequest
} from '../../../api/application-development-center/applicationDevelopmentCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { FormRenderer } from '../../../shared/forms/FormRenderer';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { filterTreeNodes } from '../../../shared/tree/treeUtils';
import { WorkspaceEmptyState } from '../workspace-shell/WorkspaceEmptyState';

import { flattenModuleTree, getModuleChildren } from './applicationDevelopmentModuleTreeUtils';

interface MenuTreeCrudPanelProps {
  deleting?: boolean;
  disabled?: boolean;
  modules: ApplicationDevelopmentModuleTreeNode[];
  saving?: boolean;
  selectedModuleId: string | null;
  selectedVersionId: string;
  onCreate: (request: ApplicationDevelopmentModuleUpsertRequest) => void;
  onDelete: (moduleId: string) => void;
  onSelect: (moduleId: string | null) => void;
  onUpdate: (moduleId: string, request: ApplicationDevelopmentModuleUpsertRequest) => void;
}

interface MenuFormState {
  moduleCode: string;
  moduleName: string;
  parentModuleId: string;
  remark: string;
  sortOrder: number;
}

const emptyForm: MenuFormState = {
  moduleCode: '',
  moduleName: '',
  parentModuleId: '',
  remark: '',
  sortOrder: 0
};

export function MenuTreeCrudPanel({
  deleting,
  disabled,
  modules,
  onCreate,
  onDelete,
  onSelect,
  onUpdate,
  saving,
  selectedModuleId,
  selectedVersionId
}: MenuTreeCrudPanelProps) {
  const confirm = useConfirm();
  const flatModules = useMemo(() => flattenModuleTree(modules), [modules]);
  const selectedModule = flatModules.find((module) => module.id === selectedModuleId) ?? null;
  const [formState, setFormState] = useState<MenuFormState>(emptyForm);
  const [editingModuleId, setEditingModuleId] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [searchKeyword, setSearchKeyword] = useState('');
  const [collapsedModuleIds, setCollapsedModuleIds] = useState<Set<string>>(() => new Set());
  const filteredModules = useMemo(
    () => filterTreeNodes(modules, searchKeyword, (module) => `${module.moduleName} ${module.moduleCode}`),
    [modules, searchKeyword]
  );
  const searching = searchKeyword.trim().length > 0;

  const fields = useMemo<FormFieldConfig<MenuFormState>[]>(
    () => [
      { label: translateCurrentLiteral("目录名称"), name: 'moduleName', required: true, section: '应用菜单目录', type: 'text' },
      ...(editingModuleId
        ? [{ disabled: true, helpText: '系统生成，发布菜单和权限绑定使用。', label: translateCurrentLiteral("目录编码"), name: 'moduleCode' as const, section: '应用菜单目录', type: 'text' as const }]
        : []),
      {
        label: translateCurrentLiteral("上级目录"),
        name: 'parentModuleId',
        options: [
          { label: translateCurrentLiteral("根级菜单"), value: '' },
          ...flatModules
            .filter((module) => module.id !== editingModuleId)
            .map((module) => ({ label: `${module.moduleName} / ${module.moduleCode}`, value: module.id }))
        ],
        section: '应用菜单目录',
        type: 'select'
      },
      { label: translateCurrentLiteral("排序"), name: 'sortOrder', section: '应用菜单目录', type: 'number' },
      { label: translateCurrentLiteral("备注"), name: 'remark', section: '应用菜单目录', span: 2, type: 'textarea' }
    ],
    [editingModuleId, flatModules]
  );

  return (
    <>
      <section className="flex min-h-0 flex-col overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm">
        <div className="flex shrink-0 items-center gap-2 border-b border-gray-100 p-2">
          <div className="flex min-w-0 flex-1 items-center gap-1.5 overflow-hidden rounded border border-gray-200 bg-gray-50 px-2 transition-colors focus-within:border-primary-400 focus-within:bg-white">
            <Search className="h-3.5 w-3.5 shrink-0 text-gray-400" />
            <input
              className="min-w-0 flex-1 border-none bg-transparent py-1.5 text-sm outline-none"
              placeholder={translateCurrentLiteral("搜索应用菜单目录")}
              type="text"
              value={searchKeyword}
              onChange={(event) => setSearchKeyword(event.target.value)}
            />
          </div>
          <button
            className="shrink-0 rounded px-1.5 py-1.5 text-xs font-medium text-gray-500 transition-colors hover:bg-primary-50 hover:text-primary-600"
            type="button"
            onClick={() => {
              setSearchKeyword('');
              onSelect(null);
            }}
          >
            All
          </button>
          <button className="icon-button h-8 w-8 shrink-0" disabled={disabled || !selectedVersionId} title={translateCurrentLiteral("新建应用菜单目录")} type="button" onClick={() => openCreate(null)}>
            <Plus className="h-3.5 w-3.5" />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-2">
          <button
            className={`mb-1 flex w-full items-center justify-between rounded py-1.5 pl-2 pr-2 text-left text-sm transition-colors ${selectedModuleId === null ? 'bg-primary-50 font-medium text-primary-700' : 'text-gray-700 hover:bg-primary-50/50'}`}
            type="button"
            onClick={() => onSelect(null)}
          >
            <span className="flex min-w-0 items-center gap-1.5">
              <FolderTree className="h-3.5 w-3.5 shrink-0 text-gray-400" />
              <span className="truncate">{translateCurrentLiteral("全部页面")}</span>
            </span>
            <span className="text-[11px] text-gray-400">All</span>
          </button>

          {modules.length === 0 ? (
            <WorkspaceEmptyState className="px-3 py-8">{translateCurrentLiteral("当前版本还没有应用菜单目录，先创建目录再组织页面。")}</WorkspaceEmptyState>
          ) : filteredModules.length === 0 ? (
            <WorkspaceEmptyState className="px-3 py-8">{translateCurrentLiteral("没有匹配的应用菜单目录。")}</WorkspaceEmptyState>
          ) : (
            <div className="flex flex-col gap-0.5">
              {filteredModules.map((module) => (
                <MenuTreeNode
                  key={module.id}
                  collapsedModuleIds={collapsedModuleIds}
                  deleting={deleting}
                  disabled={disabled}
                  forceExpanded={searching}
                  node={module}
                  selectedModuleId={selectedModuleId}
                  onCreateChild={openCreate}
                  onDelete={(node) => {
                    confirm({
                      content: `确认删除应用菜单目录“${node.moduleName}”吗？存在子目录、页面或业务对象时后端会阻止删除。`,
                      confirmText: '删除',
                      onConfirm: () => onDelete(node.id),
                      title: '删除应用菜单目录'
                    });
                  }}
                  onEdit={openEdit}
                  onSelect={onSelect}
                  onToggle={(moduleId) => {
                    setCollapsedModuleIds((current) => {
                      const next = new Set(current);
                      if (next.has(moduleId)) {
                        next.delete(moduleId);
                      } else {
                        next.add(moduleId);
                      }
                      return next;
                    });
                  }}
                />
              ))}
            </div>
          )}
        </div>

        <div className="shrink-0 border-t border-gray-100 bg-gray-50 px-2.5 py-2 text-[11px] text-gray-500">
          <span className="mr-1 text-gray-400">{translateCurrentLiteral("当前")}</span>
          <span className="font-medium text-gray-700">{selectedModule?.moduleName ?? '全部页面'}</span>
        </div>
      </section>

      <ResponsiveModal
        footer={
          <>
            <button className="secondary-button h-8 text-xs" type="button" onClick={() => setModalOpen(false)}>{translateCurrentLiteral("取消")}</button>
            <button className="primary-button h-8 text-xs" disabled={saving} type="button" onClick={submit}>
              {saving ? '保存中...' : '保存并同步左侧菜单'}
            </button>
          </>
        }
        open={modalOpen}
        description="保存后会同步到左侧应用菜单目录；页面入口仍需发布页面后生成。"
        title={editingModuleId ? '编辑应用菜单目录' : '新建应用菜单目录'}
        onClose={() => setModalOpen(false)}
      >
        <FormRenderer
          fields={fields}
          value={formState}
          onValueChange={(name, value) => setFormState((current) => ({ ...current, [name]: value }))}
        />
      </ResponsiveModal>
    </>
  );

  function openCreate(parentId: string | null) {
    setEditingModuleId(null);
    setFormState({
      ...emptyForm,
      parentModuleId: parentId ?? selectedModuleId ?? '',
      sortOrder: flatModules.length + 1
    });
    setModalOpen(true);
  }

  function openEdit(module: ApplicationDevelopmentModuleTreeNode) {
    setEditingModuleId(module.id);
    setFormState({
      moduleCode: module.moduleCode,
      moduleName: module.moduleName,
      parentModuleId: module.parentModuleId ?? '',
      remark: '',
      sortOrder: module.sortOrder
    });
    setModalOpen(true);
  }

  function submit() {
    const request: ApplicationDevelopmentModuleUpsertRequest = {
      moduleCode: editingModuleId ? formState.moduleCode.trim() : '',
      moduleName: formState.moduleName.trim(),
      parentModuleId: formState.parentModuleId || null,
      remark: formState.remark.trim() || null,
      sortOrder: Number(formState.sortOrder) || 0,
      versionId: selectedVersionId
    };
    if (editingModuleId) {
      onUpdate(editingModuleId, request);
    } else {
      onCreate(request);
    }

    setModalOpen(false);
  }
}

function MenuTreeNode({
  collapsedModuleIds,
  deleting,
  disabled,
  forceExpanded,
  node,
  onCreateChild,
  onDelete,
  onEdit,
  onSelect,
  onToggle,
  selectedModuleId
}: {
  collapsedModuleIds: Set<string>;
  deleting?: boolean;
  disabled?: boolean;
  forceExpanded: boolean;
  node: ApplicationDevelopmentModuleTreeNode;
  selectedModuleId: string | null;
  onCreateChild: (parentId: string) => void;
  onDelete: (node: ApplicationDevelopmentModuleTreeNode) => void;
  onEdit: (node: ApplicationDevelopmentModuleTreeNode) => void;
  onSelect: (moduleId: string | null) => void;
  onToggle: (moduleId: string) => void;
}) {
  const selected = selectedModuleId === node.id;
  const children = getModuleChildren(node);
  const hasChildren = children.length > 0;
  const expanded = forceExpanded || !collapsedModuleIds.has(node.id);
  return (
    <div>
      <div
        className={`group flex items-center gap-1 rounded transition-colors ${selected ? 'bg-primary-50 text-primary-700' : 'text-gray-700 hover:bg-primary-50/50'}`}
        title={`${node.moduleName} / ${node.moduleCode}`}
      >
        <button
          aria-expanded={hasChildren ? expanded : undefined}
          className="ml-1 flex h-6 w-6 shrink-0 items-center justify-center rounded text-gray-400 transition-colors hover:bg-white hover:text-primary-600"
          disabled={!hasChildren}
          title={hasChildren ? (expanded ? '折叠菜单' : '展开菜单') : undefined}
          type="button"
          onClick={() => {
            if (hasChildren) {
              onToggle(node.id);
            }
          }}
        >
          {hasChildren ? <ChevronDown className={`h-3.5 w-3.5 transition-transform ${expanded ? '' : '-rotate-90'}`} /> : <Circle className="h-1.5 w-1.5 text-gray-300" />}
        </button>
        <button className="flex min-w-0 flex-1 items-center justify-between gap-2 py-1.5 pr-1 text-left text-sm" type="button" onClick={() => onSelect(selected ? null : node.id)}>
          <span className="flex min-w-0 items-center gap-1.5">
            <span className={`truncate ${selected ? 'font-medium' : ''}`}>{node.moduleName}</span>
          </span>
          <span className={`shrink-0 text-[11px] transition-colors ${selected ? 'text-primary-400' : 'text-gray-400 group-hover:text-primary-400'}`}>{node.pageCount}</span>
        </button>
        <div className="flex shrink-0 items-center gap-0.5 pr-1 opacity-70 transition-opacity group-hover:opacity-100">
          <button className="icon-button h-6 w-6" disabled={disabled} title={translateCurrentLiteral("新建子菜单")} type="button" onClick={() => onCreateChild(node.id)}>
            <Plus className="h-3 w-3" />
          </button>
          <button className="icon-button h-6 w-6" disabled={disabled} title={translateCurrentLiteral("编辑菜单")} type="button" onClick={() => onEdit(node)}>
            <Edit3 className="h-3 w-3" />
          </button>
          <button className="icon-button h-6 w-6 text-red-500" disabled={disabled || deleting} title={translateCurrentLiteral("删除菜单")} type="button" onClick={() => onDelete(node)}>
            <Trash2 className="h-3 w-3" />
          </button>
        </div>
      </div>
      {hasChildren && expanded ? (
        <div className="mt-0.5 border-l border-gray-100 pl-3">
          {children.map((child) => (
            <MenuTreeNode
              key={child.id}
              collapsedModuleIds={collapsedModuleIds}
              deleting={deleting}
              disabled={disabled}
              forceExpanded={forceExpanded}
              node={child}
              selectedModuleId={selectedModuleId}
              onCreateChild={onCreateChild}
              onDelete={onDelete}
              onEdit={onEdit}
              onSelect={onSelect}
              onToggle={onToggle}
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}
