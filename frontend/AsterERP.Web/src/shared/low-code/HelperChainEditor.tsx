import { ArrowDown, ArrowUp, Check, Plus, Search, Trash2, X } from 'lucide-react';
import { useMemo, useState } from 'react';

import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';

import {
  createExpressionHelper,
  expressionHelperDefinitions,
  type ExpressionHelperDefinition,
  type LowCodeExpressionHelper
} from './expressionHelperCatalog';

interface HelperChainEditorProps<T extends LowCodeExpressionHelper> {
  helpers: T[];
  onChange: (helpers: T[]) => void;
  title?: string;
}

export function HelperChainEditor<T extends LowCodeExpressionHelper>({
  helpers,
  onChange,
  title = '辅助函数链'
}: HelperChainEditorProps<T>) {
  const [query, setQuery] = useState('');
  const [activeGroup, setActiveGroup] = useState('全部');
  const [selectedHelperNames, setSelectedHelperNames] = useState<string[]>([]);
  const helperGroups = useMemo(() => groupHelpers(expressionHelperDefinitions), []);
  const groupOptions = useMemo(() => [
    { count: expressionHelperDefinitions.length, label: translateCurrentLiteral("全部"), value: '全部' },
    ...Object.entries(helperGroups).map(([group, items]) => ({ count: items.length, label: group, value: group }))
  ], [helperGroups]);
  const filteredHelpers = useMemo(() => filterHelpers(query, activeGroup), [query, activeGroup]);
  const groupedHelpers = useMemo(() => groupHelpers(filteredHelpers), [filteredHelpers]);
  const selectedHelperSet = useMemo(() => new Set(selectedHelperNames), [selectedHelperNames]);
  const visibleHelperNameSet = useMemo(() => new Set(filteredHelpers.map((helper) => helper.name)), [filteredHelpers]);

  return (
    <div className="space-y-3">
      <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
        <div className="flex items-center justify-between gap-2">
          <span className="text-xs font-semibold text-slate-700">{title}</span>
          <div className="flex items-center gap-1.5">
            <span className="rounded-full bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700">
              全部 {expressionHelperDefinitions.length} 项
            </span>
            <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-500">
              当前 {filteredHelpers.length} 项
            </span>
          </div>
        </div>
        <div className="mt-2 flex max-h-24 flex-wrap gap-1.5 overflow-y-auto pr-1">
          {groupOptions.map((group) => (
            <button
              key={group.value}
              className={[
                'rounded-full border px-2.5 py-1 text-[11px] font-medium transition',
                activeGroup === group.value
                  ? 'border-blue-500 bg-blue-600 text-white shadow-sm'
                  : 'border-slate-200 bg-slate-50 text-slate-600 hover:border-blue-200 hover:bg-blue-50 hover:text-blue-700'
              ].join(' ')}
              type="button"
              onClick={() => setActiveGroup(group.value)}
            >
              {group.label}
              <span className="ml-1 opacity-75">{group.count}</span>
            </button>
          ))}
        </div>
        <label className="mt-2 flex h-8 items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-2 text-xs text-slate-500">
          <Search className="h-3.5 w-3.5" />
          <input
            className="min-w-0 flex-1 bg-transparent text-xs text-slate-700 outline-none"
            placeholder={translateCurrentLiteral("搜索函数名称、分组或英文标识")}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
          {query ? (
            <button className="rounded p-0.5 text-slate-400 hover:bg-slate-200 hover:text-slate-600" type="button" onClick={() => setQuery('')}>
              <X className="h-3.5 w-3.5" />
            </button>
          ) : null}
        </label>
        <div className="mt-2 rounded-lg border border-blue-100 bg-blue-50/40 p-2">
          <div className="mb-1.5 flex items-center justify-between gap-2">
            <span className="text-[11px] font-semibold text-blue-700">{translateCurrentLiteral("常用函数")}</span>
            <span className="text-[11px] text-blue-500">{quickHelperNames.length} 项，可直接加入链条</span>
          </div>
          <div className="grid max-h-40 grid-cols-[repeat(auto-fit,minmax(108px,1fr))] gap-1.5 overflow-y-auto pr-1">
            {quickHelperNames.map((name) => {
              const definition = expressionHelperDefinitions.find((helper) => helper.name === name);
              if (!definition) {
                return null;
              }

              return (
                <button
                  key={name}
                  className="min-h-8 rounded-md border border-blue-100 bg-white px-2 py-1 text-left text-[11px] font-medium text-blue-700 shadow-sm hover:border-blue-300 hover:bg-blue-50"
                  title={`${definition.title} / ${definition.name}`}
                  type="button"
                  onClick={() => onChange([...helpers, createExpressionHelper<T>(name)])}
                >
                  <span className="block truncate">+ {definition.title}</span>
                  <span className="block truncate font-mono text-[10px] text-blue-400">{definition.name}</span>
                </button>
              );
            })}
          </div>
        </div>
        <div className="mt-2 flex items-center justify-between text-[11px] font-semibold text-slate-500">
          <span>{translateCurrentLiteral("完整函数库")}</span>
          <span>{activeGroup} / {filteredHelpers.length} 项</span>
        </div>
        <div className="mt-1 max-h-[28rem] space-y-3 overflow-y-auto rounded-lg border border-slate-100 bg-slate-50/60 p-2 pr-1">
          {Object.keys(groupedHelpers).length === 0 ? (
            <div className="rounded-md border border-dashed border-slate-200 bg-slate-50 px-2 py-6 text-center text-xs text-slate-400">{translateCurrentLiteral("没有匹配的函数")}</div>
          ) : (
            Object.entries(groupedHelpers).map(([group, items]) => (
              <div key={group} className="space-y-1.5">
                <div className="flex items-center justify-between text-[11px] font-semibold uppercase tracking-wide text-slate-400">
                  <span>{group}</span>
                  <span>{items.length}</span>
                </div>
                <div className="grid grid-cols-[repeat(auto-fit,minmax(132px,1fr))] gap-1.5">
                  {items.map((helper) => {
                    const checked = selectedHelperSet.has(helper.name);
                    return (
                      <button
                        key={helper.name}
                        className={[
                          'flex min-h-8 items-center gap-2 rounded-md border px-2 py-1.5 text-left text-xs transition',
                          checked
                            ? 'border-blue-300 bg-blue-50 text-blue-700'
                            : 'border-slate-200 bg-white text-slate-700 hover:border-blue-200 hover:bg-blue-50/60'
                        ].join(' ')}
                        type="button"
                        onClick={() => toggleHelper(helper.name)}
                      >
                        <span className={[
                          'flex h-4 w-4 shrink-0 items-center justify-center rounded border',
                          checked ? 'border-blue-500 bg-blue-600 text-white' : 'border-slate-300 bg-white text-transparent'
                        ].join(' ')}>
                          <Check className="h-3 w-3" />
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate font-medium">{helper.title}</span>
                          <span className="block truncate font-mono text-[10px] text-slate-400">{helper.name}</span>
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
            ))
          )}
        </div>
        <div className="mt-2 grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-2">
          <button
            className="primary-button h-8 flex-1 text-xs"
            type="button"
            disabled={selectedHelperNames.length === 0}
            onClick={appendSelectedHelpers}
          >
            <Plus className="h-3.5 w-3.5" />
            加入选中 {selectedHelperNames.length ? `(${selectedHelperNames.length})` : ''}
          </button>
          <button className="secondary-button h-8 px-3 text-xs" type="button" disabled={filteredHelpers.length === 0} onClick={selectVisibleHelpers}>{translateCurrentLiteral("全选当前")}</button>
          <button className="secondary-button h-8 px-3 text-xs" type="button" disabled={selectedHelperNames.length === 0} onClick={clearVisibleSelection}>{translateCurrentLiteral("取消当前")}</button>
        </div>
        <div className="mt-1.5 text-[11px] text-slate-400">{translateCurrentLiteral("完整函数库支持多选，运行时按链条顺序逐个执行；拖动排序可用上移/下移调整。")}</div>
      </div>

      {helpers.length === 0 ? (
        <div className="rounded border border-dashed border-slate-200 bg-slate-50 px-2 py-3 text-center text-xs text-slate-400">{translateCurrentLiteral("未配置辅助函数")}</div>
      ) : (
        helpers.map((helper, index) => {
          const definition = expressionHelperDefinitions.find((item) => item.name === helper.name);
          return (
            <div key={`${helper.name}-${index}`} className="rounded border border-slate-200 bg-white p-2">
              <div className="mb-2 flex items-center gap-1">
                <span className="min-w-0 flex-1 truncate text-xs font-semibold text-slate-700">
                  {definition?.title ?? helper.name}
                </span>
                <button className="icon-button h-6 w-6" type="button" onClick={() => move(index, -1)}>
                  <ArrowUp className="h-3.5 w-3.5" />
                </button>
                <button className="icon-button h-6 w-6" type="button" onClick={() => move(index, 1)}>
                  <ArrowDown className="h-3.5 w-3.5" />
                </button>
                <button className="icon-button h-6 w-6 text-red-500" type="button" onClick={() => remove(index)}>
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
              {(definition?.args ?? []).length > 0 ? (
                <div className="space-y-1">
                  {(definition?.args ?? []).map((arg) => (
                    <label key={arg.key} className="grid grid-cols-[70px_minmax(0,1fr)] items-center gap-2 text-xs">
                      <span className="text-right text-sky-700">{arg.label}</span>
                      <input
                        className="form-input h-7 text-xs"
                        placeholder={arg.placeholder}
                        value={String(helper.args?.[arg.key] ?? '')}
                        onChange={(event) => updateArg(index, arg.key, event.target.value)}
                      />
                    </label>
                  ))}
                </div>
              ) : null}
            </div>
          );
        })
      )}
    </div>
  );

  function toggleHelper(name: string) {
    setSelectedHelperNames((current) => {
      if (current.includes(name)) {
        return current.filter((item) => item !== name);
      }

      return [...current, name];
    });
  }

  function appendSelectedHelpers() {
    if (selectedHelperNames.length === 0) {
      return;
    }

    onChange([...helpers, ...selectedHelperNames.map((name) => createExpressionHelper<T>(name))]);
    setSelectedHelperNames([]);
  }

  function selectVisibleHelpers() {
    setSelectedHelperNames((current) => {
      const next = new Set(current);
      filteredHelpers.forEach((helper) => next.add(helper.name));
      return [...next];
    });
  }

  function clearVisibleSelection() {
    setSelectedHelperNames((current) => current.filter((name) => !visibleHelperNameSet.has(name)));
  }

  function updateArg(index: number, key: string, value: string) {
    onChange(helpers.map((helper, helperIndex) => helperIndex === index ? { ...helper, args: { ...helper.args, [key]: value } } : helper));
  }

  function move(index: number, offset: number) {
    const nextIndex = index + offset;
    if (nextIndex < 0 || nextIndex >= helpers.length) {
      return;
    }

    const next = [...helpers];
    [next[index], next[nextIndex]] = [next[nextIndex], next[index]];
    onChange(next);
  }

  function remove(index: number) {
    onChange(helpers.filter((_, helperIndex) => helperIndex !== index));
  }
}

const quickHelperNames = [
  'trim',
  'normalizeWhitespace',
  'defaultIfEmpty',
  'replace',
  'substring',
  'onlyDigits',
  'toNumber',
  'toBoolean',
  'round',
  'fixed',
  'currency',
  'percent',
  'formatDate',
  'now',
  'addDays',
  'diffDays',
  'jsonPath',
  'parseJson',
  'first',
  'count',
  'join',
  'mapField',
  'filterEquals',
  'sortBy',
  'groupBy',
  'sum',
  'coalesce',
  'isEmpty',
  'ifElse',
  'equals',
  'contains',
  'inList',
  'isEmail',
  'isPhone',
  'maskPhone',
  'mapValue'
];

function filterHelpers(query: string, group: string) {
  const keyword = query.trim().toLowerCase();
  return expressionHelperDefinitions.filter((helper) =>
    (group === '全部' || helper.group === group) &&
    (!keyword ||
      helper.title.toLowerCase().includes(keyword) ||
      helper.name.toLowerCase().includes(keyword) ||
      helper.group.toLowerCase().includes(keyword))
  );
}

function groupHelpers(helpers: ExpressionHelperDefinition[]) {
  return helpers.reduce<Record<string, ExpressionHelperDefinition[]>>((groups, helper) => {
    groups[helper.group] = groups[helper.group] ? [...groups[helper.group], helper] : [helper];
    return groups;
  }, {});
}
