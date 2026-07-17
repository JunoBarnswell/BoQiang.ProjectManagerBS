import { Clock3, Play, Plus, Wand2, X } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

import {
  getApplicationDataSourceColumns,
  getApplicationDataSourceTables
} from '../../../../../api/application-data-center/applicationDataCenter.api';
import type {
  ApplicationDataCenterPreviewResponse,
  ApplicationDataSourceColumn,
  ApplicationDataSourceTable
} from '../../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import { getMonaco } from '../../../../../shared/monaco/monacoLoader';
import type { Monaco } from '../../../../../shared/monaco/monacoLoader';

import { WorkbenchResultViewer } from './WorkbenchResultViewer';

interface WorkbenchSqlEditorProps {
  dataSourceId?: string;
  label?: string;
  readOnly?: boolean;
  value: string;
  onChange: (value: string) => void;
  onExecute?: () => Promise<ApplicationDataCenterPreviewResponse | null>;
  onPreview?: () => void;
}

type MetadataStatus = 'idle' | 'loading' | 'ready' | 'error' | 'empty';
type TableMetadata = ApplicationDataSourceTable & { columns?: ApplicationDataSourceColumn[] };
interface SqlTab { id: string; name: string; value: string; }
interface SqlHistoryItem { id: string; value: string; executedAt: string; }

const markerOwner = 'astererp-workbench-sql';
const sqlLanguageId = 'astererp-workbench-sql';
const sqlKeywords = [
  'SELECT', 'INSERT', 'INTO', 'UPDATE', 'DELETE', 'FROM', 'WHERE', 'WITH', 'AS', 'JOIN', 'LEFT',
  'RIGHT', 'FULL', 'INNER', 'OUTER', 'ON', 'GROUP', 'BY', 'ORDER', 'HAVING', 'LIMIT', 'OFFSET',
  'UNION', 'ALL', 'DISTINCT', 'VALUES', 'SET', 'CASE', 'WHEN', 'THEN', 'ELSE', 'END', 'AND', 'OR',
  'NOT', 'IS', 'NULL', 'TRUE', 'FALSE', 'ASC', 'DESC', 'CREATE', 'TABLE', 'VIEW'
];

