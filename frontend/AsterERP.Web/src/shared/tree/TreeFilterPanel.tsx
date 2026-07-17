import { ChevronDown, Circle, Search } from 'lucide-react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { PageLoading } from '../status/PageLoading';

import { filterTreeNodes, type TreeLikeNode } from './treeUtils';

interface TreeFilterPanelProps<TNode extends TreeLikeNode<TNode>> {
  allText?: string;
  emptyText: string;
  error?: boolean;
  errorText: string;
  getKey: (node: TNode) => string;
  getLabel: (node: TNode) => string;
  getMeta?: (node: TNode) => string;
  getSearchText: (node: TNode) => string;
  loading?: boolean;
  nodes: TNode[];
  onReset: () => void;
  onSearchKeywordChange: (value: string) => void;
  onSelect: (key: string) => void;
  placeholder: string;
  searchKeyword: string;
  selectedKey: string;
}

export function TreeFilterPanel<TNode extends TreeLikeNode<TNode>>({
  allText = 'table.all',
  emptyText,
  error = false,
  errorText,
  getKey,
  getLabel,
  getMeta,
  getSearchText,
  loading = false,
  nodes,
  onReset,
  onSearchKeywordChange,
  onSelect,
  placeholder,
  searchKeyword,
  selectedKey
}: TreeFilterPanelProps<TNode>) {
  const { translate } = useI18n();
  const filteredNodes = filterTreeNodes(nodes, searchKeyword, getSearchText);
  const resolvedAllText = allText.includes('.') ? translate(allText) : allText;

  const renderNode = (node: TNode, depth = 0) => {
    const key = getKey(node);
    const isActive = selectedKey === key;
    const children = node.children ?? [];
    const hasChildren = children.length > 0;

    return (
      <div key={key}>
        <button
          className={`flex items-center justify-between w-full text-left py-1.5 pr-2 hover:bg-primary-50/50 rounded transition-colors group ${isActive ? 'bg-primary-50 text-primary-700 font-medium' : 'text-gray-700'}`}
          style={{ paddingLeft: `${depth * 1 + 0.5}rem` }}
          type="button"
          onClick={() => onSelect(isActive ? '' : key)}
        >
          <span className="flex items-center gap-1.5 truncate">
            {hasChildren ? <ChevronDown size={14} className="text-gray-400 shrink-0" /> : <Circle size={6} className="text-gray-300 shrink-0 ml-1" />}
            <span title={getLabel(node)} className="truncate text-sm">{getLabel(node)}</span>
          </span>
          {getMeta ? <span className={`text-[11px] shrink-0 transition-colors ${isActive ? 'text-primary-400' : 'text-gray-400 group-hover:text-primary-400'}`}>{getMeta(node)}</span> : null}
        </button>
        {hasChildren ? (
          <div className="flex flex-col mt-0.5">
            {children.map((child) => renderNode(child, depth + 1))}
          </div>
        ) : null}
      </div>
    );
  };

  return (
    <div className="flex flex-col h-full bg-white rounded-lg">
      <div className="p-2 border-b border-gray-100 flex items-center gap-2 shrink-0">
        <div className="flex-1 flex items-center gap-1.5 bg-gray-50 border border-gray-200 rounded px-2 focus-within:border-primary-400 focus-within:bg-white transition-colors overflow-hidden">
          <Search size={14} className="text-gray-400 shrink-0" />
          <input
            className="flex-1 bg-transparent border-none outline-none text-sm py-1.5 min-w-0"
            placeholder={placeholder}
            type="text"
            value={searchKeyword}
            onChange={(event) => onSearchKeywordChange(event.target.value)}
          />
        </div>
        <button className="text-xs text-gray-500 hover:text-primary-600 px-1.5 py-1.5 shrink-0 transition-colors font-medium" type="button" onClick={onReset}>
          {resolvedAllText}
        </button>
      </div>

      <div className="flex-1 overflow-y-auto p-2">
        {loading ? (
          <div className="flex justify-center p-4"><PageLoading /></div>
        ) : error ? (
          <div className="text-sm text-red-500 p-4 text-center">{errorText}</div>
        ) : filteredNodes.length === 0 ? (
          <div className="text-sm text-gray-400 p-4 text-center">{emptyText}</div>
        ) : (
          <div className="flex flex-col gap-0.5">
            {filteredNodes.map((node) => renderNode(node))}
          </div>
        )}
      </div>
    </div>
  );
}
