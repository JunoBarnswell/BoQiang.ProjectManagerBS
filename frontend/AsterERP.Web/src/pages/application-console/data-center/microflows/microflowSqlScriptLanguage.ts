import type * as Monaco from 'monaco-editor';

import type {
  MicroflowDomainField,
  MicroflowDomainObject,
  MicroflowSqlScript,
  MicroflowValueExpression
} from '../../../../api/application-data-center/applicationDataCenter.types';
import type {
  RuntimeExpressionFunctionCatalogResponse,
  RuntimeExpressionFunctionDefinitionDto
} from '../../../../api/runtime/runtimeExpressionFunctions.types';
import {
  buildRuntimeExpressionFunctionSignature,
  buildRuntimeExpressionFunctionSnippet,
  findRuntimeExpressionFunction,
  normalizeFunctionName,
  runtimeExpressionFunctionNamespaces
} from '../../../../shared/runtime/expression-functions/runtimeExpressionFunctionCatalog';

import type { MicroflowNodeReferenceOption } from './microflowNodeContext';
import { cloneMicroflowField, normalizeVariableValueType } from './microflowVariableSchema';

export interface MicroflowSqlScriptLanguageContext {
  functionCatalog?: RuntimeExpressionFunctionCatalogResponse | null;
  references: MicroflowNodeReferenceOption[];
  script: MicroflowSqlScript;
  tables: MicroflowDomainObject[];
}

export interface MicroflowSqlScriptFunctionDiagnostic {
  length: number;
  message: string;
  startIndex: number;
}

const languageId = 'astererp-microflow-sql-script';
const sqlKeywords = [
  'SELECT',
  'INSERT',
  'INTO',
  'UPDATE',
  'DELETE',
  'TRUNCATE',
  'FROM',
  'WHERE',
  'WITH',
  'AS',
  'JOIN',
  'LEFT',
  'RIGHT',
  'INNER',
  'OUTER',
  'ON',
  'GROUP',
  'BY',
  'ORDER',
  'HAVING',
  'LIMIT',
  'TOP',
  'DECLARE',
  'SET',
  'IF',
  'ELSE',
  'FOR',
  'IN',
  'RETURN',
  'JSON',
  'BEGIN',
  'COMMIT',
  'ROLLBACK',
  'TRANSACTION',
  'CREATE',
  'TEMP',
  'TEMPORARY',
  'TABLE',
  'DROP',
  'AND',
  'OR',
  'NOT',
  'IS',
  'NULL',
  'TRUE',
  'FALSE'
];
const forbiddenWords = ['alter', 'attach', 'call', 'detach', 'exec', 'execute', 'grant', 'merge', 'pragma', 'replace', 'revoke', 'vacuum'];
const runtimeFunctionNamespaceSet = new Set(runtimeExpressionFunctionNamespaces.map((item) => item.toLowerCase()));

export const microflowSqlScriptBuiltInVariables = [
  { detail: '当前用户 ID', label: '@currentUserId', type: 'string' },
  { detail: '当前租户 ID', label: '@currentTenantId', type: 'string' },
  { detail: '当前应用编码', label: '@currentAppCode', type: 'string' },
  { detail: '当前任职 ID', label: '@currentEmploymentId', type: 'string' },
  { detail: '当前部门 ID', label: '@currentDeptId', type: 'string' },
  { detail: '当前全部任职部门 ID 数组', label: '@currentDeptIds', type: 'array' },
  { detail: '当前岗位 ID', label: '@currentPositionId', type: 'string' },
  { detail: '当前全部任职岗位 ID 数组', label: '@currentPositionIds', type: 'array' },
  { detail: '当前数据范围', label: '@currentDataScope', type: 'string' },
  { detail: '是否已登录，SQL 中为 1/0', label: '@isAuthenticated', type: 'number' },
  { detail: '是否平台管理员，SQL 中为 1/0', label: '@isPlatformAdmin', type: 'number' },
  { detail: '是否租户管理员，SQL 中为 1/0', label: '@isTenantAdmin', type: 'number' },
  { detail: '审计用户 ID', label: '@auditUserId', type: 'string' },
  { detail: '审计时间', label: '@auditNow', type: 'datetime' },
  { detail: '创建人审计值', label: '@auditCreatedBy', type: 'string' },
  { detail: '修改人审计值', label: '@auditUpdatedBy', type: 'string' },
  { detail: '创建时间审计值', label: '@auditCreatedTime', type: 'datetime' },
  { detail: '修改时间审计值', label: '@auditUpdatedTime', type: 'datetime' }
] as const;

