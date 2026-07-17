// 注意：monaco-editor 及其 worker 均通过动态 import 加载，不参与同步模块图，
// 以降低 Vite 构建时 transform 阶段的内存峰值。
import type * as Monaco from 'monaco-editor';
import { useEffect, useMemo, useRef } from 'react';

import type { MicroflowDomainObject, MicroflowSqlScript } from '../../../../api/application-data-center/applicationDataCenter.types';
import type { RuntimeExpressionFunctionCatalogResponse } from '../../../../api/runtime/runtimeExpressionFunctions.types';
import { getMonaco } from '../../../../shared/monaco/monacoLoader';

import type { MicroflowNodeReferenceOption } from './microflowNodeContext';
import {
  buildMicroflowSqlScriptMarkers,
  createMicroflowSqlScriptCompletionProvider,
  createMicroflowSqlScriptHoverProvider,
  createMicroflowSqlScriptSignatureHelpProvider,
  ensureMicroflowSqlScriptLanguage,
  microflowSqlScriptLanguageId
} from './microflowSqlScriptLanguage';

interface MicroflowSqlScriptEditorProps {
  functionCatalog?: RuntimeExpressionFunctionCatalogResponse | null;
  references: MicroflowNodeReferenceOption[];
  script: MicroflowSqlScript;
  tables: MicroflowDomainObject[];
  value: string;
  onChange: (value: string) => void;
}

declare global {
  interface Window {
    MonacoEnvironment?: {
      getWorker: () => Worker;
    };
  }
}

const markerOwner = 'astererp-microflow-sql-script';

export function MicroflowSqlScriptEditor({
  functionCatalog,
  references,
  script,
  tables,
  value,
  onChange
}: MicroflowSqlScriptEditorProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const editorRef = useRef<Monaco.editor.IStandaloneCodeEditor | null>(null);
  const initialValueRef = useRef(value);
  const modelRef = useRef<Monaco.editor.ITextModel | null>(null);
  const changeRef = useRef(onChange);
  const monacoRef = useRef<typeof Monaco | null>(null);
  const context = useMemo(
    () => ({ functionCatalog, references, script, tables }),
    [functionCatalog, references, script, tables]
  );

  useEffect(() => {
    changeRef.current = onChange;
  }, [onChange]);

  // 初始化：动态加载 monaco，注册 worker 和自定义语言
  useEffect(() => {
    let cancelled = false;
    void getMonaco().then((monaco) => {
      if (cancelled) return;
      monacoRef.current = monaco;
      // Worker 也通过动态 import 加载，避免静态引用链进入同步模块图
      window.MonacoEnvironment ??= {
        getWorker: () => {
          const workerUrl = new URL(
            /* @vite-ignore */
            'monaco-editor/esm/vs/editor/editor.worker',
            import.meta.url
          );
          return new Worker(workerUrl, { type: 'module' });
        }
      };
      ensureMicroflowSqlScriptLanguage(monaco);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  // 创建编辑器实例（等 monaco 加载完成后再初始化）
  useEffect(() => {
    let cancelled = false;
    void getMonaco().then((monaco) => {
      if (cancelled || !containerRef.current || editorRef.current) {
        return;
      }

      const model = monaco.editor.createModel(initialValueRef.current, microflowSqlScriptLanguageId);
      const editor = monaco.editor.create(containerRef.current, {
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
        changeRef.current(editor.getValue());
      });

      // 返回清理函数（在 Promise 内部，React 不会直接调用，但 cancelled 标志会阻止后续操作）
      if (cancelled) {
        subscription.dispose();
        editor.dispose();
        model.dispose();
      }
    });

    return () => {
      cancelled = true;
      // 编辑器已创建时同步清理
      if (editorRef.current) {
        editorRef.current.dispose();
        editorRef.current = null;
      }
      if (modelRef.current) {
        modelRef.current.dispose();
        modelRef.current = null;
      }
    };
  }, []);

  useEffect(() => {
    const editor = editorRef.current;
    if (!editor) {
      return;
    }

    if (editor.getValue() !== value) {
      editor.setValue(value);
    }
  }, [value]);

  useEffect(() => {
    const monaco = monacoRef.current;
    if (!monaco) return;

    const completionProvider = monaco.languages.registerCompletionItemProvider(
      microflowSqlScriptLanguageId,
      createMicroflowSqlScriptCompletionProvider(monaco, context)
    );
    const hoverProvider = monaco.languages.registerHoverProvider(
      microflowSqlScriptLanguageId,
      createMicroflowSqlScriptHoverProvider(monaco, context)
    );
    const signatureProvider = monaco.languages.registerSignatureHelpProvider(
      microflowSqlScriptLanguageId,
      createMicroflowSqlScriptSignatureHelpProvider(monaco, context)
    );
    const model = modelRef.current;
    if (model) {
      monaco.editor.setModelMarkers(model, markerOwner, buildMicroflowSqlScriptMarkers(monaco, model, context));
    }

    return () => {
      completionProvider.dispose();
      hoverProvider.dispose();
      signatureProvider.dispose();
    };
  }, [context]);

  useEffect(() => {
    const monaco = monacoRef.current;
    const editor = editorRef.current;
    const model = modelRef.current;
    if (!monaco || !editor || !model) {
      return;
    }

    const disposable = editor.onDidChangeModelContent(() => {
      monaco.editor.setModelMarkers(model, markerOwner, buildMicroflowSqlScriptMarkers(monaco, model, context));
    });
    monaco.editor.setModelMarkers(model, markerOwner, buildMicroflowSqlScriptMarkers(monaco, model, context));
    return () => disposable.dispose();
  }, [context]);

  return (
    <div
      className="microflow-sql-script-editor"
      data-microflow-code-editor="sql"
      ref={containerRef}
      onKeyDown={(event) => event.stopPropagation()}
      onKeyUp={(event) => event.stopPropagation()}
    />
  );
}
