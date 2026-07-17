import {
  Braces,
  ChevronDown,
  ChevronRight,
  Database,
  FunctionSquare,
  Layers3,
  ListTree,
  Search,
  Type,
  X
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';

import type { MicroflowValueExpression } from '../../../../api/application-data-center/applicationDataCenter.types';
import type {
  RuntimeExpressionFunctionCatalogResponse,
  RuntimeExpressionFunctionParameterDto
} from '../../../../api/runtime/runtimeExpressionFunctions.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import {
  buildRuntimeExpressionFunctionSignature,
  groupRuntimeExpressionFunctions
} from '../../../../shared/runtime/expression-functions/runtimeExpressionFunctionCatalog';
import { useRuntimeExpressionFunctionCatalog } from '../../../../shared/runtime/expression-functions/useRuntimeExpressionFunctionCatalog';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';

import type { MicroflowNodeReferenceOption } from './microflowNodeContext';
import {
  microflowVariableValueTypeOptions,
  normalizeVariableValueType,
  type MicroflowVariableValueType
} from './microflowVariableSchema';

interface MicroflowExpressionBuilderProps {
  allowConstant?: boolean;
  expression: MicroflowValueExpression;
  helperTitle?: string;
  label: string;
  references: MicroflowNodeReferenceOption[];
  required?: boolean;
  onChange: (expression: MicroflowValueExpression) => void;
}

interface VariableTreeGroup {
  id: string;
  label: string;
  nodes: VariableTreeNode[];
}

interface VariableTreeNode {
  children: VariableTreeNode[];
  description: string;
  expression: MicroflowValueExpression | null;
  id: string;
  label: string;
  option: MicroflowNodeReferenceOption | null;
  valueType: MicroflowVariableValueType;
}

interface FunctionOperation {
  description: string;
  functionId: string;
  label: string;
  parameters: RuntimeExpressionFunctionParameterDto[];
  requiresInput: boolean;
  resultType: MicroflowVariableValueType;
  signature: string;
}

interface FunctionOperationGroup {
  label: string;
  operations: FunctionOperation[];
}

const literalSourceId = '__literal__';

const sourceOrder = ['input', 'variable', 'local', 'node', 'sqlResult', 'output'] as const;

const sourceLabels: Record<string, string> = {
  input: '输入变量',
  literal: '常量',
  local: '局部变量',
  node: '上游输出',
  output: '输出变量',
  sqlResult: 'SQL 结果',
  variable: '全局变量'
};

