import { ChevronRight, Filter, Search, Star } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';

import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { BindingDocument, DesignerValueType } from '../expression/expressionTypes';

import type { StableResourceReference } from './bindingTypes';
import { createConversionPipeline } from './conversionPipeline';
import { findResourceUsages, findUnresolvedResourceUsages, listStableResources, readResourceHistory, rememberResource, searchStableResources, toggleFavoriteResource, type StableResourceUsage } from './resourceExplorerStore';
import { createResourceDropEvent } from './resourcePointerDrag';

export interface ResourceExplorerProps {
  document?: BindingDocument | null;
  expectedType?: DesignerValueType;
  onRepairUsage?: (usage: StableResourceUsage) => void;
  onSelect: (resource: StableResourceReference) => void;
}

export function ResourceExplorer({ document, expectedType, onRepairUsage, onSelect }: ResourceExplorerProps) {
  const [query, setQuery] = useState('');
  const [favorites, setFavorites] = useState(() => readResourceHistory().favorites);
  const [recent, setRecent] = useState(() => readResourceHistory().recent);
  const [sourceFilter, setSourceFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [scope, setScope] = useState<'all' | 'recent' | 'favorites'>('all');
  const [expandedUsageId, setExpandedUsageId] = useState<string | null>(null);
  const [draggingResourceId, setDraggingResourceId] = useState<string | null>(null);
  const pointerIdRef = useRef<number | null>(null);
  const allResources = useMemo(() => listStableResources(document), [document]);
  const unresolvedUsages = useMemo(() => findUnresolvedResourceUsages(document), [document]);
  const sourceOptions = useMemo(() => [...new Set(allResources.map((resource) => resource.resourceType))].sort(), [allResources]);
  const typeOptions = useMemo(() => [...new Set(allResources.map((resource) => resource.valueType))].sort(), [allResources]);
  const resources = useMemo(() => {
    const filtered = searchStableResources(allResources, query).filter((resource) => {
      if (sourceFilter !== 'all' && resource.resourceType !== sourceFilter) return false;
      if (typeFilter !== 'all' && resource.valueType !== typeFilter) return false;
      if (scope === 'recent' && !recent.includes(resource.id)) return false;
      if (scope === 'favorites' && !favorites.includes(resource.id)) return false;
      return true;
    });
    return [...filtered].sort((left, right) => recentIndex(recent, left.id) - recentIndex(recent, right.id));
  }, [allResources, favorites, query, recent, scope, sourceFilter, typeFilter]);
  const grouped = useMemo(() => resources.reduce<Record<string, StableResourceReference[]>>((result, item) => {
    (result[item.resourceType] ??= []).push(item);
    return result;
  }, {}), [resources]);

  useEffect(() => {
    if (!draggingResourceId) return undefined;

    const finishPointerDrag = (event: PointerEvent) => {
      if (pointerIdRef.current !== event.pointerId) return;
      const target = window.document.elementFromPoint(event.clientX, event.clientY)?.closest<HTMLElement>('[data-resource-drop-target="true"]');
      target?.dispatchEvent(createResourceDropEvent(draggingResourceId, target.dataset.bindingSlot));
      pointerIdRef.current = null;
      setDraggingResourceId(null);
    };
    const cancelPointerDrag = (event: PointerEvent) => {
      if (pointerIdRef.current !== event.pointerId) return;
      pointerIdRef.current = null;
      setDraggingResourceId(null);
    };

    window.addEventListener('pointerup', finishPointerDrag);
    window.addEventListener('pointercancel', cancelPointerDrag);
    return () => {
      window.removeEventListener('pointerup', finishPointerDrag);
      window.removeEventListener('pointercancel', cancelPointerDrag);
    };
  }, [draggingResourceId]);

  return <div className="space-y-2" aria-label={translateCurrentLiteral('资源浏览器')}>
    <label className="flex h-8 items-center gap-2 rounded border border-slate-200 bg-white px-2">
      <Search className="h-3.5 w-3.5 text-slate-400" />
      <input aria-label={translateCurrentLiteral('搜索资源')} className="min-w-0 flex-1 text-xs outline-none" value={query} onChange={(event) => setQuery(event.target.value)} placeholder={translateCurrentLiteral('搜索资源、字段或路径')} />
    </label>
    <div className="flex items-center gap-1 rounded border border-slate-200 bg-slate-50 p-1">
      <Filter className="h-3.5 w-3.5 shrink-0 text-slate-400" />
      <select aria-label={translateCurrentLiteral('资源来源')} value={sourceFilter} onChange={(event) => setSourceFilter(event.target.value)} className="min-w-0 flex-1 bg-transparent text-[11px] outline-none">
        <option value="all">{translateCurrentLiteral('全部来源')}</option>
        {sourceOptions.map((source) => <option key={source} value={source}>{source}</option>)}
      </select>
      <select aria-label={translateCurrentLiteral('值类型')} value={typeFilter} onChange={(event) => setTypeFilter(event.target.value)} className="min-w-0 flex-1 bg-transparent text-[11px] outline-none">
        <option value="all">{translateCurrentLiteral('全部类型')}</option>
        {typeOptions.map((type) => <option key={type} value={type}>{type}</option>)}
      </select>
    </div>
    <div className="flex gap-1 text-[11px]">
      {(['all', 'recent', 'favorites'] as const).map((item) => <button key={item} type="button" onClick={() => setScope(item)} className={['rounded px-2 py-1', scope === item ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-600'].join(' ')}>{translateCurrentLiteral(item === 'all' ? '全部' : item === 'recent' ? '最近使用' : '收藏')}</button>)}
    </div>
    {onRepairUsage && unresolvedUsages.length > 0 ? <section aria-label="失效绑定" className="rounded border border-amber-200 bg-amber-50 p-2 text-[10px] text-amber-800">
      <p className="font-semibold">存在失效绑定（{unresolvedUsages.length}）</p>
      {unresolvedUsages.map((usage) => <div key={`${usage.path}:${usage.resourceId}`} className="flex items-center gap-2 py-0.5"><span className="min-w-0 flex-1 truncate" title={usage.path}>{usage.resourceId} · {usage.path}</span><button type="button" className="shrink-0 text-primary-600 hover:underline" onClick={() => onRepairUsage(usage)}>{translateCurrentLiteral('选择替代资源')}</button></div>)}
    </section> : null}
    {Object.entries(grouped).map(([source, items]) => <section key={source}>
      <div className="px-1 py-1 text-[10px] font-semibold uppercase text-slate-400">{source}</div>
      <div className="grid gap-1">
        {items.map((resource) => {
          const compatible = !expectedType || createConversionPipeline(resource.valueType, expectedType).valid;
          const favorite = favorites.includes(resource.id);
          const usages = findResourceUsages(document, resource.id);
          const expanded = expandedUsageId === resource.id;
          return <div key={resource.id} className="space-y-1">
            <div onPointerDown={(event) => {
              if (!compatible || event.button !== 0) return;
              pointerIdRef.current = event.pointerId;
              setDraggingResourceId(resource.id);
            }} className={["flex items-center gap-1 rounded border px-2 py-1.5", compatible ? 'border-slate-200 bg-white' : 'border-slate-100 bg-slate-50 opacity-50', draggingResourceId === resource.id ? 'ring-2 ring-primary-300' : ''].join(' ')}>
              <button type="button" disabled={!compatible} onClick={() => { rememberResource(resource.id); setRecent(readResourceHistory().recent); onSelect(resource); }} className="flex min-w-0 flex-1 items-center gap-1 text-left text-xs text-slate-700"><ChevronRight className="h-3 w-3 shrink-0" /><span className="truncate">{resource.label}</span><span className="ml-auto text-[10px] text-slate-400">{resource.valueType}</span></button>
              {usages.length > 0 ? <button type="button" onPointerDown={(event) => event.stopPropagation()} onClick={() => setExpandedUsageId(expanded ? null : resource.id)} className="rounded px-1 text-[10px] text-slate-500 hover:bg-slate-100">{usages.length} {translateCurrentLiteral('处使用')}</button> : null}
              <button aria-label={`${translateCurrentLiteral(favorite ? '取消收藏' : '收藏')} ${resource.label}`} type="button" onPointerDown={(event) => event.stopPropagation()} onClick={() => { toggleFavoriteResource(resource.id); setFavorites(readResourceHistory().favorites); }} className="rounded p-1 text-slate-400 hover:text-amber-500"><Star className={favorite ? 'h-3 w-3 fill-amber-400 text-amber-400' : 'h-3 w-3'} /></button>
            </div>
            {expanded ? <div className="rounded border border-dashed border-slate-200 bg-slate-50 px-2 py-1 text-[10px] text-slate-500">
              {usages.map((usage) => <div key={usage.path} className="flex items-center gap-2 py-0.5"><span className="min-w-0 flex-1 truncate" title={usage.path}>{usage.path}</span>{onRepairUsage ? <button type="button" onClick={() => onRepairUsage(usage)} className="shrink-0 text-primary-600 hover:underline">{translateCurrentLiteral('修复绑定')}</button> : null}</div>)}
            </div> : null}
          </div>;
        })}
      </div>
    </section>)}
  </div>;
}

function recentIndex(recent: string[], resourceId: string): number {
  const index = recent.indexOf(resourceId);
  return index < 0 ? Number.MAX_SAFE_INTEGER : index;
}
