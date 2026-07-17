import { Check, ChevronDown, ChevronUp, Copy, Edit3, MoreHorizontal, Plus, Save, SlidersHorizontal, Trash2, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';

import type {
  MicroflowDefinition,
  MicroflowDomainField,
  MicroflowNode,
  MicroflowSqlScript,
  MicroflowVariable,
  MicroflowValueExpression
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { ResponsiveModal } from '../../../../shared/responsive/ResponsiveModal';

import {
  getVariableRootCode,
  normalizeMicroflowDefinitionForSave,
  normalizeSetVariableOutputFields
} from './microflowDefinitionNormalizer';
import { MicroflowExpressionBuilder } from './MicroflowExpressionBuilder';
import {
  createDefaultGlobalVariable,
  globalVariablesNodeType,
  readGlobalVariableNodeVariables,
  writeGlobalVariableNodeVariables
} from './microflowGlobalVariableNode';
import {
  applyReturnOutputSchema,
  listNodeInputReferenceOptions,
  listNodeOutputSchemaOptions,
  readNodeOutputSchema,
  readReturnOutputSchema,
  validateReturnOutputSchema,
  type MicroflowContextVariable,
  type MicroflowNodeReferenceOption
} from './microflowNodeContext';
import {
  cloneMicroflowField,
  createMicroflowVariableField,
  microflowFieldDataTypeOptions,
  normalizeVariableValueType
} from './microflowVariableSchema';
import { MicroflowVariableSchemaEditor } from './MicroflowVariableSchemaEditor';
import { cloneSqlScript, normalizeSqlScriptForSave, ReturnSqlScriptEditor } from './ReturnSqlScriptEditor';

interface MicroflowNodeConfigEditorProps {
  definition: MicroflowDefinition;
  microflowId?: string | null;
  node: MicroflowNode | null;
  onClose: () => void;
  onSave: (definition: MicroflowDefinition) => void;
  open: boolean;
  variant?: 'dialog' | 'panel';
}

interface EditableReturnField extends MicroflowDomainField {
  selected: boolean;
  sourceField: boolean;
}

type DecisionConditionMode = 'all' | 'any';

type ReturnOutputMode = 'fields' | 'sqlScript';

interface DecisionConditionRule {
  id: string;
  leftExpression: MicroflowValueExpression;
  operator: string;
  rightExpression: MicroflowValueExpression;
}

const nodeTypeLabels: Record<string, string> = {
  callApi: '调用接口',
  change: '更新数据',
  compositeCreate: '保存主从',
  compositeDelete: '级联删除',
  compositeDetail: '主从详情',
  compositeUpdate: '更新主从',
  create: '新增数据',
  decision: '条件分支',
  delete: '删除数据',
  detail: '详情查询',
  end: '结束',
  globalVariables: '全局变量',
  loop: '循环',
  query: '列表查询',
  retrieve: '列表读取',
  return: '返回',
  setVariable: '设置变量'
};

const operatorOptions = [
  ['equals', '等于'],
  ['notEquals', '不等于'],
  ['contains', '包含'],
  ['gt', '大于'],
  ['gte', '大于等于'],
  ['lt', '小于'],
  ['lte', '小于等于'],
  ['between', '区间']
] as const;

const httpMethodOptions = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] as const;

const nodeConfigTabs = ['基础信息', '输入配置', '条件判断', '输出结果', '高级设置'] as const;

export function MicroflowNodeConfigEditor({
  definition,
  microflowId,
  node,
  onClose,
  onSave,
  open,
  variant = 'dialog'
}: MicroflowNodeConfigEditorProps) {
  const message = useMessage();
  const [draftNodeState, setDraftNode] = useState<MicroflowNode | null>(null);
  const [returnContextId, setReturnContextId] = useState('');
  const [returnOutputCode, setReturnOutputCode] = useState('');
  const [returnOutputName, setReturnOutputName] = useState('');
  const [returnOutputType, setReturnOutputType] = useState('object');
  const [returnOutputMode, setReturnOutputMode] = useState<ReturnOutputMode>('fields');
  const [returnFields, setReturnFields] = useState<EditableReturnField[]>([]);
  const [returnSqlScript, setReturnSqlScript] = useState<MicroflowSqlScript>(() => createEmptySqlScript());
  const [activeConfigTab, setActiveConfigTab] = useState<(typeof nodeConfigTabs)[number]>('基础信息');

  const referenceOptions = useMemo(
    () => node ? listNodeInputReferenceOptions(definition, node.id) : [],
    [definition, node]
  );
  const returnSchema = useMemo(
    () => node?.type === 'return' ? readReturnOutputSchema(definition, node) : null,
    [definition, node]
  );
  const returnContexts = useMemo(
    () => buildReturnContexts(definition, node, returnSchema),
    [definition, node, returnSchema]
  );
  const selectedReturnContext = returnContexts.find((context) => context.id === returnContextId) ?? null;
  const returnReferenceOptions = useMemo(
    () => createReturnReferenceOptions(referenceOptions, selectedReturnContext),
    [referenceOptions, selectedReturnContext]
  );
  const selectedReturnFields = returnFields.filter((field) => field.selected);

  useEffect(() => {
    if (!open || !node) {
      setDraftNode(null);
      return;
    }

    const nextDraft = cloneNode(node);
    setDraftNode(nextDraft);
    setActiveConfigTab(defaultNodeConfigTab(node.type));
    if (node.type !== 'return') {
      return;
    }

    const matchedContext = findInitialReturnContext(returnContexts, returnSchema);
    setReturnContextId(matchedContext?.id ?? '');
    setReturnOutputCode(returnSchema?.variableCode || matchedContext?.variableCode || 'returnResult');
    setReturnOutputName(returnSchema?.variableName || matchedContext?.variableName || matchedContext?.variableCode || '');
    setReturnOutputType(returnSchema?.valueType || matchedContext?.valueType || 'object');
    setReturnOutputMode(returnSchema?.sourceMode ?? (returnSchema?.sqlScript ? 'sqlScript' : 'fields'));
    setReturnSqlScript(returnSchema?.sqlScript ? cloneSqlScript(returnSchema.sqlScript) : createEmptySqlScript());
    setReturnFields(buildEditableReturnFields(matchedContext, returnSchema?.fields ?? []));
  }, [node, open, returnContexts, returnSchema]);

  if (!node || !draftNodeState) {
    return null;
  }
  const draftNode = draftNodeState;
  const content = (
    <div className="microflow-node-config-editor__shell">
      <nav className="microflow-node-config-tabs" aria-label="节点配置分区">
        {nodeConfigTabs.map((tab) => (
          <button
            className={tab === activeConfigTab ? 'microflow-node-config-tabs__item microflow-node-config-tabs__item--active' : 'microflow-node-config-tabs__item'}
            key={tab}
            type="button"
            onClick={() => setActiveConfigTab(tab)}
          >
            {tab}
            {tab === '条件判断' && draftNode.type === 'decision' ? <em>2</em> : null}
          </button>
        ))}
      </nav>

      {renderActiveConfigTab()}

      <footer className="microflow-node-config-editor__actions">
        <span className="microflow-node-config-editor__action-note">{translateCurrentLiteral("取消将丢弃本次未保存的节点配置")}</span>
        <div className="microflow-node-config-editor__action-buttons">
          <button className="secondary-button h-8 text-xs" type="button" onClick={onClose}>
            <X className="h-3.5 w-3.5" />{translateCurrentLiteral("取消")}</button>
          <button className="primary-button h-8 text-xs" type="button" onClick={save}>
            <Save className="h-3.5 w-3.5" />{translateCurrentLiteral("保存节点")}</button>
        </div>
      </footer>
    </div>
  );

  if (variant === 'panel') {
    return (
      <aside className="microflow-node-config-panel">
        <header className="microflow-node-config-panel__header">
          <div className="microflow-node-config-panel__title">
            <span className="microflow-node-config-panel__icon">
              <SlidersHorizontal size={17} />
            </span>
            <div className="microflow-node-config-panel__copy">
              <div className="microflow-node-config-panel__name-row">
                <strong>{draftNode.name || nodeTypeLabels[draftNode.type] || '节点配置'}</strong>
                <button className="microflow-node-config-panel__edit" title={translateCurrentLiteral("编辑名称")} type="button" onClick={() => setActiveConfigTab('基础信息')}>
                  <Edit3 size={13} />
                </button>
              </div>
              <span>{nodeDescription(draftNode.type)}</span>
            </div>
          </div>
          <div className="microflow-node-config-panel__actions">
            <label className="microflow-node-config-switch">
              <input checked readOnly type="checkbox" />
              <span><Check size={11} /></span>{translateCurrentLiteral("启用")}</label>
            <button className="microflow-node-config-panel__tool" title={translateCurrentLiteral("复制节点")} type="button">
              <Copy size={14} />
            </button>
            <button className="microflow-node-config-panel__tool" title={translateCurrentLiteral("更多")} type="button">
              <MoreHorizontal size={15} />
            </button>
            <button className="microflow-node-config-panel__tool" title={translateCurrentLiteral("关闭配置")} type="button" onClick={onClose}>
              <X size={15} />
            </button>
          </div>
        </header>
        {content}
      </aside>
    );
  }

  return (
    <ResponsiveModal
      bodyClassName="microflow-node-config-editor__body"
      className="microflow-node-config-editor"
      maxWidth={820}
      mode="drawer"
      open={open}
      title={`${draftNode.name || nodeTypeLabels[draftNode.type] || '节点'} · 节点配置`}
      onClose={onClose}
    >
      {content}
    </ResponsiveModal>
  );

  function renderActiveConfigTab() {
    if (activeConfigTab === '基础信息') {
      return renderBasicInfoConfig();
    }

    if (activeConfigTab === '输入配置') {
      return renderInputConfig();
    }

    if (activeConfigTab === '条件判断') {
      return renderConditionTab();
    }

    if (activeConfigTab === '输出结果') {
      return renderOutputResultConfig();
    }

    return renderAdvancedSettingsConfig();
  }

  function renderBasicInfoConfig() {
    return (
      <section className="microflow-node-config-section">
        <h4>{translateCurrentLiteral("基础信息")}</h4>
        <div className="microflow-node-config-grid">
          <TextField label="节点名称" value={draftNode.name} onChange={(name) => patchDraft({ name })} />
          <TextField disabled label="节点类型" value={nodeTypeLabels[draftNode.type] || draftNode.type} onChange={() => undefined} />
        </div>
        <p className="microflow-node-config-muted">{nodeDescription(draftNode.type)}</p>
      </section>
    );
  }

  function renderInputConfig() {
    if (draftNode.type === 'decision' || draftNode.type === 'return') {
      return renderReferenceSourcesConfig();
    }

    return renderNodeConfig();
  }

  function renderConditionTab() {
    if (draftNode.type === 'decision') {
      return renderDecisionConfig();
    }

    return (
      <section className="microflow-node-config-section">
        <h4>{translateCurrentLiteral("条件判断")}</h4>
        <div className="microflow-node-config-empty">{translateCurrentLiteral("当前节点不产生分支条件；需要条件路由时请使用 Decision 节点。")}</div>
      </section>
    );
  }

  function renderOutputResultConfig() {
    if (draftNode.type === 'return') {
      return renderReturnConfig();
    }

    const outputSchemaConfig = renderOutputSchemaConfig();
    if (outputSchemaConfig) {
      return outputSchemaConfig;
    }

    return (
      <section className="microflow-node-config-section">
        <h4>{translateCurrentLiteral("输出结果")}</h4>
        <div className="microflow-node-config-empty">{translateCurrentLiteral("当前节点没有可声明的输出结构。")}</div>
      </section>
    );
  }

  function renderAdvancedSettingsConfig() {
    return (
      <section className="microflow-node-config-section">
        <h4>{translateCurrentLiteral("高级设置")}</h4>
        <div className="microflow-node-config-grid microflow-node-config-grid--three">
          <TextField disabled label="节点 ID" value={draftNode.id} onChange={() => undefined} />
          <NumberField label="X" min={-10000} value={Math.round(draftNode.x)} onChange={(x) => patchDraft({ x })} />
          <NumberField label="Y" min={-10000} value={Math.round(draftNode.y)} onChange={(y) => patchDraft({ y })} />
        </div>
        <p className="microflow-node-config-muted">{translateCurrentLiteral("高级设置只维护布局与节点标识；业务参数请在输入、条件、输出页签内配置。")}</p>
      </section>
    );
  }

  function renderReferenceSourcesConfig() {
    return (
      <section className="microflow-node-config-section">
        <h4>{translateCurrentLiteral("输入来源")}</h4>
        {referenceOptions.length > 0 ? (
          <div className="microflow-node-config-reference-list">
            {referenceOptions.slice(0, 18).map((option) => (
              <span className="microflow-node-config-reference-chip" key={option.id} title={`${option.label} / ${option.valueType}`}>
                {option.label}
                <em>{option.valueType}</em>
              </span>
            ))}
            {referenceOptions.length > 18 ? <span className="microflow-node-config-reference-chip">+{referenceOptions.length - 18}</span> : null}
          </div>
        ) : (
          <div className="microflow-node-config-empty">{translateCurrentLiteral("当前节点暂无上游变量。连接上游节点或新增全局变量后，可在表达式选择器中引用。")}</div>
        )}
      </section>
    );
  }

  function renderNodeConfig() {
    if (draftNode.type === 'end') {
      return (
        <section className="microflow-node-config-section">
          <h4>{translateCurrentLiteral("结束节点")}</h4>
          <p className="microflow-node-config-muted">{translateCurrentLiteral("结束节点不配置输入参数；需要返回数据时请使用 Return 节点。")}</p>
        </section>
      );
    }

    if (draftNode.type === 'return') {
      return renderReturnConfig();
    }

    if (draftNode.type === globalVariablesNodeType) {
      return (
        <GlobalVariablesEditor
          definition={definition}
          node={draftNode}
          onChange={(variables) => patchConfig({ variables })}
        />
      );
    }

    if (draftNode.type === 'query' || draftNode.type === 'retrieve') {
      const modelFields = findModelFields(definition, String(draftNode.config.modelCode ?? ''));
      return (
        <>
          <section className="microflow-node-config-section">
            <h4>{translateCurrentLiteral("查询")}</h4>
            <div className="microflow-node-config-grid microflow-node-config-grid--four">
              <TextField label="模型" value={String(draftNode.config.modelCode ?? '')} onChange={(modelCode) => patchConfig({ modelCode })} />
              <TextField label="输出变量" value={String(draftNode.config.targetVariable ?? '')} onChange={(targetVariable) => patchConfig({ targetVariable })} />
              <NumberField label="页码" value={Number(draftNode.config.pageIndex ?? 1)} onChange={(pageIndex) => patchConfig({ pageIndex })} />
              <NumberField label="页大小" value={Number(draftNode.config.pageSize ?? 20)} onChange={(pageSize) => patchConfig({ pageSize })} />
            </div>
            <TextField label="关键字" value={String(draftNode.config.keyword ?? '')} onChange={(keyword) => patchConfig({ keyword })} />
          </section>
          <FilterMappingsEditor
            filters={readRecordArray(draftNode.config.filters)}
            fieldOptions={modelFields}
            references={referenceOptions}
            onChange={(filters) => patchConfig({ filters })}
          />
        </>
      );
    }

    if (draftNode.type === 'detail' || draftNode.type === 'delete') {
      return renderModelWithIdConfig(draftNode.type === 'detail' ? '详情读取' : '删除');
    }

    if (draftNode.type === 'create' || draftNode.type === 'change') {
      const modelFields = findModelFields(definition, String(draftNode.config.modelCode ?? ''));
      return (
        <>
          {draftNode.type === 'change' ? renderModelWithIdConfig('更新目标') : (
            <section className="microflow-node-config-section">
              <h4>{translateCurrentLiteral("新增目标")}</h4>
              <div className="microflow-node-config-grid">
                <TextField label="模型" value={String(draftNode.config.modelCode ?? '')} onChange={(modelCode) => patchConfig({ modelCode })} />
                <TextField label="输出变量" value={String(draftNode.config.targetVariable ?? 'createdRow')} onChange={(targetVariable) => patchConfig({ targetVariable })} />
              </div>
            </section>
          )}
          <FieldMappingsEditor
            mappings={readRecordArray(draftNode.config.fieldMappings)}
            references={referenceOptions}
            targetFields={modelFields}
            title={translateCurrentLiteral("字段映射")}
            onChange={(fieldMappings) => patchConfig({ fieldMappings })}
          />
        </>
      );
    }

    if (draftNode.type === 'compositeCreate' || draftNode.type === 'compositeDetail' || draftNode.type === 'compositeUpdate' || draftNode.type === 'compositeDelete') {
      return renderCompositeConfig();
    }

    if (draftNode.type === 'decision') {
      return renderDecisionConfig();
    }

    if (draftNode.type === 'loop') {
      const nodeOptions = definition.nodes.filter((item) => item.id !== draftNode.id && item.type !== 'start' && item.type !== globalVariablesNodeType);
      return (
        <section className="microflow-node-config-section">
          <h4>{translateCurrentLiteral("循环")}</h4>
          <div className="microflow-node-config-grid">
            <TextField label="循环变量" value={String(draftNode.config.itemVariable ?? 'item')} onChange={(itemVariable) => patchConfig({ itemVariable })} />
            <label className="microflow-node-config-field">
              <span>{translateCurrentLiteral("循环体节点")}</span>
              <select className="form-select h-8 text-xs" value={String(draftNode.config.bodyNodeId ?? '')} onChange={(event) => patchConfig({ bodyNodeId: event.target.value })}>
                <option value="">{translateCurrentLiteral("不指定")}</option>
                {nodeOptions.map((item) => (
                  <option key={item.id} value={item.id}>{item.name || item.id}</option>
                ))}
              </select>
            </label>
          </div>
          <ExpressionPicker
            expression={toExpression(draftNode.config.collectionExpression, 'variables', 'array')}
            label="集合来源"
            references={referenceOptions.filter((option) => !option.isField && option.valueType === 'array')}
            onChange={(collectionExpression) => patchConfig({ collectionExpression })}
          />
        </section>
      );
    }

    if (draftNode.type === 'callApi') {
      return (
        <>
          <section className="microflow-node-config-section">
            <h4>{translateCurrentLiteral("接口")}</h4>
            <div className="microflow-node-config-grid microflow-node-config-grid--three">
              <TextField label="接口路径" value={String(draftNode.config.routePath ?? '')} onChange={(routePath) => patchConfig({ routePath })} />
              <label className="microflow-node-config-field">
                <span>{translateCurrentLiteral("方法")}</span>
                <select className="form-select h-8 text-xs" value={String(draftNode.config.httpMethod ?? 'GET')} onChange={(event) => patchConfig({ httpMethod: event.target.value })}>
                  {httpMethodOptions.map((method) => <option key={method} value={method}>{method}</option>)}
                </select>
              </label>
              <TextField label="输出变量" value={String(draftNode.config.targetVariable ?? 'apiResult')} onChange={(targetVariable) => patchConfig({ targetVariable })} />
            </div>
          </section>
          <FieldMappingsEditor allowCustomTarget mappings={readRecordArray(draftNode.config.queryMappings)} references={referenceOptions} targetFields={[]} title={translateCurrentLiteral("Query 映射")} onChange={(queryMappings) => patchConfig({ queryMappings })} />
          <section className="microflow-node-config-section">
            <h4>Body</h4>
            <ExpressionPicker expression={toExpression(draftNode.config.bodyExpression)} label="Body 表达式" references={referenceOptions} onChange={(bodyExpression) => patchConfig({ bodyExpression })} />
          </section>
          <FieldMappingsEditor allowCustomTarget mappings={readRecordArray(draftNode.config.bodyMappings)} references={referenceOptions} targetFields={[]} title={translateCurrentLiteral("Body 字段映射")} onChange={(bodyMappings) => patchConfig({ bodyMappings })} />
        </>
      );
    }

    if (draftNode.type === 'setVariable') {
      return (
        <section className="microflow-node-config-section">
          <h4>{translateCurrentLiteral("变量")}</h4>
          <VariableTargetPicker
            references={referenceOptions}
            value={String(draftNode.config.variableCode ?? '')}
            onChange={(variableCode) => patchConfig({ variableCode })}
          />
          <ExpressionPicker expression={toExpression(draftNode.config.valueExpression)} label="变量值" references={referenceOptions} onChange={(valueExpression) => patchConfig({ valueExpression })} />
        </section>
      );
    }

    return (
      <section className="microflow-node-config-section">
        <h4>{translateCurrentLiteral("配置")}</h4>
        <p className="microflow-node-config-muted">{translateCurrentLiteral("该节点类型暂无专用编辑器，仍可在右侧高级属性中维护。")}</p>
      </section>
    );
  }

  function renderOutputSchemaConfig() {
    if (!supportsOutputSchema(draftNode)) {
      return null;
    }

    const outputVariableCode = readDraftOutputVariableCode(draftNode);
    const schema = readNodeOutputSchema(definition, draftNode);
    const valueType = schema?.valueType ?? inferDraftOutputValueType(draftNode);
    const fields = schema?.fields ?? [];
    const fallbackFields = inferDraftOutputFields(definition, draftNode);

    return (
      <NodeOutputSchemaEditor
        fallbackFields={fallbackFields}
        fields={fields}
        valueType={valueType}
        variableCode={outputVariableCode}
        variableName={String(schema?.variableName ?? draftNode.name ?? outputVariableCode)}
        onChange={(nextSchema) => patchConfig({ outputSchema: nextSchema })}
      />
    );
  }

  function renderModelWithIdConfig(title: string) {
    return (
      <section className="microflow-node-config-section">
        <h4>{title}</h4>
        <div className="microflow-node-config-grid">
          <TextField label="模型" value={String(draftNode.config.modelCode ?? '')} onChange={(modelCode) => patchConfig({ modelCode })} />
          <TextField label="输出变量" value={String(draftNode.config.targetVariable ?? `${draftNode.type}Result`)} onChange={(targetVariable) => patchConfig({ targetVariable })} />
        </div>
        <ExpressionPicker
          expression={toExpression(draftNode.config.idExpression)}
          label="主键表达式"
          references={referenceOptions}
          onChange={(idExpression) => patchConfig({ idExpression })}
        />
      </section>
    );
  }

  function renderCompositeConfig() {
    const rootFields = findModelFields(definition, String(draftNode.config.rootModelCode ?? ''));
    const children = readRecordArray(draftNode.config.children);
    return (
      <>
        <section className="microflow-node-config-section">
          <h4>{translateCurrentLiteral("主对象")}</h4>
          <div className="microflow-node-config-grid">
            <TextField label="主模型" value={String(draftNode.config.rootModelCode ?? '')} onChange={(rootModelCode) => patchConfig({ rootModelCode })} />
            <TextField label="输出变量" value={String(draftNode.config.targetVariable ?? 'compositeResult')} onChange={(targetVariable) => patchConfig({ targetVariable })} />
          </div>
          {draftNode.type === 'compositeDetail' || draftNode.type === 'compositeUpdate' || draftNode.type === 'compositeDelete' ? (
            <ExpressionPicker expression={toExpression(draftNode.config.idExpression)} label="主键表达式" references={referenceOptions} onChange={(idExpression) => patchConfig({ idExpression })} />
          ) : null}
        </section>
        {draftNode.type === 'compositeCreate' || draftNode.type === 'compositeUpdate' ? (
          <FieldMappingsEditor mappings={readRecordArray(draftNode.config.fieldMappings)} references={referenceOptions} targetFields={rootFields} title={translateCurrentLiteral("主对象字段映射")} onChange={(fieldMappings) => patchConfig({ fieldMappings })} />
        ) : null}
        <CompositeChildrenEditor
          children={children}
          definition={definition}
          mode={draftNode.type}
          references={referenceOptions}
          onChange={(nextChildren) => patchConfig({ children: nextChildren })}
        />
      </>
    );
  }

  function renderReturnConfig() {
    return (
        <section className={returnOutputMode === 'sqlScript' ? 'microflow-node-config-section microflow-return-output-section microflow-return-output-section--sql' : 'microflow-node-config-section microflow-return-output-section'}>
          <div className="microflow-node-config-section__toolbar">
            <h4>{translateCurrentLiteral("输出结果")}</h4>
            <div className="microflow-return-source-mode">
              {([
                ['fields', '字段配置'],
                ['sqlScript', 'SQL 脚本']
              ] as const).map(([mode, label]) => (
                <button
                  className={returnOutputMode === mode ? 'microflow-return-source-mode__item microflow-return-source-mode__item--active' : 'microflow-return-source-mode__item'}
                  key={mode}
                  type="button"
                  onClick={() => setReturnOutputMode(mode)}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
          {returnOutputMode === 'fields' ? (
            <ReturnFieldsEditor
              fields={returnFields}
              references={returnReferenceOptions}
              onChange={setReturnFields}
            />
          ) : (
            <ReturnSqlScriptEditor
              definition={definition}
              microflowId={microflowId}
              nodeId={draftNode.id}
              references={referenceOptions}
              source={returnSqlScript}
              tables={definition.domainObjects}
              valueType={returnOutputType}
              onChange={setReturnSqlScript}
            />
          )}
        </section>
    );
  }

  function renderDecisionConfig() {
    const conditionMode: DecisionConditionMode = String(draftNode.config.conditionMode ?? 'all') === 'any' ? 'any' : 'all';
    const conditionNot = Boolean(draftNode.config.conditionNot);
    const rules = readDecisionConditionRules(draftNode.config.conditionRules);
    const updateRule = (index: number, patch: Partial<DecisionConditionRule>) => {
      patchConfig({ conditionRules: patchArrayItem(rules, index, { ...rules[index], ...patch }) });
    };

    return (
      <section className="microflow-node-config-section">
        <div className="microflow-node-config-section__toolbar">
          <h4>{translateCurrentLiteral("条件")}</h4>
          <button className="secondary-button h-7 text-xs" type="button" onClick={() => patchConfig({ conditionRules: [...rules, createDecisionConditionRule(rules.length)] })}>
            <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增条件")}</button>
        </div>
        <div className="microflow-node-config-grid">
          <label className="microflow-node-config-field">
            <span>{translateCurrentLiteral("组合方式")}</span>
            <select className="form-select h-8 text-xs" value={conditionMode} onChange={(event) => patchConfig({ conditionMode: event.target.value as DecisionConditionMode })}>
              <option value="all">{translateCurrentLiteral("全部满足 AND")}</option>
              <option value="any">{translateCurrentLiteral("任一满足 OR")}</option>
            </select>
          </label>
          <CheckField label="结果取反 NOT" checked={conditionNot} onChange={(nextValue) => patchConfig({ conditionNot: nextValue })} />
        </div>
        <div className="microflow-node-config-list">
          {rules.map((rule, index) => (
            <div className="microflow-node-config-filter" key={rule.id || index}>
              <ExpressionPicker
                expression={toExpression(rule.leftExpression)}
                label={`条件 ${index + 1} 左值`}
                references={referenceOptions}
                onChange={(leftExpression) => updateRule(index, { leftExpression })}
              />
              <label className="microflow-node-config-field">
                <span>{translateCurrentLiteral("操作符")}</span>
                <select className="form-select h-8 text-xs" value={rule.operator || 'equals'} onChange={(event) => updateRule(index, { operator: event.target.value })}>
                  {operatorOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                </select>
              </label>
              <ExpressionPicker
                expression={toExpression(rule.rightExpression)}
                label={rule.operator === 'between' ? '区间值' : '右值'}
                references={referenceOptions}
                onChange={(rightExpression) => updateRule(index, { rightExpression })}
              />
              <button className="icon-button h-8 w-8 text-red-500" title={translateCurrentLiteral("删除条件")} type="button" onClick={() => patchConfig({ conditionRules: removeArrayItem(rules, index) })}>
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          ))}
          {rules.length === 0 ? (
            <div className="microflow-node-config-empty">{translateCurrentLiteral("未配置条件组。请新增条件，运行时只读取结构化 AND / OR / NOT 条件。")}</div>
          ) : null}
        </div>
      </section>
    );
  }

  function patchDraft(patch: Partial<MicroflowNode>) {
    setDraftNode((current) => current ? { ...current, ...patch } : current);
  }

  function patchConfig(patch: Record<string, unknown>) {
    setDraftNode((current) => current ? { ...current, config: { ...current.config, ...patch } } : current);
  }

  function save() {
    if (!draftNode) {
      return;
    }

    const normalizedDraftNode = normalizeOutputSchemaBeforeSave(definition, draftNode);
    const baseDefinition = normalizeMicroflowDefinitionForSave(replaceNode(definition, normalizedDraftNode));
    if (normalizedDraftNode.type === globalVariablesNodeType) {
      onSave(writeGlobalVariableNodeVariables(
        baseDefinition,
        normalizedDraftNode.id,
        readGlobalVariableNodeVariables(normalizedDraftNode)
      ));
      onClose();
      return;
    }

    if (normalizedDraftNode.type !== 'return') {
      onSave(normalizeMicroflowDefinitionForSave(baseDefinition));
      onClose();
      return;
    }

    if (!returnOutputCode.trim()) {
      message.error('请配置返回输出变量');
      return;
    }

    if (returnOutputMode === 'fields' && selectedReturnFields.length === 0) {
      message.error('请至少选择或新增一个返回字段');
      return;
    }

    const sqlScript = returnOutputMode === 'fields'
      ? null
      : normalizeSqlScriptForSave(returnSqlScript);
    if (returnOutputMode === 'sqlScript' && !sqlScript) {
      message.error('请配置 SQL 脚本输出源');
      return;
    }

    const effectiveFields = returnOutputMode === 'sqlScript'
      ? normalizeSqlScriptFieldsForSave(sqlScript)
      : selectedReturnFields.map((field) => normalizeReturnFieldBeforeSave(field, selectedReturnContext));
    const returnSchema = {
      arrayExpression: returnOutputMode === 'fields' && returnOutputType === 'array'
        ? selectedReturnContext?.expression ?? readReturnOutputSchema(definition, draftNode)?.arrayExpression ?? null
        : null,
      fields: effectiveFields,
      sourceMode: returnOutputMode,
      sqlScript,
      valueType: returnOutputMode === 'sqlScript' && sqlScript
        ? normalizeVariableValueType(sqlScript.resultShape.valueType)
        : normalizeVariableValueType(returnOutputType),
      variableCode: returnOutputCode.trim(),
      variableName: returnOutputName.trim() || returnOutputCode.trim()
    };
    const nextDefinition = applyReturnOutputSchema(baseDefinition, draftNode.id, returnSchema);
    const nextReturnNode = nextDefinition.nodes.find((item) => item.id === draftNode.id);
    const returnIssues = nextReturnNode ? validateReturnOutputSchema(nextDefinition, nextReturnNode) : [];
    const errorIssue = returnIssues.find((issue) => issue.severity === 'error');
    if (errorIssue) {
      message.error(errorIssue.message);
      return;
    }

    onSave(normalizeMicroflowDefinitionForSave(nextDefinition));
    onClose();
  }
}

function defaultNodeConfigTab(nodeType: string): (typeof nodeConfigTabs)[number] {
  if (nodeType === 'decision') {
    return '条件判断';
  }

  if (nodeType === 'return') {
    return '输出结果';
  }

  if (nodeType === 'start' || nodeType === 'globalVariables') {
    return '输入配置';
  }

  return '基础信息';
}

function nodeDescription(nodeType: string): string {
  if (nodeType === 'decision') {
    return '根据设定条件判断分支，满足条件走满足分支，否则走不满足分支';
  }

  if (nodeType === 'return') {
    return '按返回字段结构投影业务输出';
  }

  if (nodeType === globalVariablesNodeType) {
    return '定义画布级变量，供任意节点读取和写入';
  }

  return '配置节点输入、处理逻辑和输出结果';
}

function normalizeOutputSchemaBeforeSave(definition: MicroflowDefinition, node: MicroflowNode): MicroflowNode {
  if (!supportsOutputSchema(node)) {
    return node;
  }

  const variableCode = readDraftOutputVariableCode(node);
  if (!variableCode) {
    const config = { ...node.config };
    delete config.outputSchema;
    return { ...node, config };
  }

  const schema = readNodeOutputSchema(definition, node);
  const schemaFields = schema?.fields.length ? schema.fields : inferDraftOutputFields(definition, node);
  return {
    ...node,
    config: {
      ...node.config,
      outputSchema: {
        fields: node.type === 'setVariable'
          ? normalizeSetVariableOutputFields(String(node.config.variableCode ?? ''), schemaFields)
          : schemaFields.map(cloneMicroflowField),
        valueType: schema?.valueType ?? inferDraftOutputValueType(node),
        variableCode,
        variableName: schema?.variableName || node.name || variableCode
      }
    }
  };
}

function GlobalVariablesEditor({
  definition,
  node,
  onChange
}: {
  definition: MicroflowDefinition;
  node: MicroflowNode;
  onChange: (variables: MicroflowVariable[]) => void;
}) {
  const variables = readGlobalVariableNodeVariables(node);
  return (
    <section className="microflow-node-config-section">
      <div className="microflow-node-config-section__toolbar">
        <div>
          <h4>{translateCurrentLiteral("全局变量")}</h4>
          <p className="microflow-node-config-muted">{translateCurrentLiteral("这里定义画布级局部变量，不参与连线，所有节点都可以通过变量树读取或写入。")}</p>
        </div>
        <button className="secondary-button h-7 text-xs" type="button" onClick={() => onChange([...variables, createDefaultGlobalVariable(node.id, variables.length)])}>
          <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增变量")}</button>
      </div>
      <div className="microflow-node-config-list">
        {variables.map((variable, index) => (
          <MicroflowVariableSchemaEditor
            domainObjects={definition.domainObjects}
            key={`${variable.variableCode}-${index}`}
            variable={variable}
            onChange={(nextVariable) => onChange(patchArrayItem(variables, index, { ...nextVariable, sourceNodeId: node.id }))}
            onDelete={() => onChange(removeArrayItem(variables, index))}
          />
        ))}
        {variables.length === 0 ? <div className="microflow-node-config-empty">{translateCurrentLiteral("暂无全局变量，新增后可在任意节点表达式中选择。")}</div> : null}
      </div>
    </section>
  );
}

function VariableTargetPicker({
  references,
  value,
  onChange
}: {
  references: MicroflowNodeReferenceOption[];
  value: string;
  onChange: (value: string) => void;
}) {
  const writableReferences = references.filter((option) =>
    option.expression.kind === 'ref' &&
    Boolean(option.expression.ref) &&
    (option.sourceKind === 'variable' || option.sourceKind === 'output')
  );
  const matched = writableReferences.find((option) => readExpressionTargetPath(option.expression) === value);
  const hasInvalidValue = value.trim().length > 0 && !matched;
  return (
    <div className="microflow-expression-builder">
      <label className="microflow-node-config-field">
        <span>{translateCurrentLiteral("目标变量")}</span>
        <select className="form-select h-8 text-xs" value={matched?.id ?? ''} onChange={(event) => {
          const reference = writableReferences.find((item) => item.id === event.target.value);
          onChange(reference ? readExpressionTargetPath(reference.expression) : '');
        }}>
          <option value="">{translateCurrentLiteral("选择可写变量或字段")}</option>
          {writableReferences.map((option) => (
            <option key={option.id} value={option.id}>
              {option.isField ? '字段' : '变量'} · {option.label} / {option.valueType}
            </option>
          ))}
        </select>
      </label>
      {hasInvalidValue ? (
        <div className="microflow-expression-builder__error">
          目标变量 {value} 不在当前变量树中，请重新选择。
        </div>
      ) : null}
    </div>
  );
}

function FieldMappingsEditor({
  allowCustomTarget = false,
  mappings,
  references,
  targetFields,
  title,
  onChange
}: {
  allowCustomTarget?: boolean;
  mappings: Array<Record<string, unknown>>;
  references: MicroflowNodeReferenceOption[];
  targetFields: MicroflowDomainField[];
  title: string;
  onChange: (mappings: Array<Record<string, unknown>>) => void;
}) {
  return (
    <section className="microflow-node-config-section">
      <div className="microflow-node-config-section__toolbar">
        <h4>{title}</h4>
        <button className="secondary-button h-7 text-xs" type="button" onClick={() => onChange([...mappings, createFieldMapping()])}>
          <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增")}</button>
      </div>
      <div className="microflow-node-config-list">
        {mappings.map((mapping, index) => (
          <div className="microflow-node-config-mapping" key={index}>
            <FieldCodeInput
              allowCustom={allowCustomTarget}
              fields={targetFields}
              label="目标字段"
              value={String(mapping.target ?? '')}
              onChange={(target) => onChange(patchArrayItem(mappings, index, { ...mapping, target }))}
            />
            <ExpressionPicker
              expression={toExpression(mapping.expression)}
              label="来源"
              references={references}
              onChange={(expression) => onChange(patchArrayItem(mappings, index, { ...mapping, expression }))}
            />
            <button className="icon-button h-8 w-8 text-red-500" title={translateCurrentLiteral("删除映射")} type="button" onClick={() => onChange(removeArrayItem(mappings, index))}>
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>
        ))}
        {mappings.length === 0 ? <div className="microflow-node-config-empty">{translateCurrentLiteral("暂无映射，可自由新增目标字段并绑定上游变量。")}</div> : null}
      </div>
    </section>
  );
}

function NodeOutputSchemaEditor({
  fallbackFields,
  fields,
  valueType,
  variableCode,
  variableName,
  onChange
}: {
  fallbackFields: MicroflowDomainField[];
  fields: MicroflowDomainField[];
  valueType: string;
  variableCode: string;
  variableName: string;
  onChange: (schema: {
    fields: MicroflowDomainField[];
    valueType: string;
    variableCode: string;
    variableName: string;
  }) => void;
}) {
  const normalizedValueType = normalizeVariableValueType(valueType);
  const canEditFields = variableCode.trim().length > 0;
  const patchSchema = (patch: Partial<{ fields: MicroflowDomainField[]; valueType: string; variableName: string }>) => {
    if (!canEditFields) {
      return;
    }

    onChange({
      fields: patch.fields ?? fields,
      valueType: patch.valueType ?? normalizedValueType,
      variableCode,
      variableName: patch.variableName ?? variableName
    });
  };

  return (
    <section className="microflow-node-config-section">
      <div className="microflow-node-config-section__toolbar">
        <h4>{translateCurrentLiteral("输出参数")}</h4>
        <div className="microflow-node-config-toolbar-actions">
          {fallbackFields.length > 0 ? (
            <button className="secondary-button h-7 text-xs" disabled={!canEditFields} type="button" onClick={() => patchSchema({ fields: fallbackFields.map(cloneMicroflowField) })}>{translateCurrentLiteral("从模型带入")}</button>
          ) : null}
          <button className="secondary-button h-7 text-xs" disabled={!canEditFields} type="button" onClick={() => patchSchema({ fields: [...fields, createMicroflowVariableField(fields.length)] })}>
            <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增字段")}</button>
        </div>
      </div>
      {!canEditFields ? (
        <div className="microflow-node-config-empty">{translateCurrentLiteral("先配置输出变量后，再编辑下游可引用的输出字段。")}</div>
      ) : (
        <>
          <div className="microflow-node-config-grid microflow-node-config-grid--three">
            <TextField disabled label="输出变量" value={variableCode} onChange={() => undefined} />
            <TextField label="显示名称" value={variableName} onChange={(nextName) => patchSchema({ variableName: nextName })} />
            <label className="microflow-node-config-field">
              <span>{translateCurrentLiteral("值类型")}</span>
              <select className="form-select h-8 text-xs" value={normalizedValueType} onChange={(event) => patchSchema({ valueType: event.target.value })}>
                {microflowFieldDataTypeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </label>
          </div>
          <div className="microflow-node-config-output-fields">
            {fields.map((field, index) => (
              <OutputFieldRow
                field={field}
                fieldOptions={fallbackFields}
                key={`${field.fieldCode}-${index}`}
                onChange={(nextField) => patchSchema({ fields: patchArrayItem(fields, index, nextField) })}
                onDelete={() => patchSchema({ fields: removeArrayItem(fields, index) })}
              />
            ))}
            {fields.length === 0 ? <div className="microflow-node-config-empty">{translateCurrentLiteral("未配置输出字段；下游只能选择整个变量。可自由新增字段，不受固定 5 个限制。")}</div> : null}
          </div>
        </>
      )}
    </section>
  );
}

function OutputFieldRow({
  field,
  fieldOptions,
  onChange,
  onDelete
}: {
  field: MicroflowDomainField;
  fieldOptions: MicroflowDomainField[];
  onChange: (field: MicroflowDomainField) => void;
  onDelete: () => void;
}) {
  const matchedField = fieldOptions.find((option) => option.fieldCode === field.fieldCode);
  const hasFieldOptions = fieldOptions.length > 0;
  return (
    <div className="microflow-node-config-output-field">
      {hasFieldOptions ? (
        <select
          className="form-select h-8 text-xs"
          value={matchedField ? field.fieldCode : ''}
          onChange={(event) => {
            const selected = fieldOptions.find((option) => option.fieldCode === event.target.value);
            onChange(selected ? { ...cloneMicroflowField(selected), required: field.required, visible: field.visible } : { ...field, fieldCode: '' });
          }}
        >
          <option value="">{translateCurrentLiteral("选择字段")}</option>
          {fieldOptions.map((option) => <option key={option.fieldCode} value={option.fieldCode}>{option.fieldName || option.fieldCode}</option>)}
        </select>
      ) : (
        <input className="form-input h-8 text-xs" placeholder={translateCurrentLiteral("字段代码")} value={field.fieldCode} onChange={(event) => onChange({ ...field, fieldCode: event.target.value })} />
      )}
      <input className="form-input h-8 text-xs" placeholder={translateCurrentLiteral("显示名称")} value={field.fieldName} onChange={(event) => onChange({ ...field, fieldName: event.target.value })} />
      <select className="form-select h-8 text-xs" value={field.dataType} onChange={(event) => onChange({ ...field, dataType: event.target.value })}>
        {microflowFieldDataTypeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
      <CheckField label="必填" checked={Boolean(field.required)} onChange={(required) => onChange({ ...field, required })} />
      <button className="icon-button h-8 w-8 text-red-500" title={translateCurrentLiteral("删除字段")} type="button" onClick={onDelete}>
        <Trash2 className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

function FilterMappingsEditor({
  filters,
  fieldOptions,
  references,
  onChange
}: {
  fieldOptions: MicroflowDomainField[];
  filters: Array<Record<string, unknown>>;
  references: MicroflowNodeReferenceOption[];
  onChange: (filters: Array<Record<string, unknown>>) => void;
}) {
  return (
    <section className="microflow-node-config-section">
      <div className="microflow-node-config-section__toolbar">
        <h4>{translateCurrentLiteral("查询过滤")}</h4>
        <button className="secondary-button h-7 text-xs" type="button" onClick={() => onChange([...filters, createFilterMapping()])}>
          <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增")}</button>
      </div>
      <div className="microflow-node-config-list">
        {filters.map((filter, index) => (
          <div className="microflow-node-config-filter" key={index}>
            <FieldCodeInput fields={fieldOptions} label="字段" value={String(filter.field ?? '')} onChange={(field) => onChange(patchArrayItem(filters, index, { ...filter, field }))} />
            <label className="microflow-node-config-field">
              <span>{translateCurrentLiteral("操作符")}</span>
              <select className="form-select h-8 text-xs" value={String(filter.operator ?? 'equals')} onChange={(event) => onChange(patchArrayItem(filters, index, { ...filter, operator: event.target.value }))}>
                {operatorOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
            </label>
            <ExpressionPicker expression={toExpression(filter.valueExpression)} label="值" references={references} onChange={(valueExpression) => onChange(patchArrayItem(filters, index, { ...filter, valueExpression }))} />
            {String(filter.operator ?? '') === 'between' ? (
              <ExpressionPicker expression={toExpression(filter.valueToExpression)} label="结束值" references={references} onChange={(valueToExpression) => onChange(patchArrayItem(filters, index, { ...filter, valueToExpression }))} />
            ) : null}
            <button className="icon-button h-8 w-8 text-red-500" title={translateCurrentLiteral("删除过滤")} type="button" onClick={() => onChange(removeArrayItem(filters, index))}>
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>
        ))}
        {filters.length === 0 ? <div className="microflow-node-config-empty">{translateCurrentLiteral("暂无过滤，可新增后从上游变量选择条件值。")}</div> : null}
      </div>
    </section>
  );
}

function CompositeChildrenEditor({
  children,
  definition,
  mode,
  references,
  onChange
}: {
  children: Array<Record<string, unknown>>;
  definition: MicroflowDefinition;
  mode: string;
  references: MicroflowNodeReferenceOption[];
  onChange: (children: Array<Record<string, unknown>>) => void;
}) {
  return (
    <section className="microflow-node-config-section">
      <div className="microflow-node-config-section__toolbar">
        <h4>{translateCurrentLiteral("子对象")}</h4>
        <button className="secondary-button h-7 text-xs" type="button" onClick={() => onChange([...children, createCompositeChild(mode)])}>
          <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增")}</button>
      </div>
      <div className="microflow-node-config-list">
        {children.map((child, index) => (
          <div className="microflow-node-config-child" key={index}>
            <div className="microflow-node-config-grid microflow-node-config-grid--three">
              <TextField label="子模型" value={String(child.modelCode ?? '')} onChange={(modelCode) => onChange(patchArrayItem(children, index, { ...child, modelCode }))} />
              <TextField label="父主键" value={String(child.parentKeyField ?? 'id')} onChange={(parentKeyField) => onChange(patchArrayItem(children, index, { ...child, parentKeyField }))} />
              <TextField label="外键" value={String(child.foreignKeyField ?? '')} onChange={(foreignKeyField) => onChange(patchArrayItem(children, index, { ...child, foreignKeyField }))} />
            </div>
            {mode === 'compositeDetail' ? (
              <div className="microflow-node-config-grid microflow-node-config-grid--three">
                <TextField label="绑定 Key" value={String(child.bindingKey ?? '')} onChange={(bindingKey) => onChange(patchArrayItem(children, index, { ...child, bindingKey }))} />
                <NumberField label="页码" value={Number(child.pageIndex ?? 1)} onChange={(pageIndex) => onChange(patchArrayItem(children, index, { ...child, pageIndex }))} />
                <NumberField label="页大小" value={Number(child.pageSize ?? 20)} onChange={(pageSize) => onChange(patchArrayItem(children, index, { ...child, pageSize }))} />
              </div>
            ) : null}
            {mode === 'compositeCreate' || mode === 'compositeUpdate' ? (
              <ExpressionPicker expression={toExpression(child.rowsExpression, 'variables', 'array')} label="子集合来源" references={references.filter((option) => !option.isField && option.valueType === 'array')} onChange={(rowsExpression) => onChange(patchArrayItem(children, index, { ...child, rowsExpression }))} />
            ) : null}
            {mode === 'compositeUpdate' ? (
              <>
                <ExpressionPicker expression={toExpression(child.deleteIdsExpression, 'variables', 'array')} label="删除 ID 集合" references={references} onChange={(deleteIdsExpression) => onChange(patchArrayItem(children, index, { ...child, deleteIdsExpression }))} />
                <CheckField label="删除缺失子行" checked={Boolean(child.deleteMissing)} onChange={(deleteMissing) => onChange(patchArrayItem(children, index, { ...child, deleteMissing }))} />
              </>
            ) : null}
            {mode === 'compositeDelete' ? (
              <>
                <ExpressionPicker expression={toExpression(child.parentIdExpression)} label="父级主键表达式" references={references} onChange={(parentIdExpression) => onChange(patchArrayItem(children, index, { ...child, parentIdExpression }))} />
                <CheckField label="必须存在子数据" checked={Boolean(child.required)} onChange={(required) => onChange(patchArrayItem(children, index, { ...child, required }))} />
              </>
            ) : null}
            {mode === 'compositeCreate' || mode === 'compositeUpdate' ? (
              <FieldMappingsEditor
                mappings={readRecordArray(child.fieldMappings)}
                references={references}
                targetFields={findModelFields(definition, String(child.modelCode ?? child.childModelCode ?? ''))}
                title={translateCurrentLiteral("子对象字段映射")}
                onChange={(fieldMappings) => onChange(patchArrayItem(children, index, { ...child, fieldMappings }))}
              />
            ) : null}
            <button className="danger-button h-7 text-xs" type="button" onClick={() => onChange(removeArrayItem(children, index))}>{translateCurrentLiteral("删除子对象")}</button>
          </div>
        ))}
        {children.length === 0 ? <div className="microflow-node-config-empty">{translateCurrentLiteral("暂无子对象配置。")}</div> : null}
      </div>
    </section>
  );
}

function ReturnFieldsEditor({
  fields,
  references,
  onChange
}: {
  fields: EditableReturnField[];
  references: MicroflowNodeReferenceOption[];
  onChange: (fields: EditableReturnField[]) => void;
}) {
  return (
    <section className="microflow-node-config-section">
      <div className="microflow-node-config-section__toolbar">
        <h4>{translateCurrentLiteral("返回字段")}</h4>
        <button className="secondary-button h-7 text-xs" type="button" onClick={() => onChange([...fields, { ...createMicroflowVariableField(fields.length), selected: true, expression: createExpression('variables', '', 'string'), sourceField: false }])}>
          <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增字段")}</button>
      </div>
      <div className="microflow-node-config-return-fields">
        {fields.map((field, index) => (
          <div className="microflow-node-config-return-field" key={`${field.fieldCode}-${index}`}>
            <div className="microflow-node-config-return-field__main">
              <label className="microflow-node-config-check microflow-node-config-check--select">
                <input checked={field.selected} type="checkbox" onChange={(event) => onChange(patchArrayItem(fields, index, { ...field, selected: event.target.checked }))} />
              </label>
              <input className="form-input h-8 text-xs" placeholder={translateCurrentLiteral("选择来源后生成字段代码")} readOnly title={translateCurrentLiteral("字段代码由来源表达式自动解析生成")} value={field.fieldCode} />
              <input className="form-input h-8 text-xs" placeholder={translateCurrentLiteral("显示名称")} value={field.fieldName} onChange={(event) => onChange(patchArrayItem(fields, index, { ...field, fieldName: event.target.value }))} />
              <select className="form-select h-8 text-xs" value={field.dataType} onChange={(event) => onChange(patchArrayItem(fields, index, { ...field, dataType: event.target.value }))}>
                {microflowFieldDataTypeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
              <button className="icon-button h-8 w-8 text-red-500" title={translateCurrentLiteral("删除字段")} type="button" onClick={() => onChange(removeArrayItem(fields, index))}>
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
            <div className="microflow-node-config-return-field__meta">
              <label className="microflow-node-config-inline-check">
                <input checked={Boolean(field.required)} type="checkbox" onChange={(event) => onChange(patchArrayItem(fields, index, { ...field, required: event.target.checked }))} />{translateCurrentLiteral("必填")}</label>
              <label className="microflow-node-config-inline-check">
                <input checked={field.visible !== false} type="checkbox" onChange={(event) => onChange(patchArrayItem(fields, index, { ...field, visible: event.target.checked }))} />{translateCurrentLiteral("可见")}</label>
              <button className="icon-button h-8 w-8" disabled={index === 0} title={translateCurrentLiteral("上移")} type="button" onClick={() => onChange(moveArrayItem(fields, index, -1))}>
                <ChevronUp className="h-3.5 w-3.5" />
              </button>
              <button className="icon-button h-8 w-8" disabled={index === fields.length - 1} title={translateCurrentLiteral("下移")} type="button" onClick={() => onChange(moveArrayItem(fields, index, 1))}>
                <ChevronDown className="h-3.5 w-3.5" />
              </button>
            </div>
            <ExpressionPicker
              expression={toExpression(field.expression, 'variables', field.dataType || 'string')}
              helperTitle="字段二次处理"
              label="来源表达式"
              references={references}
              onChange={(expression) => onChange(patchArrayItem(fields, index, applyExpressionToReturnField(field, expression, references)))}
            />
          </div>
        ))}
        {fields.length === 0 ? <div className="microflow-node-config-empty">{translateCurrentLiteral("当前来源没有字段 schema，可自由新增返回字段。")}</div> : null}
      </div>
    </section>
  );
}

function ExpressionPicker({
  expression,
  helperTitle,
  label,
  references,
  onChange
}: {
  expression: MicroflowValueExpression;
  helperTitle?: string;
  label: string;
  references: MicroflowNodeReferenceOption[];
  onChange: (expression: MicroflowValueExpression) => void;
}) {
  return (
    <MicroflowExpressionBuilder
      expression={expression}
      helperTitle={helperTitle}
      label={label}
      references={references}
      onChange={onChange}
    />
  );
}

function applyExpressionToReturnField(
  field: EditableReturnField,
  expression: MicroflowValueExpression,
  references: MicroflowNodeReferenceOption[]
): EditableReturnField {
  const reference = findReferenceByExpression(references, expression) ?? findReferenceByExpression(references, readPrimaryExpressionArgument(expression));
  if (!reference) {
    return {
      ...field,
      dataType: normalizeVariableValueType(expression.dataType),
      expression
    };
  }

  const referenceField = reference.field ? cloneMicroflowField(reference.field) : null;
  return {
    ...field,
    dataType: normalizeVariableValueType(referenceField?.dataType ?? reference.valueType ?? expression.dataType),
    expression,
    fieldCode: referenceField?.fieldCode || reference.sourceVariableCode || field.fieldCode,
    fieldName: referenceField?.fieldName || reference.description || reference.label || field.fieldName,
    required: field.required ?? referenceField?.required,
    visible: field.visible ?? referenceField?.visible
  };
}

function findReferenceByExpression(
  references: MicroflowNodeReferenceOption[],
  expression: MicroflowValueExpression | null | undefined
): MicroflowNodeReferenceOption | null {
  if (!expression || expression.kind !== 'ref' || !expression.ref) {
    return null;
  }

  return references.find((option) => {
    const optionRef = option.expression.ref;
    return option.expression.kind === 'ref'
      && optionRef
      && optionRef.sourceType === expression.ref?.sourceType
      && optionRef.variableId === expression.ref.variableId
      && (optionRef.outputKey ?? '') === (expression.ref.outputKey ?? '')
      && optionRef.fieldPath.join('.') === expression.ref.fieldPath.join('.');
  }) ?? null;
}

function readPrimaryExpressionArgument(expression: MicroflowValueExpression): MicroflowValueExpression | null {
  return expression.kind === 'function' && Array.isArray(expression.args) && expression.args.length > 0
    ? expression.args[0] ?? null
    : null;
}

function TextField({
  disabled = false,
  label,
  value,
  onChange
}: {
  disabled?: boolean;
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="microflow-node-config-field">
      <span>{label}</span>
      <input className="form-input h-8 text-xs" disabled={disabled} value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function NumberField({
  label,
  min = 1,
  value,
  onChange
}: {
  label: string;
  min?: number;
  value: number;
  onChange: (value: number) => void;
}) {
  return (
    <label className="microflow-node-config-field">
      <span>{label}</span>
      <input className="form-input h-8 text-xs" min={min} type="number" value={Number.isFinite(value) ? value : min} onChange={(event) => {
        const nextValue = Number(event.target.value);
        onChange(Number.isFinite(nextValue) ? nextValue : min);
      }} />
    </label>
  );
}

function FieldCodeInput({
  allowCustom = false,
  fields,
  label,
  value,
  onChange
}: {
  allowCustom?: boolean;
  fields: MicroflowDomainField[];
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  const matched = fields.find((field) => field.fieldCode === value);
  const hasInvalidValue = Boolean(value.trim()) && fields.length > 0 && !matched;
  if (fields.length > 0 && !allowCustom) {
    return (
      <label className="microflow-node-config-field">
        <span>{label}</span>
        <select className="form-select h-8 text-xs" value={matched ? value : ''} onChange={(event) => onChange(event.target.value)}>
          <option value="">{translateCurrentLiteral("选择字段")}</option>
          {fields.map((field) => <option key={field.fieldCode} value={field.fieldCode}>{field.fieldName || field.fieldCode}</option>)}
        </select>
        {hasInvalidValue ? <small className="microflow-node-config-field__error">字段 {value} 不在当前模型字段中，请重新选择。</small> : null}
      </label>
    );
  }

  return (
    <label className="microflow-node-config-field">
      <span>{label}</span>
      <input className="form-input h-8 text-xs" disabled={!allowCustom && fields.length === 0} placeholder={allowCustom ? '输入目标 Key' : '暂无字段目录'} value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function CheckField({ checked, label, onChange }: { checked: boolean; label: string; onChange: (checked: boolean) => void }) {
  return (
    <label className="microflow-node-config-inline-check">
      <input checked={checked} type="checkbox" onChange={(event) => onChange(event.target.checked)} />
      {label}
    </label>
  );
}

function buildReturnContexts(
  definition: MicroflowDefinition,
  node: MicroflowNode | null,
  currentSchema: ReturnType<typeof readReturnOutputSchema>
): MicroflowContextVariable[] {
  const available = listNodeOutputSchemaOptions(definition, node);
  if (!currentSchema?.variableCode) {
    return available;
  }

  const hasCurrent = available.some((context) => context.variableCode.toLowerCase() === currentSchema.variableCode.toLowerCase());
  if (hasCurrent) {
    return available;
  }

  return [
    {
      expression: createExpression('variables', currentSchema.variableCode, currentSchema.valueType),
      fields: currentSchema.fields,
      id: `return-schema:${currentSchema.variableCode}`,
      sourceKind: 'variable',
      sourceLabel: `variables.${currentSchema.variableCode}`,
      valueType: currentSchema.valueType,
      variableCode: currentSchema.variableCode,
      variableName: currentSchema.variableName
    },
    ...available
  ];
}

function findInitialReturnContext(
  contexts: MicroflowContextVariable[],
  currentSchema: ReturnType<typeof readReturnOutputSchema>
): MicroflowContextVariable | null {
  if (currentSchema?.variableCode) {
    const matched = contexts.find((context) => context.variableCode.toLowerCase() === currentSchema.variableCode.toLowerCase());
    if (matched) {
      return matched;
    }
  }

  return contexts.find((context) => context.valueType === 'array') ?? contexts[0] ?? null;
}

function createReturnReferenceOptions(
  references: MicroflowNodeReferenceOption[],
  context: MicroflowContextVariable | null
): MicroflowNodeReferenceOption[] {
  if (!context || context.valueType !== 'array') {
    return references;
  }

  const currentRowOptions: MicroflowNodeReferenceOption[] = context.fields
    .filter((field) => field.fieldCode.trim())
    .map((field) => {
      const fieldCode = field.fieldCode.trim();
      const valueType = normalizeVariableValueType(field.dataType);
      return {
        description: `当前行 · ${field.fieldName || fieldCode}`,
        expression: createExpression('currentRow', fieldCode, valueType),
        field: cloneMicroflowField(field),
        id: `current-row:field:${fieldCode}`,
        isField: true,
        label: `当前行.${fieldCode}`,
        sourceKind: 'node' as const,
        sourceVariableCode: 'currentRow',
        valueType
      };
    });

  return [...currentRowOptions, ...references];
}

function createSqlResultExpression(
  variableCode: 'sqlRow' | 'sqlRows',
  fieldPath: string[],
  dataType: string,
  label: string
): MicroflowValueExpression {
  const normalizedType = normalizeVariableValueType(dataType);
  return {
    dataType: normalizedType,
    kind: 'ref',
    ref: {
      dataType: normalizedType,
      fieldPath,
      label,
      outputKey: variableCode,
      sourceType: 'sqlResult',
      variableId: variableCode
    }
  };
}

function createEmptySqlScript(): MicroflowSqlScript {
  return {
    dataSourceId: '',
    localVariables: [],
    maxRows: 50,
    parameters: [],
    resultShape: {
      fields: [],
      valueType: 'array'
    },
    script: ''
  };
}

function normalizeSqlScriptFieldsForSave(
  sqlScript: MicroflowSqlScript | null
): MicroflowDomainField[] {
  return (sqlScript?.resultShape.fields ?? [])
    .filter((field) => field.fieldCode.trim())
    .map((field) => {
      const fieldCode = field.fieldCode.trim();
      const normalized = {
        ...cloneMicroflowField(field),
        fieldCode,
        fieldName: field.fieldName.trim() || fieldCode,
        required: Boolean(field.required),
        visible: field.visible !== false
      };
      return {
        ...normalized,
        expression: createSqlResultExpression('sqlRow', [fieldCode], field.dataType, `SQL结果.${fieldCode}`)
      };
    });
}

function buildEditableReturnFields(
  context: MicroflowContextVariable | null | undefined,
  configuredFields: MicroflowDomainField[]
): EditableReturnField[] {
  const configuredByCode = new Map(configuredFields.map((field) => [field.fieldCode.trim().toLowerCase(), field]));
  const contextFields = context?.fields ?? [];
  const result: EditableReturnField[] = contextFields.map((field) => {
    const configured = configuredByCode.get(field.fieldCode.trim().toLowerCase());
    const expression = configured?.expression
      ? cloneExpression(configured.expression)
      : createReturnFieldExpression(context, field);
    return {
      ...cloneMicroflowField(configured ?? field),
      expression,
      selected: configuredFields.length === 0 || Boolean(configured),
      sourceField: true
    };
  });
  const contextFieldCodes = new Set(contextFields.map((field) => field.fieldCode.trim().toLowerCase()));
  for (const field of configuredFields) {
    const fieldCode = field.fieldCode.trim().toLowerCase();
    if (!fieldCode || contextFieldCodes.has(fieldCode)) {
      continue;
    }

    result.push({
      ...cloneMicroflowField(field),
      expression: field.expression ? cloneExpression(field.expression) : createExpression('variables', '', field.dataType || 'string'),
      selected: true,
      sourceField: false
    });
  }

  return result;
}

function normalizeReturnFieldBeforeSave(
  field: EditableReturnField,
  context: MicroflowContextVariable | null
): MicroflowDomainField {
  const normalized = cloneMicroflowField(field);
  normalized.expression = field.expression
    ? cloneExpression(field.expression)
    : createReturnFieldExpression(context, field);
  return normalized;
}

function createReturnFieldExpression(
  context: MicroflowContextVariable | null | undefined,
  field: MicroflowDomainField
): MicroflowValueExpression {
  const valueType = normalizeVariableValueType(field.dataType);
  if (!context?.variableCode) {
    return createExpression('variables', '', valueType);
  }

  if (context.valueType === 'array') {
    return createExpression('currentRow', field.fieldCode, valueType);
  }

  return createExpression('variables', `${context.variableCode}.${field.fieldCode}`, valueType);
}

function findModelFields(definition: MicroflowDefinition, modelCode: string): MicroflowDomainField[] {
  const normalized = modelCode.trim().toLowerCase();
  if (!normalized) {
    return [];
  }

  const domainObject = definition.domainObjects.find((item) =>
    [item.objectCode, item.modelCode].some((code) => code && code.toLowerCase() === normalized)
  );
  return domainObject?.fields.map(cloneMicroflowField) ?? [];
}

function supportsOutputSchema(node: MicroflowNode): boolean {
  return !['start', 'end', 'decision', 'return', globalVariablesNodeType].includes(node.type);
}

function readDraftOutputVariableCode(node: MicroflowNode): string {
  if (node.type === 'loop') {
    return String(node.config.itemVariable ?? '').trim();
  }

  if (node.type === 'setVariable') {
    return getVariableRootCode(String(node.config.variableCode ?? ''));
  }

  return String(node.config.targetVariable ?? node.config.variableCode ?? '').trim();
}

function inferDraftOutputValueType(node: MicroflowNode): string {
  if (node.type === 'loop' || node.type === 'detail' || node.type === 'compositeDetail' || node.type === 'create' || node.type === 'change' || node.type === 'setVariable' || node.type === 'delete' || node.type === 'compositeDelete') {
    return 'object';
  }

  return 'array';
}

function inferDraftOutputFields(definition: MicroflowDefinition, node: MicroflowNode): MicroflowDomainField[] {
  if (node.type === 'loop') {
    const sourceVariable = readExpressionVariableCode(node.config.collectionExpression);
    const context = listNodeInputReferenceOptions(definition, node.id)
      .find((option) => !option.isField && option.sourceVariableCode.toLowerCase() === sourceVariable.toLowerCase());
    if (context) {
      return listNodeInputReferenceOptions(definition, node.id)
        .filter((option) => option.isField && option.sourceVariableCode.toLowerCase() === sourceVariable.toLowerCase() && option.field)
        .map((option) => cloneMicroflowField(option.field as MicroflowDomainField));
    }
  }

  const modelCode = String(node.config.modelCode ?? node.config.rootModelCode ?? '').trim();
  return findModelFields(definition, modelCode);
}

function readExpressionVariableCode(value: unknown): string {
  if (!isRecord(value) || value.kind !== 'ref' || !isRecord(value.ref)) {
    return '';
  }

  const reference = value.ref as { outputKey?: unknown; variableId?: unknown };
  return String(reference.outputKey ?? reference.variableId ?? '').trim();
}

function replaceNode(definition: MicroflowDefinition, node: MicroflowNode): MicroflowDefinition {
  return {
    ...definition,
    nodes: definition.nodes.map((item) => item.id === node.id ? node : item)
  };
}

function cloneNode(node: MicroflowNode): MicroflowNode {
  return {
    ...node,
    config: JSON.parse(JSON.stringify(node.config)) as Record<string, unknown>
  };
}

function readRecordArray(value: unknown): Array<Record<string, unknown>> {
  return Array.isArray(value)
    ? value.filter(isRecord).map((item) => ({ ...item }))
    : [];
}

function readDecisionConditionRules(value: unknown): DecisionConditionRule[] {
  return readRecordArray(value).map((item, index) => ({
    id: String(item.id ?? `rule_${index + 1}`),
    leftExpression: toExpression(item.leftExpression),
    operator: String(item.operator ?? 'equals'),
    rightExpression: toExpression(item.rightExpression)
  }));
}

function toExpression(value: unknown, source = 'variables', dataType = 'string'): MicroflowValueExpression {
  return isRecord(value) && typeof value.kind === 'string'
    ? cloneExpression(value as unknown as MicroflowValueExpression)
    : createExpression(source, '', dataType);
}

function createExpression(source: string, path: string, dataType: string): MicroflowValueExpression {
  const normalizedType = normalizeVariableValueType(dataType);
  if (source === 'constant') {
    return {
      dataType: normalizedType,
      kind: 'literal',
      value: createDefaultLiteralValue(normalizedType)
    };
  }

  const parts = path.split('.').filter(Boolean);
  const sourceType = source === 'variables'
    ? 'global'
    : source === 'currentRow' || source === 'row' || source === 'item'
      ? 'loopItem'
      : source;
  const variableId = source === 'variables' ? parts[0] ?? '' : source;
  return {
    dataType: normalizedType,
    kind: 'ref',
    ref: {
      dataType: normalizedType,
      fieldPath: source === 'variables' ? parts.slice(1) : parts,
      label: path || source,
      outputKey: source === 'variables' ? variableId : source,
      sourceType,
      variableId
    }
  };
}

function createDefaultLiteralValue(valueType: string): unknown {
  const normalizedType = normalizeVariableValueType(valueType);
  if (normalizedType === 'array') {
    return [];
  }

  if (normalizedType === 'object' || normalizedType === 'json') {
    return {};
  }

  if (normalizedType === 'number') {
    return 0;
  }

  if (normalizedType === 'boolean') {
    return false;
  }

  return '';
}

function readExpressionTargetPath(expression: MicroflowValueExpression): string {
  const reference = expression.ref;
  if (!reference) {
    return '';
  }

  const root = reference.outputKey || reference.variableId;
  return [root, ...(reference.fieldPath ?? [])].filter(Boolean).join('.');
}

function createFieldMapping(): Record<string, unknown> {
  return {
    expression: createExpression('variables', '', 'string'),
    target: ''
  };
}

function createFilterMapping(): Record<string, unknown> {
  return {
    field: '',
    operator: 'equals',
    valueExpression: createExpression('variables', '', 'string')
  };
}

function createDecisionConditionRule(index: number): DecisionConditionRule {
  return {
    id: `condition_${Date.now().toString(36)}_${index}`,
    leftExpression: createExpression('variables', '', 'string'),
    operator: 'equals',
    rightExpression: createExpression('constant', '', 'string')
  };
}

function createCompositeChild(mode: string): Record<string, unknown> {
  const base = {
    foreignKeyField: '',
    modelCode: '',
    parentKeyField: 'id'
  };
  if (mode === 'compositeDetail') {
    return { ...base, bindingKey: 'children', filters: [], pageIndex: 1, pageSize: 20 };
  }

  if (mode === 'compositeDelete') {
    return { ...base, parentIdExpression: createExpression('currentRow', '__runtimeKey', 'string'), required: false };
  }

  return { ...base, fieldMappings: [], rowsExpression: createExpression('variables', '', 'array') };
}

function patchArrayItem<T>(items: T[], index: number, item: T): T[] {
  return items.map((current, currentIndex) => currentIndex === index ? item : current);
}

function removeArrayItem<T>(items: T[], index: number): T[] {
  return items.filter((_, currentIndex) => currentIndex !== index);
}

function moveArrayItem<T>(items: T[], index: number, offset: -1 | 1): T[] {
  const targetIndex = index + offset;
  if (targetIndex < 0 || targetIndex >= items.length) {
    return items;
  }

  const next = [...items];
  [next[index], next[targetIndex]] = [next[targetIndex], next[index]];
  return next;
}

function cloneExpression(expression: MicroflowValueExpression): MicroflowValueExpression {
  return JSON.parse(JSON.stringify(expression)) as MicroflowValueExpression;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}