let languageRegistered = false;

export function ensureMicroflowSqlScriptLanguage(monaco: typeof Monaco) {
  if (languageRegistered) {
    return;
  }

  monaco.languages.register({ id: languageId });
  monaco.languages.setLanguageConfiguration(languageId, {
    brackets: [['{', '}'], ['(', ')'], ['[', ']']],
    comments: {
      lineComment: '--',
      blockComment: ['/*', '*/']
    },
    autoClosingPairs: [
      { open: "'", close: "'" },
      { open: '"', close: '"' },
      { open: '(', close: ')' },
      { open: '{', close: '}' },
      { open: '[', close: ']' }
    ]
  });
  monaco.languages.setMonarchTokensProvider(languageId, {
    ignoreCase: true,
    keywords: sqlKeywords,
    tokenizer: {
      root: [
        [/--.*$/, 'comment'],
        [/\/\*/, 'comment', '@comment'],
        [/'(?:''|[^'])*'/, 'string'],
        [/@[A-Za-z_][\w]*/, 'variable'],
        [/(StringFns|NumberFns|DateFns|FormatFns|JsonFns|ObjectFns|ArrayFns|RegexFns|UrlFns|TypeFns|RbacFns)(?=\s*\.)/, 'type.identifier'],
        [/[A-Za-z_][\w]*/, {
          cases: {
            '@keywords': 'keyword',
            '@default': 'identifier'
          }
        }],
        [/[{}()[\]]/, '@brackets'],
        [/[=<>!]+/, 'operator'],
        [/\d+(?:\.\d+)?/, 'number']
      ],
      comment: [
        [/[^*/]+/, 'comment'],
        [/\*\//, 'comment', '@pop'],
        [/./, 'comment']
      ]
    }
  });
  languageRegistered = true;
}

export function createMicroflowSqlScriptCompletionProvider(
  monaco: typeof Monaco,
  context: MicroflowSqlScriptLanguageContext
): Monaco.languages.CompletionItemProvider {
  return {
    triggerCharacters: ['.', '@', ' '],
    provideCompletionItems(model, position) {
      const word = model.getWordUntilPosition(position);
      const range = {
        endColumn: word.endColumn,
        endLineNumber: position.lineNumber,
        startColumn: word.startColumn,
        startLineNumber: position.lineNumber
      };
      const beforeCursor = model.getValueInRange({
        endColumn: position.column,
        endLineNumber: position.lineNumber,
        startColumn: 1,
        startLineNumber: position.lineNumber
      });
      const suggestions = [
        ...createKeywordSuggestions(monaco, range),
        ...createTableSuggestions(monaco, context, beforeCursor, range),
        ...createVariableSuggestions(monaco, context, range),
        ...createRuntimeFunctionSuggestions(monaco, context, beforeCursor, range),
        ...createDotSuggestions(monaco, context, beforeCursor, range),
        ...createResultFieldSuggestions(monaco, context, range)
      ];
      return { suggestions };
    }
  };
}

export function createMicroflowSqlScriptHoverProvider(
  monaco: typeof Monaco,
  context: MicroflowSqlScriptLanguageContext
): Monaco.languages.HoverProvider {
  return {
    provideHover(model, position) {
      const fn = findFunctionAtPosition(model, position, context.functionCatalog);
      if (!fn) {
        return null;
      }

      return {
        contents: [
          { value: `\`${buildRuntimeExpressionFunctionSignature(fn)}\`` },
          { value: fn.description },
          ...fn.examples.map((example) => ({ value: `示例：\`${example}\`` }))
        ],
        range: model.getWordAtPosition(position)
          ? undefined
          : new monaco.Range(position.lineNumber, position.column, position.lineNumber, position.column)
      };
    }
  };
}