export function WorkbenchSqlEditor({ dataSourceId, label = 'SQL', readOnly = false, value, onChange, onExecute, onPreview }: WorkbenchSqlEditorProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const editorRef = useRef<Monaco.editor.IStandaloneCodeEditor | null>(null);
  const modelRef = useRef<Monaco.editor.ITextModel | null>(null);
  const monacoRef = useRef<typeof import('monaco-editor') | null>(null);
  const valueRef = useRef(value);
  const changeRef = useRef(onChange);
  const tablesRef = useRef<TableMetadata[]>([]);
  const columnsLoadingRef = useRef(new Map<string, Promise<ApplicationDataSourceColumn[]>>());
  const [tabs, setTabs] = useState<SqlTab[]>([{ id: 'query-1', name: 'Query 1', value }]);
  const [activeTabId, setActiveTabId] = useState('query-1');
  const [history, setHistory] = useState<SqlHistoryItem[]>([]);
  const [result, setResult] = useState<ApplicationDataCenterPreviewResponse | null>(null);
  const [executing, setExecuting] = useState(false);
  const [status, setStatus] = useState<MetadataStatus>(dataSourceId ? 'loading' : 'idle');
  const [statusMessage, setStatusMessage] = useState(dataSourceId ? '正在加载数据源元数据…' : '未提供数据源，无法启用补全');

  useEffect(() => { changeRef.current = onChange; }, [onChange]);
  useEffect(() => { valueRef.current = value; }, [value]);
  useEffect(() => {
    setTabs((current) => current.map((tab) => tab.id === activeTabId && tab.value !== value ? { ...tab, value } : tab));
  }, [activeTabId, value]);

  useEffect(() => {
    if (!dataSourceId) {
      tablesRef.current = [];
      setStatus('idle');
      setStatusMessage('未提供数据源，无法启用补全');
      return;
    }

    const controller = new AbortController();
    setStatus('loading');
    setStatusMessage('正在加载数据源元数据…');
    void getApplicationDataSourceTables(dataSourceId, controller.signal).then((response) => {
      if (controller.signal.aborted) return;
      tablesRef.current = response.data ?? [];
      setStatus(tablesRef.current.length > 0 ? 'ready' : 'empty');
      setStatusMessage(tablesRef.current.length > 0 ? `${tablesRef.current.length} 个表/视图可用于补全` : '数据源没有可用表或视图元数据');
    }).catch((error: unknown) => {
      if (controller.signal.aborted) return;
      setStatus('error');
      setStatusMessage(error instanceof Error ? error.message : '数据源元数据加载失败');
    });
    return () => controller.abort();
  }, [dataSourceId]);

  useEffect(() => {
    let cancelled = false;
    void getMonaco().then((monaco) => {
      if (cancelled || !containerRef.current || editorRef.current) return;
      monacoRef.current = monaco;
      ensureSqlLanguage(monaco);
      const model = monaco.editor.createModel(valueRef.current, sqlLanguageId);
      const editor = monaco.editor.create(containerRef.current, {
        ariaLabel: 'SQL 编辑器',
        automaticLayout: true,
        contextmenu: true,
        fontFamily: 'JetBrains Mono, Consolas, "Cascadia Mono", monospace',
        fontSize: 12,
        glyphMargin: false,
        lineDecorationsWidth: 8,
        lineNumbers: 'on',
        lineNumbersMinChars: 3,
        minimap: { enabled: false },
        model,
        padding: { bottom: 12, top: 12 },
        renderLineHighlight: 'line',
        scrollBeyondLastLine: false,
        smoothScrolling: true,
        tabSize: 2,
        wordWrap: 'on'
      });
      editorRef.current = editor;
      modelRef.current = model;
      const subscription = editor.onDidChangeModelContent(() => {
        const nextValue = editor.getValue();
        valueRef.current = nextValue;
        changeRef.current(nextValue);
        setSqlMarkers(monaco, model, nextValue);
      });
      setSqlMarkers(monaco, model, valueRef.current);
      if (cancelled) {
        subscription.dispose();
        editor.dispose();
        model.dispose();
      }
    });
    return () => {
      cancelled = true;
      editorRef.current?.dispose();
      modelRef.current?.dispose();
      editorRef.current = null;
      modelRef.current = null;
      monacoRef.current = null;
    };
  }, []);

  useEffect(() => {
    const editor = editorRef.current;
    if (editor && editor.getValue() !== value) editor.setValue(value);
  }, [value]);

  useEffect(() => {
    editorRef.current?.updateOptions({ readOnly });
  }, [readOnly]);

  useEffect(() => {
    const monaco = monacoRef.current;
    if (!monaco) return;
    const monacoInstance = monaco;
    const completion = monaco.languages.registerCompletionItemProvider(sqlLanguageId, {
      triggerCharacters: ['.', ' '],
      provideCompletionItems: async (model, position) => {
        const line = model.getLineContent(position.lineNumber).slice(0, position.column - 1);
        const qualifiedMatch = line.match(/([\w$]+)\.([\w$]*)$/);
        const tables = tablesRef.current;
        const suggestions = qualifiedMatch
          ? await getColumnSuggestions(qualifiedMatch[1], dataSourceId)
          : getGeneralSuggestions(tables);
        return {
          suggestions: suggestions.map((suggestion) => ({
            ...suggestion,
            range: {
              endColumn: position.column,
              endLineNumber: position.lineNumber,
              startColumn: position.column - (qualifiedMatch?.[2].length ?? 0),
              startLineNumber: position.lineNumber
            }
          }))
        };
      }
    });
    const formatting = monaco.languages.registerDocumentFormattingEditProvider(sqlLanguageId, {
      provideDocumentFormattingEdits: (model) => [{ range: model.getFullModelRange(), text: formatWorkbenchSql(model.getValue()) }]
    });
    return () => { completion.dispose(); formatting.dispose(); };

    async function getColumnSuggestions(tableName: string, sourceId?: string) {
      if (!sourceId) return [];
      const table = tablesRef.current.find((item) => item.tableName.toLowerCase() === tableName.toLowerCase());
      if (!table) return [];
      const columns = await loadColumns(table, sourceId);
      return columns.map((column) => ({
        detail: column.dataType,
        insertText: column.columnName,
        kind: monacoInstance.languages.CompletionItemKind.Field,
        label: column.columnName
      }));
    }

    function getGeneralSuggestions(tables: TableMetadata[]) {
      const keywords = sqlKeywords.map((keyword) => ({
        detail: 'SQL keyword',
        insertText: keyword,
        kind: monacoInstance.languages.CompletionItemKind.Keyword,
        label: keyword
      }));
      const tableSuggestions = tables.flatMap((table) => {
        const schema = table.schemaName ? `${table.schemaName}.` : '';
        return [{ detail: `${schema}${table.tableType}`, insertText: table.tableName, kind: monacoInstance.languages.CompletionItemKind.Class, label: table.tableName }];
      });
      return [...tableSuggestions, ...keywords];
    }
  }, [dataSourceId]);

  return (
    <div className="block">
      <div className="mb-1 flex items-center justify-between gap-2">
        <span className="text-sm font-medium text-slate-700">{label}</span>
        <div className="flex gap-2">
          <button className="secondary-button h-8" disabled={readOnly} type="button" onClick={addTab}>
            <Plus className="mr-1 h-3.5 w-3.5" />New tab
          </button>
          {onExecute ? <button className="primary-button h-8" disabled={readOnly || executing || !value.trim()} type="button" onClick={() => void executeQuery()}>
            <Play className="mr-1 h-3.5 w-3.5" />{executing ? 'Running…' : 'Run'}
          </button> : null}
          <button className="secondary-button h-8" type="button" aria-label="格式化 SQL" onClick={() => void formatEditor()}>
            <Wand2 className="mr-1 h-3.5 w-3.5" />{translateCurrentLiteral('格式化')}
          </button>
          {onPreview ? <button className="secondary-button h-8" type="button" onClick={onPreview}>{translateCurrentLiteral('预览')}</button> : null}
        </div>
      </div>
      <div className="mb-2 flex items-center gap-1 overflow-x-auto border-b border-slate-200" role="tablist" aria-label="SQL query tabs">
        {tabs.map((tab) => <div className={`flex items-center rounded-t border border-b-0 ${tab.id === activeTabId ? 'border-slate-300 bg-white' : 'border-transparent bg-slate-50'}`} key={tab.id}>
          <button className="px-3 py-1.5 text-xs text-slate-700" role="tab" aria-selected={tab.id === activeTabId} type="button" onClick={() => selectTab(tab.id)}>{tab.name}</button>
          {tabs.length > 1 ? <button className="p-1 text-slate-400 hover:text-slate-700" type="button" aria-label={`Close ${tab.name}`} onClick={() => closeTab(tab.id)}><X className="h-3 w-3" /></button> : null}
        </div>)}
      </div>
      <div
        ref={containerRef}
        className="min-h-[220px] overflow-hidden rounded border border-slate-300 bg-slate-950"
        data-testid="workbench-sql-editor"
        data-workbench-code-editor="sql"
        role="textbox"
        aria-label={`${label} Monaco 编辑器`}
        aria-multiline="true"
        onKeyDown={(event) => event.stopPropagation()}
        onKeyUp={(event) => event.stopPropagation()}
      />
      <div className={`mt-1 text-xs ${status === 'error' ? 'text-rose-600' : 'text-slate-500'}`} role="status" aria-live="polite">
        {statusMessage}
      </div>
      {history.length > 0 ? <div className="mt-3 rounded border border-slate-200 bg-slate-50 p-2" aria-label="SQL query history">
        <div className="mb-1 flex items-center gap-1 text-xs font-medium text-slate-600"><Clock3 className="h-3.5 w-3.5" />History</div>
        <div className="space-y-1">{history.map((item) => <button className="flex w-full items-center justify-between gap-2 rounded px-2 py-1 text-left text-xs text-slate-600 hover:bg-white" key={item.id} type="button" onClick={() => restoreHistory(item)}><code className="truncate">{item.value}</code><span className="shrink-0 text-slate-400">{item.executedAt}</span></button>)}</div>
      </div> : null}
      {onExecute ? <div className="mt-3" aria-label="SQL query result"><WorkbenchResultViewer preview={result} /></div> : null}
    </div>
  );

  async function loadColumns(table: TableMetadata, sourceId: string) {
    if (table.columns) return table.columns;
    const key = `${sourceId}:${table.schemaName ?? ''}:${table.tableName}`;
    const pending = columnsLoadingRef.current.get(key);
    if (pending) return pending;
    const request = getApplicationDataSourceColumns(sourceId, table.tableName).then((response) => {
      table.columns = response.data ?? [];
      return table.columns;
    }).catch((error: unknown) => {
      setStatus('error');
      setStatusMessage(error instanceof Error ? error.message : `表 ${table.tableName} 的列元数据加载失败`);
      return [];
    }).finally(() => columnsLoadingRef.current.delete(key));
    columnsLoadingRef.current.set(key, request);
    return request;
  }

  async function formatEditor() {
    const editor = editorRef.current;
    if (!editor) return;
    editor.getAction('editor.action.formatDocument')?.run();
  }

  function addTab() {
    const id = `query-${Date.now()}`;
    setTabs((current) => [...current, { id, name: `Query ${current.length + 1}`, value: '' }]);
    setActiveTabId(id);
    valueRef.current = '';
    onChange('');
  }

  function selectTab(id: string) {
    const next = tabs.find((tab) => tab.id === id);
    if (!next || next.id === activeTabId) return;
    setActiveTabId(id);
    valueRef.current = next.value;
    onChange(next.value);
  }

  function closeTab(id: string) {
    if (tabs.length === 1) return;
    const index = tabs.findIndex((tab) => tab.id === id);
    const remaining = tabs.filter((tab) => tab.id !== id);
    setTabs(remaining);
    if (id === activeTabId) {
      const next = remaining[Math.max(0, index - 1)] ?? remaining[0];
      setActiveTabId(next.id);
      valueRef.current = next.value;
      onChange(next.value);
    }
  }

  async function executeQuery() {
    if (!onExecute || !value.trim()) return;
    setExecuting(true);
    try {
      const response = await onExecute();
      if (response) setResult(response);
      setHistory((current) => [{ id: `${Date.now()}`, value, executedAt: new Date().toLocaleTimeString() }, ...current.filter((item) => item.value !== value)].slice(0, 10));
    } catch (error: unknown) {
      setStatus('error');
      setStatusMessage(error instanceof Error ? error.message : 'SQL execution failed');
    } finally {
      setExecuting(false);
    }
  }

  function restoreHistory(item: SqlHistoryItem) {
    valueRef.current = item.value;
    onChange(item.value);
    setTabs((current) => current.map((tab) => tab.id === activeTabId ? { ...tab, value: item.value } : tab));
  }
}