export function MicroflowExpressionBuilder({
  allowConstant = true,
  expression,
  label,
  references,
  required = false,
  onChange
}: MicroflowExpressionBuilderProps) {
  const functionCatalogQuery = useRuntimeExpressionFunctionCatalog('all');
  const normalizedExpression = normalizeExpression(expression);
  const selectedReference = useMemo(
    () => references.find((option) => expressionMatches(createSelectableExpression(option), normalizedExpression)) ?? null,
    [normalizedExpression, references]
  );
  const groups = useMemo(() => buildVariableTreeGroups(references), [references]);
  const functionGroups = useMemo(
    () => buildFunctionOperationGroups(functionCatalogQuery.data),
    [functionCatalogQuery.data]
  );
  const initialSourceId = selectedReference ? getReferenceSourceGroup(selectedReference) : groups[0]?.id ?? (allowConstant ? literalSourceId : '');
  const [activeSourceId, setActiveSourceId] = useState(initialSourceId);
  const [expandedNodeIds, setExpandedNodeIds] = useState<Set<string>>(() => new Set());
  const [functionOpen, setFunctionOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [treeOpen, setTreeOpen] = useState(false);
  const resultType = normalizeVariableValueType(normalizedExpression.dataType);
  const visibleGroups = filterVariableTreeGroups(groups, query);
  const activeGroup = visibleGroups.find((group) => group.id === activeSourceId) ?? visibleGroups[0] ?? null;
  const labelText = describeExpression(normalizedExpression, selectedReference);
  const descriptionText = selectedReference?.description ?? describeExpressionKind(normalizedExpression);

  useEffect(() => {
    if (selectedReference) {
      setActiveSourceId(getReferenceSourceGroup(selectedReference));
    }
  }, [selectedReference]);

  return (
    <div className="microflow-expression-builder microflow-expression-builder--compact">
      <div className="microflow-expression-builder__caption">
        <span>{label}{required ? <em>*</em> : null}</span>
        <small>{normalizedExpression.kind === 'ref' ? '变量引用' : normalizedExpression.kind}</small>
      </div>
      <div className="microflow-expression-token-row">
        <button className="microflow-expression-token" type="button" onClick={() => setTreeOpen((value) => !value)}>
          <Braces size={13} />
          <span>{labelText}</span>
          <small>{descriptionText}</small>
        </button>
        <select className="microflow-expression-type-select" value={resultType} onChange={(event) => changeDataType(event.target.value)}>
          {microflowVariableValueTypeOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
        <button className="microflow-expression-fx" title={translateCurrentLiteral("函数处理")} type="button" onClick={() => setFunctionOpen((value) => !value)}>
          fx
        </button>
      </div>

      {treeOpen ? (
        <div className="microflow-variable-tree-popover">
          <div className="microflow-variable-tree-popover__sources">
            {allowConstant ? (
              <button
                className={activeSourceId === literalSourceId ? 'microflow-variable-tree-popover__source microflow-variable-tree-popover__source--active' : 'microflow-variable-tree-popover__source'}
                type="button"
                onClick={() => {
                  setActiveSourceId(literalSourceId);
                  onChange(createLiteralExpression('', resultType));
                }}
              >
                <Type size={13} />{translateCurrentLiteral("常量")}</button>
            ) : null}
            {visibleGroups.map((group) => (
              <button
                className={group.id === activeSourceId ? 'microflow-variable-tree-popover__source microflow-variable-tree-popover__source--active' : 'microflow-variable-tree-popover__source'}
                key={group.id}
                type="button"
                onClick={() => setActiveSourceId(group.id)}
              >
                {group.id === 'local' ? <Layers3 size={13} /> : <Database size={13} />}
                {group.label}
              </button>
            ))}
          </div>
          <div className="microflow-variable-tree-popover__body">
            <label className="microflow-variable-tree-popover__search">
              <Search size={13} />
              <input placeholder={translateCurrentLiteral("搜索变量、字段或节点")} value={query} onChange={(event) => setQuery(event.target.value)} />
            </label>
            {activeSourceId === literalSourceId ? (
              <LiteralEditor expression={normalizedExpression.kind === 'literal' ? normalizedExpression : createLiteralExpression('', resultType)} onChange={onChange} />
            ) : activeGroup && activeGroup.nodes.length > 0 ? (
              <div className="microflow-variable-tree">
                {activeGroup.nodes.map((nodeItem) => (
                  <VariableTreeNodeRow
                    expandedNodeIds={expandedNodeIds}
                    key={nodeItem.id}
                    node={nodeItem}
                    onPick={(nextExpression) => {
                      onChange(nextExpression);
                      setTreeOpen(false);
                    }}
                    onToggle={toggleExpanded}
                  />
                ))}
              </div>
            ) : (
              <div className="microflow-variable-tree-popover__empty">{translateCurrentLiteral("没有可选变量。请先配置上游输出或全局变量节点。")}</div>
            )}
          </div>
        </div>
      ) : null}

      {functionOpen ? (
        <div className="microflow-function-popover">
          <div className="microflow-function-popover__header">
            <span><FunctionSquare size={13} />{translateCurrentLiteral("函数处理")}</span>
            <button type="button" onClick={() => setFunctionOpen(false)}>
              <X size={13} />
            </button>
          </div>
          {functionGroups.map((group) => (
            <div className="microflow-function-popover__group" key={group.label}>
              <strong>{group.label}</strong>
              <div>
                {group.operations.map((operation) => (
                  <button key={`${group.label}:${operation.functionId}:${operation.signature}`} title={operation.signature} type="button" onClick={() => applyFunction(operation)}>
                    <span>{operation.label}</span>
                    <small>{operation.description}</small>
                  </button>
                ))}
              </div>
            </div>
          ))}
          {functionCatalogQuery.isLoading ? <div className="microflow-function-popover__empty">{translateCurrentLiteral("函数目录加载中")}</div> : null}
          {functionCatalogQuery.isError ? <div className="microflow-function-popover__empty">{getErrorMessage(functionCatalogQuery.error, '函数目录加载失败')}</div> : null}
          {!functionCatalogQuery.isLoading && !functionCatalogQuery.isError && functionGroups.length === 0 ? (
            <div className="microflow-function-popover__empty">{translateCurrentLiteral("暂无可用函数")}</div>
          ) : null}
        </div>
      ) : null}
    </div>
  );

  function toggleExpanded(nodeId: string) {
    setExpandedNodeIds((current) => {
      const next = new Set(current);
      if (next.has(nodeId)) {
        next.delete(nodeId);
      } else {
        next.add(nodeId);
      }

      return next;
    });
  }

  function changeDataType(dataType: string) {
    const nextDataType = normalizeVariableValueType(dataType);
    onChange({
      ...normalizedExpression,
      dataType: nextDataType,
      ref: normalizedExpression.ref ? { ...normalizedExpression.ref, dataType: nextDataType } : normalizedExpression.ref
    });
  }

  function applyFunction(operation: FunctionOperation) {
    const args = [
      ...(operation.requiresInput ? [normalizedExpression] : []),
      ...operation.parameters.map((parameter) => createDefaultFunctionArgument(parameter, selectedReference, references))
    ];

    onChange({
      args,
      dataType: operation.resultType,
      functionId: operation.functionId,
      kind: 'function'
    });
    setFunctionOpen(false);
  }
}

function VariableTreeNodeRow({
  expandedNodeIds,
  node,
  onPick,
  onToggle,
  depth = 0
}: {
  depth?: number;
  expandedNodeIds: Set<string>;
  node: VariableTreeNode;
  onPick: (expression: MicroflowValueExpression) => void;
  onToggle: (nodeId: string) => void;
}) {
  const hasChildren = node.children.length > 0;
  const expanded = depth === 0 || expandedNodeIds.has(node.id);
  return (
    <div className="microflow-variable-tree__node">
      <div className="microflow-variable-tree__row" style={{ paddingLeft: 8 + depth * 14 }}>
        <button
          className="microflow-variable-tree__toggle"
          disabled={!hasChildren}
          type="button"
          onClick={() => onToggle(node.id)}
        >
          {hasChildren ? expanded ? <ChevronDown size={12} /> : <ChevronRight size={12} /> : <span />}
        </button>
        <button
          className="microflow-variable-tree__pick"
          disabled={!node.expression}
          type="button"
          onClick={() => node.expression ? onPick(cloneExpression(node.expression)) : undefined}
        >
          <ListTree size={13} />
          <span>{node.label}</span>
          <small>{node.valueType}</small>
        </button>
      </div>
      {hasChildren && expanded ? (
        <div>
          {node.children.map((child) => (
            <VariableTreeNodeRow
              depth={depth + 1}
              expandedNodeIds={expandedNodeIds}
              key={child.id}
              node={child}
              onPick={onPick}
              onToggle={onToggle}
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}

function LiteralEditor({
  expression,
  onChange
}: {
  expression: MicroflowValueExpression;
  onChange: (expression: MicroflowValueExpression) => void;
}) {
  const dataType = normalizeVariableValueType(expression.dataType);
  const value = expression.value;
  if (dataType === 'boolean') {
    return (
      <select className="microflow-expression-literal" value={String(Boolean(value))} onChange={(event) => onChange({ ...expression, value: event.target.value === 'true' })}>
        <option value="true">true</option>
        <option value="false">false</option>
      </select>
    );
  }

  return (
    <input
      className="microflow-expression-literal"
      placeholder={dataType === 'json' || dataType === 'object' || dataType === 'array' ? '输入 JSON 常量' : '输入常量'}
      value={stringifyValue(value)}
      onChange={(event) => onChange({ ...expression, value: parseLiteralValue(event.target.value, dataType) })}
    />
  );
}

function buildVariableTreeGroups(references: MicroflowNodeReferenceOption[]): VariableTreeGroup[] {
  const groups = new Map<string, VariableTreeGroup>();
  const rootNodes = new Map<string, VariableTreeNode>();

  for (const option of references) {
    const groupId = getReferenceSourceGroup(option);
    const group = ensureGroup(groups, groupId);
    const rootId = `${groupId}:${option.sourceNodeId ?? ''}:${option.sourceVariableCode}`;
    const rootNode = ensureRootNode(rootNodes, group.nodes, rootId, option);
    if (!option.isField) {
      rootNode.expression = createSelectableExpression(option);
      rootNode.option = option;
      rootNode.valueType = option.valueType;
      rootNode.description = option.description;
      continue;
    }

    const fieldPath = readOptionFieldPath(option);
    const pathNodes = rootNode.valueType === 'array'
      ? [createArrayItemSegment(option), ...fieldPath]
      : fieldPath;
    appendPathNode(rootNode, pathNodes, option, createSelectableExpression(option));
  }

  return sourceOrder
    .map((sourceId) => groups.get(sourceId))
    .filter((group): group is VariableTreeGroup => Boolean(group && group.nodes.length > 0));
}

function buildFunctionOperationGroups(
  catalog: RuntimeExpressionFunctionCatalogResponse | null | undefined
): FunctionOperationGroup[] {
  return groupRuntimeExpressionFunctions(catalog)
    .map((group) => ({
      label: group.moduleName,
      operations: group.functions.map((fn) => ({
        description: fn.description,
        functionId: fn.canonicalName,
        label: fn.label || fn.functionName,
        parameters: fn.parameters,
        requiresInput: fn.requiresInput,
        resultType: normalizeVariableValueType(fn.returnType),
        signature: buildRuntimeExpressionFunctionSignature(fn)
      }))
    }))
    .filter((group) => group.operations.length > 0);
}

function createDefaultFunctionArgument(
  parameter: RuntimeExpressionFunctionParameterDto,
  selectedReference: MicroflowNodeReferenceOption | null,
  references: MicroflowNodeReferenceOption[]
): MicroflowValueExpression {
  if (parameter.defaultValue !== undefined && parameter.defaultValue !== null) {
    return createLiteralExpression(parameter.defaultValue, normalizeVariableValueType(parameter.dataType));
  }

  const normalizedName = parameter.name.toLowerCase();
  if (['field', 'fields', 'path', 'key'].includes(normalizedName)) {
    return createLiteralExpression(findDefaultFieldArgument(selectedReference, references), 'string');
  }

  const dataType = normalizeVariableValueType(parameter.dataType);
  return createLiteralExpression(createDefaultLiteralValue(dataType), dataType);
}

function ensureGroup(groups: Map<string, VariableTreeGroup>, groupId: string): VariableTreeGroup {
  const existing = groups.get(groupId);
  if (existing) {
    return existing;
  }

  const group = {
    id: groupId,
    label: sourceLabels[groupId] ?? groupId,
    nodes: []
  };
  groups.set(groupId, group);
  return group;
}

function ensureRootNode(
  rootNodes: Map<string, VariableTreeNode>,
  nodes: VariableTreeNode[],
  rootId: string,
  option: MicroflowNodeReferenceOption
): VariableTreeNode {
  const existing = rootNodes.get(rootId);
  if (existing) {
    return existing;
  }

  const rootNode: VariableTreeNode = {
    children: [],
    description: option.description,
    expression: option.isField ? null : createSelectableExpression(option),
    id: rootId,
    label: option.sourceVariableCode || option.label,
    option: option.isField ? null : option,
    valueType: normalizeVariableValueType(option.isField ? 'object' : option.valueType)
  };
  rootNodes.set(rootId, rootNode);
  nodes.push(rootNode);
  return rootNode;
}

function appendPathNode(
  rootNode: VariableTreeNode,
  segments: string[],
  option: MicroflowNodeReferenceOption,
  expression: MicroflowValueExpression
) {
  let current = rootNode;
  segments.forEach((segment, index) => {
    const isLeaf = index === segments.length - 1;
    const id = `${current.id}:${segment}`;
    let next = current.children.find((child) => child.id === id) ?? null;
    if (!next) {
      next = {
        children: [],
        description: isLeaf ? option.description : segment,
        expression: isLeaf ? expression : null,
        id,
        label: segment,
        option: isLeaf ? option : null,
        valueType: isLeaf ? option.valueType : 'object'
      };
      current.children.push(next);
    }

    if (isLeaf) {
      next.expression = expression;
      next.option = option;
      next.valueType = option.valueType;
      next.description = option.description;
    }

    current = next;
  });
}

function createArrayItemSegment(option: MicroflowNodeReferenceOption): string {
  return option.sourceVariableCode.endsWith('[]') ? 'item' : 'item';
}

function filterVariableTreeGroups(groups: VariableTreeGroup[], query: string): VariableTreeGroup[] {
  const keyword = query.trim().toLowerCase();
  if (!keyword) {
    return groups;
  }

  return groups
    .map((group) => ({
      ...group,
      nodes: group.nodes.map((node) => filterVariableTreeNode(node, keyword)).filter((node): node is VariableTreeNode => Boolean(node))
    }))
    .filter((group) => group.nodes.length > 0);
}

function filterVariableTreeNode(node: VariableTreeNode, keyword: string): VariableTreeNode | null {
  const children = node.children.map((child) => filterVariableTreeNode(child, keyword)).filter((child): child is VariableTreeNode => Boolean(child));
  const matched = `${node.label} ${node.description} ${node.valueType}`.toLowerCase().includes(keyword);
  if (!matched && children.length === 0) {
    return null;
  }

  return {
    ...node,
    children
  };
}

function getReferenceSourceGroup(option: MicroflowNodeReferenceOption): string {
  if (option.id.startsWith('local:')) {
    return 'local';
  }

  return option.sourceKind === 'variable' ? 'variable' : option.sourceKind;
}

function readOptionFieldPath(option: MicroflowNodeReferenceOption): string[] {
  const refPath = option.expression.kind === 'ref' && option.expression.ref
    ? option.expression.ref.fieldPath ?? []
    : [];
  if (refPath.length > 0) {
    return refPath;
  }

  const fieldCode = option.field?.fieldCode?.trim();
  return fieldCode ? fieldCode.split('.').filter(Boolean) : [];
}

function createSelectableExpression(option: MicroflowNodeReferenceOption): MicroflowValueExpression {
  const expression = cloneExpression(option.expression);
  if (expression.kind !== 'ref' || !expression.ref) {
    return expression;
  }

  const fieldPath = option.isField ? readOptionFieldPath(option) : expression.ref.fieldPath ?? [];
  return {
    ...expression,
    dataType: option.valueType,
    ref: {
      ...expression.ref,
      dataType: option.valueType,
      fieldPath,
      label: option.label,
      outputKey: option.sourceVariableCode || expression.ref.outputKey || expression.ref.variableId,
      variableId: option.sourceVariableCode || expression.ref.variableId
    }
  };
}

function normalizeExpression(expression: MicroflowValueExpression | null | undefined): MicroflowValueExpression {
  if (!expression || !expression.kind) {
    return createLiteralExpression('', 'string');
  }

  return {
    args: expression.args ?? [],
    dataType: normalizeVariableValueType(expression.dataType),
    functionId: expression.functionId ?? null,
    items: expression.items ?? [],
    kind: expression.kind,
    properties: expression.properties ?? {},
    ref: expression.ref ?? null,
    value: expression.value
  };
}

function createLiteralExpression(value: unknown, dataType: MicroflowVariableValueType): MicroflowValueExpression {
  return {
    dataType,
    kind: 'literal',
    value
  };
}

function createDefaultLiteralValue(valueType: MicroflowVariableValueType): unknown {
  if (valueType === 'array') {
    return [];
  }

  if (valueType === 'object' || valueType === 'json') {
    return {};
  }

  if (valueType === 'number') {
    return 0;
  }

  if (valueType === 'boolean') {
    return false;
  }

  return '';
}

function cloneExpression(expression: MicroflowValueExpression): MicroflowValueExpression {
  return JSON.parse(JSON.stringify(expression)) as MicroflowValueExpression;
}

function expressionMatches(left: MicroflowValueExpression, right: MicroflowValueExpression): boolean {
  if (left.kind !== 'ref' || right.kind !== 'ref' || !left.ref || !right.ref) {
    return false;
  }

  return left.ref.sourceType === right.ref.sourceType &&
    left.ref.variableId === right.ref.variableId &&
    (left.ref.outputKey ?? '') === (right.ref.outputKey ?? '') &&
    left.ref.fieldPath.join('.') === right.ref.fieldPath.join('.');
}

function describeExpression(expression: MicroflowValueExpression, reference: MicroflowNodeReferenceOption | null): string {
  if (expression.kind === 'literal') {
    return `常量 · ${expression.dataType}`;
  }

  if (expression.kind === 'function') {
    return `fx · ${expression.functionId ?? '函数'} · ${expression.dataType}`;
  }

  if (expression.kind === 'object') {
    return `对象 · ${Object.keys(expression.properties ?? {}).length} 字段`;
  }

  if (expression.kind === 'array') {
    return `数组 · ${(expression.items ?? []).length} 项`;
  }

  return reference ? reference.label : '选择变量、字段或常量';
}

function describeExpressionKind(expression: MicroflowValueExpression): string {
  if (expression.kind === 'template') {
    return '模板字符串';
  }

  if (expression.kind === 'function') {
    return '函数运算结果';
  }

  if (expression.kind === 'literal') {
    return '字面量常量';
  }

  return '未匹配到变量树，请重新选择';
}

function findDefaultFieldArgument(
  selectedReference: MicroflowNodeReferenceOption | null,
  references: MicroflowNodeReferenceOption[]
): string {
  if (selectedReference?.isField && selectedReference.field?.fieldCode) {
    return selectedReference.field.fieldCode;
  }

  const sibling = selectedReference
    ? references.find((option) => option.sourceVariableCode === selectedReference.sourceVariableCode && option.isField && option.field?.fieldCode)
    : references.find((option) => option.isField && option.field?.fieldCode);
  return sibling?.field?.fieldCode ?? '';
}

function stringifyValue(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  return typeof value === 'string' ? value : JSON.stringify(value);
}

function parseLiteralValue(value: string, dataType: MicroflowVariableValueType): unknown {
  if (dataType === 'number') {
    const numberValue = Number(value);
    return Number.isFinite(numberValue) ? numberValue : 0;
  }

  if (dataType === 'boolean') {
    return value === 'true';
  }

  if (dataType === 'array' || dataType === 'object' || dataType === 'json') {
    try {
      return JSON.parse(value);
    } catch {
      return value;
    }
  }

  return value;
}