export function createMicroflowSqlScriptSignatureHelpProvider(
  _monaco: typeof Monaco,
  context: MicroflowSqlScriptLanguageContext
): Monaco.languages.SignatureHelpProvider {
  return {
    signatureHelpRetriggerCharacters: [','],
    signatureHelpTriggerCharacters: ['(', ','],
    provideSignatureHelp(model, position) {
      const call = findActiveFunctionCall(model, position, context.functionCatalog);
      if (!call) {
        return null;
      }

      const parameters = [
        ...(call.fn.requiresInput ? ['value'] : []),
        ...call.fn.parameters.map((param) => param.name)
      ];
      return {
        dispose: () => undefined,
        value: {
          activeParameter: Math.min(call.activeParameter, Math.max(parameters.length - 1, 0)),
          activeSignature: 0,
          signatures: [
            {
              documentation: call.fn.description,
              label: buildRuntimeExpressionFunctionSignature(call.fn),
              parameters: parameters.map((parameter) => ({ label: parameter }))
            }
          ]
        }
      };
    }
  };
}

export function buildMicroflowSqlScriptMarkers(
  monaco: typeof Monaco,
  model: Monaco.editor.ITextModel,
  context: MicroflowSqlScriptLanguageContext
): Monaco.editor.IMarkerData[] {
  const script = model.getValue();
  const markers: Monaco.editor.IMarkerData[] = [];
  const stripped = maskSqlTrivia(script);
  for (const word of forbiddenWords) {
    const match = new RegExp(`\\b${word}\\b`, 'i').exec(stripped);
    if (match) {
      markers.push(createMarker(monaco, model, match.index, word.length, `SQL 脚本禁止 ${word.toUpperCase()} 等危险语句`));
    }
  }

  for (const match of stripped.matchAll(/\b(create|drop)\b[\s\S]*?(?:;|$)/gi)) {
    const statement = match[0];
    const allowed = /^\s*create\s+(temp|temporary)\s+table\b/i.test(statement) ||
      /^\s*drop\s+(temp|temporary)\s+table\b/i.test(statement) ||
      /^\s*drop\s+table\s+if\s+exists\s+temp\.[A-Za-z_][A-Za-z0-9_]*\s*;?\s*$/i.test(statement);
    if (!allowed) {
      markers.push(createMarker(monaco, model, match.index ?? 0, statement.length, 'SQL 脚本只允许临时表 DDL，不能操作永久数据库对象'));
    }
  }

  if (!/\breturn\s+(select|@|json\b)/i.test(script)) {
    markers.push({
      endColumn: 1,
      endLineNumber: 1,
      message: 'SQL 脚本必须显式 RETURN SELECT、RETURN @变量 或 RETURN JSON',
      severity: monaco.MarkerSeverity.Error,
      startColumn: 1,
      startLineNumber: 1
    });
  }

  markers.push(...buildMicroflowSqlScriptFunctionDiagnostics(script, context.functionCatalog)
    .map((diagnostic) => createMarker(monaco, model, diagnostic.startIndex, diagnostic.length, diagnostic.message)));

  const knownVariables = new Set([
    ...context.script.parameters.map((item) => item.name.trim().replace(/^@+/, '').toLowerCase()).filter(Boolean),
    ...context.script.localVariables.map((item) => item.name.trim().replace(/^@+/, '').toLowerCase()).filter(Boolean),
    ...Array.from(script.matchAll(/\bdeclare\s+@(?<name>[A-Za-z_][\w]*)/gi)).map((item) => item.groups?.name?.toLowerCase() ?? '').filter(Boolean)
  ]);
  for (const match of script.matchAll(/(?<!@)@(?<name>[A-Za-z_][\w]*)/g)) {
    const name = match.groups?.name ?? '';
    if (name && !knownVariables.has(name.toLowerCase())) {
      knownVariables.add(name.toLowerCase());
    }
  }

  return markers;
}