export function formatWorkbenchSql(value: string) {
  return value
    .replace(/\s+/g, ' ')
    .replace(/\s*,\s*/g, ', ')
    .replace(/\b(from|where|group by|order by|having|left join|right join|inner join|full join|join|limit|offset|union)\b/gi, '\n$1')
    .replace(/\b(and|or)\b/gi, '\n  $1')
    .replace(/[ \t]+\n/g, '\n')
    .trim();
}

function setSqlMarkers(monaco: typeof import('monaco-editor'), model: Monaco.editor.ITextModel, value: string) {
  const markers: Monaco.editor.IMarkerData[] = [];
  const opening = (value.match(/\(/g) ?? []).length;
  const closing = (value.match(/\)/g) ?? []).length;
  if (opening !== closing) {
    markers.push({
      message: opening > closing ? '缺少右括号 )' : '存在多余的右括号 )',
      severity: monaco.MarkerSeverity.Error,
      startLineNumber: 1,
      startColumn: 1,
      endLineNumber: model.getLineCount(),
      endColumn: model.getLineMaxColumn(model.getLineCount())
    });
  }
  monaco.editor.setModelMarkers(model, markerOwner, markers);
}

function ensureSqlLanguage(monaco: typeof import('monaco-editor')) {
  if (monaco.languages.getLanguages().some((language) => language.id === sqlLanguageId)) return;
  monaco.languages.register({ id: sqlLanguageId });
  monaco.languages.setMonarchTokensProvider(sqlLanguageId, {
    ignoreCase: true,
    tokenizer: {
      root: [
        [/--.*$/, 'comment'],
        [/\/\*/, 'comment', '@comment'],
        [/'([^'\\]|\\.)*'/, 'string'],
        [/"([^"\\]|\\.)*"/, 'string'],
        [/\b(SELECT|FROM|WHERE|INSERT|INTO|UPDATE|DELETE|JOIN|LEFT|RIGHT|FULL|INNER|OUTER|ON|GROUP|BY|ORDER|HAVING|LIMIT|OFFSET|UNION|ALL|DISTINCT|AS|AND|OR|NOT|NULL|CREATE|TABLE|VIEW|SET|VALUES|CASE|WHEN|THEN|ELSE|END)\b/, 'keyword'],
        [/\b\d+(\.\d+)?\b/, 'number'],
        [/[a-zA-Z_][\w$]*/, 'identifier']
      ],
      comment: [[/[^/*]+/, 'comment'], [/\*\//, 'comment', '@pop'], [/./, 'comment']]
    }
  });
}
