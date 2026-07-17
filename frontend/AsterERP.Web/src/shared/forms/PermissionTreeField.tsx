import { ChevronRight } from 'lucide-react';
import { useMemo, useState } from 'react';

import type { MenuTreeNodeDto } from '../../api/system/system.types';
import { useI18n } from '../../core/i18n/I18nProvider';

interface PermissionTreeFieldProps {
  nodes: MenuTreeNodeDto[];
  onValueChange: (value: string[]) => void;
  value: string[];
}

interface NodeSelectionState {
  checked: boolean;
  indeterminate: boolean;
}

export function PermissionTreeField({ nodes, onValueChange, value }: PermissionTreeFieldProps) {
  const { translate } = useI18n();
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const selectedSet = useMemo(() => new Set(value), [value]);

  const renderNode = (node: MenuTreeNodeDto, depth = 0) => {
    const permissionCodes = collectPermissionCodes(node);
    const selectionState = getSelectionState(permissionCodes, selectedSet);
    const hasChildren = (node.children ?? []).length > 0;
    const isExpanded = expanded[node.id] ?? true;

    return (
      <div key={node.id} className="flex flex-col">
        <div
          className={`flex items-center gap-2 py-1.5 pr-2 hover:bg-primary-50/50 rounded transition-colors group ${selectionState.checked ? 'text-primary-700' : 'text-gray-700'}`}
          style={{ paddingLeft: `${0.5 + depth * 1.5}rem` }}
        >
          <button
            className={`w-5 h-5 flex items-center justify-center rounded text-gray-400 hover:text-gray-600 hover:bg-gray-200/50 transition-colors shrink-0 ${!hasChildren ? 'invisible' : ''}`}
            disabled={!hasChildren}
            type="button"
            onClick={() => {
              if (hasChildren) {
                setExpanded((current) => ({ ...current, [node.id]: !isExpanded }));
              }
            }}
          >
            {hasChildren ? <ChevronRight className={`transition-transform duration-200 ${isExpanded ? 'rotate-90' : ''}`} size={16} /> : null}
          </button>

          <div className="flex items-center pt-0.5">
            <PermissionTreeCheckbox
              checked={selectionState.checked}
              disabled={permissionCodes.length === 0}
              indeterminate={selectionState.indeterminate}
              onChange={(checked) => toggleNode(permissionCodes, checked, selectedSet, onValueChange)}
            />
          </div>

          <div className="flex flex-1 items-center gap-3 overflow-hidden">
            <div className={`text-sm font-medium truncate ${selectionState.checked ? 'text-primary-700' : 'text-gray-800'}`}>
              {node.menuName}
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <span className="text-[11px] bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded border border-gray-200 leading-none">
                {resolveMenuTypeLabel(node.menuType, translate)}
              </span>
              {node.permissionCode ? (
                <code className="text-[11px] font-mono bg-primary-50 text-primary-600 px-1.5 py-0.5 rounded border border-primary-100 leading-none">
                  {node.permissionCode}
                </code>
              ) : null}
            </div>
          </div>
        </div>

        {hasChildren && isExpanded ? (
          <div className="flex flex-col">{node.children.map((child) => renderNode(child, depth + 1))}</div>
        ) : null}
      </div>
    );
  };

  return (
    <div className="flex flex-col border border-gray-200 rounded-lg bg-white overflow-hidden shadow-sm py-2">
      {nodes.length > 0 ? nodes.map((node) => renderNode(node)) : <div className="text-sm text-gray-400 px-4 py-3">{translate('forms.permissionTree.empty')}</div>}
    </div>
  );
}

function resolveMenuTypeLabel(menuType: string, translate: (key: string) => string): string {
  switch (menuType) {
    case 'Directory':
      return translate('page.systemMenus.type.directory');
    case 'Menu':
      return translate('page.systemMenus.type.menu');
    case 'Button':
      return translate('page.systemMenus.type.button');
    default:
      return menuType;
  }
}

function collectPermissionCodes(node: MenuTreeNodeDto): string[] {
  const result: string[] = [];

  if (node.permissionCode) {
    result.push(node.permissionCode);
  }

  for (const child of node.children ?? []) {
    result.push(...collectPermissionCodes(child));
  }

  return Array.from(new Set(result));
}

function getSelectionState(permissionCodes: string[], selectedSet: Set<string>): NodeSelectionState {
  if (permissionCodes.length === 0) {
    return { checked: false, indeterminate: false };
  }

  const selectedCount = permissionCodes.filter((code) => selectedSet.has(code)).length;
  return {
    checked: selectedCount === permissionCodes.length,
    indeterminate: selectedCount > 0 && selectedCount < permissionCodes.length
  };
}

function toggleNode(permissionCodes: string[], checked: boolean, selectedSet: Set<string>, onValueChange: (value: string[]) => void) {
  const next = new Set(selectedSet);

  if (checked) {
    permissionCodes.forEach((code) => next.add(code));
  } else {
    permissionCodes.forEach((code) => next.delete(code));
  }

  onValueChange(Array.from(next));
}

interface PermissionTreeCheckboxProps {
  checked: boolean;
  disabled?: boolean;
  indeterminate: boolean;
  onChange: (checked: boolean) => void;
}

function PermissionTreeCheckbox({ checked, disabled, indeterminate, onChange }: PermissionTreeCheckboxProps) {
  return (
    <input
      ref={(element) => {
        if (element) {
          element.indeterminate = indeterminate;
        }
      }}
      checked={checked}
      disabled={disabled}
      type="checkbox"
      onChange={(event) => onChange(event.target.checked)}
      className="w-4 h-4 text-primary-600 rounded border-gray-300 focus:ring-primary-500 cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
    />
  );
}