export function buildMicroflowSqlScriptFunctionDiagnostics(
  script: string,
  catalog: RuntimeExpressionFunctionCatalogResponse | null | undefined
): MicroflowSqlScriptFunctionDiagnostic[] {
  const diagnostics: MicroflowSqlScriptFunctionDiagnostic[] = [];
  const masked = maskSqlTrivia(script);
  const catalogNamespaces = new Set((catalog?.functions ?? []).map((fn) => fn.namespace.toLowerCase()));
  const knownNamespaces = new Set([...runtimeFunctionNamespaceSet, ...catalogNamespaces]);
  const functionNames = new Set((catalog?.functions ?? []).flatMap((fn) => [fn.functionName, fn.canonicalName]).map(normalizeFunctionName));

  for (const match of masked.matchAll(/\b(?<namespace>[A-Za-z_][\w]*Fns)\s*\.\s*(?<name>[A-Za-z_][\w]*)\s*\(/g)) {
    const namespaceName = match.groups?.namespace ?? '';
    const functionName = match.groups?.name ?? '';
    const qualifiedName = `${namespaceName}.${functionName}`;
    const startIndex = match.index ?? 0;
    if (!knownNamespaces.has(namespaceName.toLowerCase())) {
      diagnostics.push({
        length: qualifiedName.length,
        message: `未知函数命名空间 ${namespaceName}`,
        startIndex
      });
      continue;
    }

    const fn = findRuntimeExpressionFunction(catalog, qualifiedName);
    if (!fn) {
      diagnostics.push({
        length: qualifiedName.length,
        message: `函数 ${qualifiedName} 不在当前 SQL 白名单中`,
        startIndex
      });
      continue;
    }

    const callSpan = readFunctionCallSpan(masked, startIndex);
    if (!callSpan) {
      continue;
    }

    const args = splitFunctionArguments(script.slice(callSpan.openParenIndex + 1, callSpan.endIndex));
    const requiredCount = (fn.requiresInput ? 1 : 0) + fn.parameters.filter((param) => param.required).length;
    const maxCount = (fn.requiresInput ? 1 : 0) + fn.parameters.length;
    if (args.length < requiredCount || args.length > maxCount) {
      diagnostics.push({
        length: qualifiedName.length,
        message: `${qualifiedName} 参数数量应为 ${requiredCount === maxCount ? requiredCount : `${requiredCount}-${maxCount}`} 个`,
        startIndex
      });
    }

    if (namespaceName.toLowerCase() === 'rbacfns') {
      diagnostics.push(...buildRbacFunctionArgumentDiagnostics(script, args, callSpan.openParenIndex + 1, qualifiedName));
    }

    const columnArgument = args.find((arg) => isBareSqlColumnArgument(arg, knownNamespaces));
    if (columnArgument) {
      const columnOffset = script.indexOf(columnArgument, callSpan.openParenIndex + 1);
      diagnostics.push({
        length: columnArgument.length,
        message: `函数参数不能直接引用数据库列 ${columnArgument}，列处理请使用 SQL 原生函数`,
        startIndex: columnOffset >= 0 ? columnOffset : callSpan.openParenIndex + 1
      });
    }
  }

  for (const match of masked.matchAll(/\bset\s+@[A-Za-z_][\w]*\s*=\s*(?<name>[A-Za-z_][\w]*)\s*\(/gi)) {
    const functionName = match.groups?.name ?? '';
    if (!functionNames.has(normalizeFunctionName(functionName))) {
      continue;
    }

    const nameIndex = (match.index ?? 0) + match[0].lastIndexOf(functionName);
    diagnostics.push({
      length: functionName.length,
      message: `函数 ${functionName} 必须使用 StringFns/NumberFns 等命名空间`,
      startIndex: nameIndex
    });
  }

  return diagnostics;
}

export function inferMicroflowSqlScriptResultFields(script: string): MicroflowDomainField[] {
  const returnSelect = /\breturn\s+select\s+(?<columns>[\s\S]+?)(?:\s+from\b|;|$)/i.exec(script);
  if (!returnSelect?.groups?.columns) {
    return [];
  }

  return splitSqlColumns(returnSelect.groups.columns)
    .map((column, index) => inferFieldFromSqlColumn(column, index))
    .filter((field): field is MicroflowDomainField => Boolean(field));
}

export function createSqlResultFieldExpression(field: MicroflowDomainField): MicroflowValueExpression {
  const dataType = normalizeVariableValueType(field.dataType);
  const fieldCode = field.fieldCode.trim();
  return {
    dataType,
    kind: 'ref',
    ref: {
      dataType,
      fieldPath: [fieldCode],
      label: `SQL结果.${fieldCode}`,
      outputKey: 'sqlRow',
      sourceType: 'sqlResult',
      variableId: 'sqlRow'
    }
  };
}

export { languageId as microflowSqlScriptLanguageId };

function createKeywordSuggestions(
  monaco: typeof Monaco,
  range: Monaco.IRange
): Monaco.languages.CompletionItem[] {
  return sqlKeywords.map((keyword) => ({
    insertText: keyword,
    kind: monaco.languages.CompletionItemKind.Keyword,
    label: keyword,
    range
  }));
}

function createRuntimeFunctionSuggestions(
  monaco: typeof Monaco,
  context: MicroflowSqlScriptLanguageContext,
  beforeCursor: string,
  range: Monaco.IRange
): Monaco.languages.CompletionItem[] {
  const catalog = context.functionCatalog;
  const namespaceMatch = /(?<namespace>[A-Za-z_][\w]*Fns)\s*\.\s*[A-Za-z_]*$/i.exec(beforeCursor);
  if (namespaceMatch?.groups?.namespace) {
    const namespaceName = namespaceMatch.groups.namespace;
    return (catalog?.functions ?? [])
      .filter((fn) => fn.namespace.toLowerCase() === namespaceName.toLowerCase())
      .map((fn) => createFunctionCompletion(monaco, fn, range, false));
  }

  const namespaceSuggestions = runtimeExpressionFunctionNamespaces.map((namespaceName) => ({
    detail: '表达式函数命名空间',
    insertText: `${namespaceName}.`,
    kind: monaco.languages.CompletionItemKind.Module,
    label: namespaceName,
    range,
    sortText: `0_${namespaceName}`
  }));
  const functionSuggestions = (catalog?.functions ?? []).map((fn) => createFunctionCompletion(monaco, fn, range, true));
  return [...namespaceSuggestions, ...functionSuggestions];
}

function createFunctionCompletion(
  monaco: typeof Monaco,
  fn: RuntimeExpressionFunctionDefinitionDto,
  range: Monaco.IRange,
  includeNamespace: boolean
): Monaco.languages.CompletionItem {
  return {
    detail: buildRuntimeExpressionFunctionSignature(fn),
    documentation: [
      fn.description,
      ...fn.examples.map((example) => `示例：\`${example}\``)
    ].filter(Boolean).join('\n\n'),
    insertText: buildRuntimeExpressionFunctionSnippet(fn, { includeNamespace }),
    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
    kind: monaco.languages.CompletionItemKind.Function,
    label: includeNamespace ? fn.qualifiedName : fn.functionName,
    range,
    sortText: `1_${fn.qualifiedName}`
  };
}

function createVariableSuggestions(
  monaco: typeof Monaco,
  context: MicroflowSqlScriptLanguageContext,
  range: Monaco.IRange
): Monaco.languages.CompletionItem[] {
  const parameters = context.script.parameters.map((parameter) => ({
    detail: 'SQL 参数',
    label: `@${parameter.name.replace(/^@+/, '')}`,
    type: parameter.dataType
  }));
  const locals = context.script.localVariables.map((variable) => ({
    detail: '局部变量',
    label: `@${variable.name.replace(/^@+/, '')}`,
    type: variable.dataType
  }));
  const references = context.references.map((reference) => ({
    detail: reference.description,
    label: reference.label,
    type: reference.valueType
  }));

  return [...microflowSqlScriptBuiltInVariables, ...parameters, ...locals, ...references]
    .filter((item) => item.label.trim())
    .map((item) => ({
      detail: `${item.detail} / ${item.type}`,
      insertText: item.label,
      kind: item.label.startsWith('@') ? monaco.languages.CompletionItemKind.Variable : monaco.languages.CompletionItemKind.Field,
      label: item.label,
      range
    }));
}

function createTableSuggestions(
  monaco: typeof Monaco,
  context: MicroflowSqlScriptLanguageContext,
  beforeCursor: string,
  range: Monaco.IRange
): Monaco.languages.CompletionItem[] {
  if (!/\b(from|join|update|truncate(?:\s+table)?|insert\s+into|delete\s+from)\s+[A-Za-z_0-9.]*$/i.test(beforeCursor)) {
    return [];
  }

  return listSqlTableIdentifiers(context.tables).map((table) => ({
    detail: `${table.objectName || table.objectCode} / 本连接数据库表`,
    insertText: table.identifier,
    kind: monaco.languages.CompletionItemKind.Struct,
    label: table.identifier,
    range
  }));
}

function createDotSuggestions(
  monaco: typeof Monaco,
  context: MicroflowSqlScriptLanguageContext,
  beforeCursor: string,
  range: Monaco.IRange
): Monaco.languages.CompletionItem[] {
  if (!beforeCursor.endsWith('.')) {
    return [];
  }

  const root = /([A-Za-z_][\w]*(?:\[[0-9]+\])?)\.$/.exec(beforeCursor)?.[1]?.replace(/\[[0-9]+\]$/, '');
  if (!root) {
    return [];
  }

  const referenceSuggestions = context.references
    .filter((option) => option.sourceVariableCode.toLowerCase() === root.toLowerCase() && option.isField)
    .map((option) => ({
      detail: option.description,
      insertText: option.field?.fieldCode ?? option.label.split('.').pop() ?? '',
      kind: monaco.languages.CompletionItemKind.Field,
      label: option.field?.fieldName || option.field?.fieldCode || option.label,
      range
    }));
  const table = resolveSqlTableFromRoot(context.tables, beforeCursor, root);
  const tableFieldSuggestions = table
    ? table.fields
      .filter((field) => field.fieldCode.trim())
      .map((field) => ({
        detail: `${table.objectName || table.objectCode} / ${field.dataType}`,
        insertText: field.fieldCode.trim(),
        kind: monaco.languages.CompletionItemKind.Field,
        label: field.fieldName || field.fieldCode,
        range
      }))
    : [];

  return [...referenceSuggestions, ...tableFieldSuggestions];
}

function createResultFieldSuggestions(
  monaco: typeof Monaco,
  context: MicroflowSqlScriptLanguageContext,
  range: Monaco.IRange
): Monaco.languages.CompletionItem[] {
  return context.script.resultShape.fields.map((field) => ({
    detail: `SQL 结果字段 / ${field.dataType}`,
    insertText: field.fieldCode,
    kind: monaco.languages.CompletionItemKind.Field,
    label: `sqlResult.${field.fieldCode}`,
    range
  }));
}

function createMarker(
  monaco: typeof Monaco,
  model: Monaco.editor.ITextModel,
  startIndex: number,
  length: number,
  message: string
): Monaco.editor.IMarkerData {
  const start = model.getPositionAt(startIndex);
  const end = model.getPositionAt(startIndex + length);
  return {
    endColumn: end.column,
    endLineNumber: end.lineNumber,
    message,
    severity: monaco.MarkerSeverity.Error,
    startColumn: start.column,
    startLineNumber: start.lineNumber
  };
}

function maskSqlTrivia(value: string): string {
  return value
    .replace(/'(?:''|[^'])*'/g, (match) => ' '.repeat(match.length))
    .replace(/--[^\r\n]*/g, (match) => ' '.repeat(match.length))
    .replace(/\/\*[\s\S]*?\*\//g, (match) => ' '.repeat(match.length));
}

function readFunctionCallSpan(value: string, startIndex: number): { endIndex: number; openParenIndex: number } | null {
  const openParenIndex = value.indexOf('(', startIndex);
  if (openParenIndex < 0) {
    return null;
  }

  let depth = 0;
  for (let index = openParenIndex; index < value.length; index += 1) {
    const char = value[index];
    if (char === '(') {
      depth += 1;
    } else if (char === ')') {
      depth -= 1;
      if (depth === 0) {
        return { endIndex: index, openParenIndex };
      }
    }
  }

  return null;
}

function splitFunctionArguments(value: string): string[] {
  const args: string[] = [];
  let current = '';
  let depth = 0;
  let quote: '"' | "'" | null = null;
  for (let index = 0; index < value.length; index += 1) {
    const char = value[index];
    if (quote) {
      current += char;
      if (char === quote) {
        if (quote === "'" && value[index + 1] === "'") {
          current += value[index + 1];
          index += 1;
          continue;
        }

        quote = null;
      }
      continue;
    }

    if (char === "'" || char === '"') {
      quote = char;
      current += char;
      continue;
    }

    if (char === '(') {
      depth += 1;
    } else if (char === ')') {
      depth = Math.max(0, depth - 1);
    }

    if (char === ',' && depth === 0) {
      if (current.trim()) {
        args.push(current.trim());
      }
      current = '';
      continue;
    }

    current += char;
  }

  if (current.trim()) {
    args.push(current.trim());
  }

  return args;
}

function isBareSqlColumnArgument(value: string, knownNamespaces: Set<string>): boolean {
  const trimmed = value.trim();
  if (!/^[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)?$/.test(trimmed)) {
    return false;
  }

  const root = trimmed.split('.')[0].toLowerCase();
  return !knownNamespaces.has(root) && !['null', 'true', 'false'].includes(root);
}

function buildRbacFunctionArgumentDiagnostics(
  script: string,
  args: string[],
  argsStartIndex: number,
  qualifiedName: string
): MicroflowSqlScriptFunctionDiagnostic[] {
  const diagnostics: MicroflowSqlScriptFunctionDiagnostic[] = [];
  for (const arg of args) {
    const offset = script.indexOf(arg, argsStartIndex);
    const literal = readSqlStringLiteral(arg);
    if (literal === null) {
      diagnostics.push({
        length: arg.length,
        message: `${qualifiedName} 只允许使用字符串字面量权限或角色编码`,
        startIndex: offset >= 0 ? offset : argsStartIndex
      });
      continue;
    }

    if (!/^[A-Za-z0-9:_.*-]+$/.test(literal.trim())) {
      diagnostics.push({
        length: arg.length,
        message: `${qualifiedName} 参数包含非法权限或角色编码`,
        startIndex: offset >= 0 ? offset : argsStartIndex
      });
    }
  }

  return diagnostics;
}

function readSqlStringLiteral(value: string): string | null {
  const trimmed = value.trim();
  if (/^'(?:''|[^'])*'$/.test(trimmed)) {
    return trimmed.slice(1, -1).replace(/''/g, "'");
  }

  if (/^"(?:[^"])*"$/.test(trimmed)) {
    return trimmed.slice(1, -1);
  }

  return null;
}

function findFunctionAtPosition(
  model: Monaco.editor.ITextModel,
  position: Monaco.Position,
  catalog: RuntimeExpressionFunctionCatalogResponse | null | undefined
): RuntimeExpressionFunctionDefinitionDto | null {
  const line = model.getLineContent(position.lineNumber);
  const offset = position.column - 1;
  for (const match of line.matchAll(/\b(?<namespace>[A-Za-z_][\w]*Fns)\s*\.\s*(?<name>[A-Za-z_][\w]*)\b/g)) {
    const start = match.index ?? 0;
    const end = start + match[0].length;
    if (offset < start || offset > end) {
      continue;
    }

    return findRuntimeExpressionFunction(catalog, `${match.groups?.namespace}.${match.groups?.name}`);
  }

  return null;
}

function findActiveFunctionCall(
  model: Monaco.editor.ITextModel,
  position: Monaco.Position,
  catalog: RuntimeExpressionFunctionCatalogResponse | null | undefined
): { activeParameter: number; fn: RuntimeExpressionFunctionDefinitionDto } | null {
  const text = model.getValueInRange({
    endColumn: position.column,
    endLineNumber: position.lineNumber,
    startColumn: 1,
    startLineNumber: 1
  });
  let result: { activeParameter: number; fn: RuntimeExpressionFunctionDefinitionDto } | null = null;
  for (const match of text.matchAll(/\b(?<namespace>[A-Za-z_][\w]*Fns)\s*\.\s*(?<name>[A-Za-z_][\w]*)\s*\(/g)) {
    const fn = findRuntimeExpressionFunction(catalog, `${match.groups?.namespace}.${match.groups?.name}`);
    if (!fn) {
      continue;
    }

    const openParenIndex = (match.index ?? 0) + match[0].lastIndexOf('(');
    const activeParameter = readActiveParameter(text.slice(openParenIndex + 1));
    if (activeParameter === null) {
      continue;
    }

    result = { activeParameter, fn };
  }

  return result;
}

function readActiveParameter(value: string): number | null {
  let depth = 0;
  let parameter = 0;
  for (const char of value) {
    if (char === '(') {
      depth += 1;
      continue;
    }

    if (char === ')') {
      if (depth === 0) {
        return null;
      }

      depth -= 1;
      continue;
    }

    if (char === ',' && depth === 0) {
      parameter += 1;
    }
  }

  return parameter;
}

function splitSqlColumns(value: string): string[] {
  const columns: string[] = [];
  let current = '';
  let depth = 0;
  for (const char of value) {
    if (char === '(') {
      depth += 1;
    } else if (char === ')') {
      depth = Math.max(0, depth - 1);
    }

    if (char === ',' && depth === 0) {
      columns.push(current.trim());
      current = '';
      continue;
    }

    current += char;
  }

  if (current.trim()) {
    columns.push(current.trim());
  }

  return columns;
}

function inferFieldFromSqlColumn(column: string, index: number): MicroflowDomainField | null {
  const alias = /\bas\s+(?<alias>[A-Za-z_][\w]*)$/i.exec(column)?.groups?.alias
    ?? /(?<alias>[A-Za-z_][\w]*)$/.exec(column)?.groups?.alias
    ?? `field_${index + 1}`;
  const fieldCode = alias.trim();
  if (!fieldCode || fieldCode === '*') {
    return null;
  }

  return {
    ...cloneMicroflowField({
      dataType: inferColumnType(column),
      fieldCode,
      fieldName: fieldCode,
      required: false,
      visible: true,
      writable: false
    }),
    expression: createSqlResultFieldExpression({
      dataType: inferColumnType(column),
      fieldCode,
      fieldName: fieldCode,
      visible: true,
      writable: false
    })
  };
}

function inferColumnType(column: string): string {
  if (/\b(count|sum|avg|min|max)\s*\(/i.test(column) || /\b\d+(?:\.\d+)?\b/.test(column)) {
    return 'number';
  }

  if (/\b(true|false)\b/i.test(column)) {
    return 'boolean';
  }

  return 'string';
}

function listSqlTableIdentifiers(tables: MicroflowDomainObject[]): Array<MicroflowDomainObject & { identifier: string }> {
  const result: Array<MicroflowDomainObject & { identifier: string }> = [];
  const seen = new Set<string>();
  for (const table of tables) {
    for (const identifier of [table.modelCode, table.objectCode]) {
      const normalized = identifier.trim();
      const key = normalized.toLowerCase();
      if (!normalized || seen.has(key)) {
        continue;
      }

      seen.add(key);
      result.push({ ...table, identifier: normalized });
    }
  }

  return result;
}

function resolveSqlTableFromRoot(
  tables: MicroflowDomainObject[],
  beforeCursor: string,
  root: string
): MicroflowDomainObject | null {
  const directTable = tables.find((table) => matchesSqlTableIdentifier(table, root));
  if (directTable) {
    return directTable;
  }

  const aliases = parseSqlTableAliases(beforeCursor, tables);
  return aliases.get(root.toLowerCase()) ?? null;
}

function parseSqlTableAliases(
  sql: string,
  tables: MicroflowDomainObject[]
): Map<string, MicroflowDomainObject> {
  const aliases = new Map<string, MicroflowDomainObject>();
  const tablePattern = /\b(?:from|join)\s+(?<table>[A-Za-z_][\w.]*)(?:\s+(?:as\s+)?(?<alias>[A-Za-z_][\w]*))?/gi;
  for (const match of sql.matchAll(tablePattern)) {
    const tableName = match.groups?.table ?? '';
    const alias = match.groups?.alias ?? '';
    const table = tables.find((item) => matchesSqlTableIdentifier(item, tableName));
    if (!table) {
      continue;
    }

    if (alias && !sqlKeywords.some((keyword) => keyword.toLowerCase() === alias.toLowerCase())) {
      aliases.set(alias.toLowerCase(), table);
    }
  }

  return aliases;
}

function matchesSqlTableIdentifier(table: MicroflowDomainObject, value: string): boolean {
  const normalized = value.trim().toLowerCase();
  return Boolean(normalized) && [table.modelCode, table.objectCode].some((identifier) => identifier.trim().toLowerCase() === normalized);
}
