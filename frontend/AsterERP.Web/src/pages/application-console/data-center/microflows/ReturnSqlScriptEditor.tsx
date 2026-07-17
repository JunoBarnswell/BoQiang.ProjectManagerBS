import { Play } from 'lucide-react';
import { useState } from 'react';


import { runApplicationDataCenterMicroflowSqlScript } from '../../../../api/application-data-center/applicationDataCenter.api';
import type {
  ApplicationDataCenterPreviewResponse,
  MicroflowDefinition,
  MicroflowDomainObject,
  MicroflowSqlScript,
  MicroflowValueExpression
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useRuntimeExpressionFunctionCatalog } from '../../../../shared/runtime/expression-functions/useRuntimeExpressionFunctionCatalog';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';
import { WorkbenchResultViewer } from '../workbench/components/WorkbenchResultViewer';

import { normalizeMicroflowDefinitionForSave } from './microflowDefinitionNormalizer';
import type { MicroflowNodeReferenceOption } from './microflowNodeContext';
import { MicroflowSqlScriptEditor } from './MicroflowSqlScriptEditor';
import { inferMicroflowSqlScriptResultFields } from './microflowSqlScriptLanguage';
import { cloneMicroflowField, normalizeVariableValueType } from './microflowVariableSchema';

interface ReturnSqlScriptEditorProps {
  definition: MicroflowDefinition;
  microflowId?: string | null;
  nodeId: string;
  references: MicroflowNodeReferenceOption[];
  source: MicroflowSqlScript;
  tables: MicroflowDomainObject[];
  valueType: string;
  onChange: (source: MicroflowSqlScript) => void;
}

export function ReturnSqlScriptEditor({
  definition,
  microflowId,
  nodeId,
  references,
  source,
  tables,
  valueType,
  onChange
}: ReturnSqlScriptEditorProps) {
  const functionCatalogQuery = useRuntimeExpressionFunctionCatalog('microflowSqlScript');
  const [runResult, setRunResult] = useState<ApplicationDataCenterPreviewResponse | null>(null);
  const [runError, setRunError] = useState('');
  const [isRunning, setIsRunning] = useState(false);
  const pageSize = 50;
  const patchSource = (patch: Partial<MicroflowSqlScript>) => {
    onChange({
      ...source,
      ...patch
    });
  };
  const runScript = async (nextPageIndex = 1) => {
    const sqlScript = normalizeSqlScriptForSave(source);
    if (!microflowId) {
      setRunError('请先保存微流后再运行 SQL');
      return;
    }

    if (!sqlScript) {
      setRunError('请先输入 SQL 脚本');
      return;
    }

    setIsRunning(true);
    setRunError('');
    try {
      const response = await runApplicationDataCenterMicroflowSqlScript(microflowId, {
        definition: normalizeMicroflowDefinitionForSave(definition),
        nodeId,
        pageIndex: nextPageIndex,
        pageSize,
        sqlScript,
        valueType
      });
      setRunResult(response.data);
    } catch (error) {
      setRunError(getErrorMessage(error, 'SQL 执行失败'));
    } finally {
      setIsRunning(false);
    }
  };
  return (
    <div className="microflow-return-sql-script">
      <div className="microflow-return-sql-script__controls">
        <label className="microflow-node-config-field microflow-return-sql-script__type-field">
          <span>{translateCurrentLiteral("返回类型")}</span>
          <select
            className="form-select h-8 text-xs"
            value={source.resultShape.valueType}
            onChange={(event) => patchSource({ resultShape: { ...source.resultShape, valueType: event.target.value } })}
          >
            <option value="object">{translateCurrentLiteral("对象")}</option>
            <option value="array">{translateCurrentLiteral("数组")}</option>
          </select>
        </label>
        <button className="primary-button h-8 text-xs" disabled={isRunning} type="button" onClick={() => runScript(1)}>
          <Play className="h-3.5 w-3.5" />
          {isRunning ? '运行中' : 'Run'}
        </button>
      </div>
      <div className="microflow-return-sql-script__workspace">
        <div className="microflow-return-sql-script__editor-pane">
          <MicroflowSqlScriptEditor
            functionCatalog={functionCatalogQuery.data}
            references={references}
            script={source}
            tables={tables}
            value={source.script}
            onChange={(script) => patchSource({ script })}
          />
        </div>
      </div>
      <div className="microflow-return-sql-run-result">
        <div className="microflow-return-sql-run-result__bar">
          <span>{runError ? '执行失败' : runResult ? '执行结果' : '尚未执行'}</span>
          {runResult?.audit ? <span>审计 TraceId：{runResult.audit.traceId}</span> : null}
        </div>
        {runError ? <div className="microflow-return-sql-run-result__error">{runError}</div> : null}
        {runResult && !runError ? <WorkbenchResultViewer preview={runResult} onPageChange={(nextPageIndex) => runScript(nextPageIndex)} /> : null}
      </div>
    </div>
  );
}

export function cloneSqlScript(source: MicroflowSqlScript): MicroflowSqlScript {
  return {
    dataSourceId: source.dataSourceId,
    localVariables: (source.localVariables ?? []).map((variable) => ({
      dataType: normalizeVariableValueType(variable.dataType),
      initializer: variable.initializer ? cloneExpression(variable.initializer) : null,
      name: variable.name
    })),
    maxRows: source.maxRows ?? 50,
    parameters: (source.parameters ?? []).map((parameter) => ({
      dataType: normalizeVariableValueType(parameter.dataType),
      expression: parameter.expression ? cloneExpression(parameter.expression) : null,
      name: parameter.name
    })),
    resultShape: {
      fields: (source.resultShape.fields ?? []).map(cloneMicroflowField),
      valueType: normalizeVariableValueType(source.resultShape.valueType)
    },
    script: source.script
  };
}

export function normalizeSqlScriptForSave(source: MicroflowSqlScript): MicroflowSqlScript | null {
  const normalized = cloneSqlScript(source);
  normalized.dataSourceId = '';
  normalized.script = normalized.script.trim();
  normalized.maxRows = Math.max(1, Math.min(200, Number(normalized.maxRows ?? 50) || 50));
  normalized.localVariables = normalized.localVariables
    .map((variable) => ({
      dataType: normalizeVariableValueType(variable.dataType),
      initializer: variable.initializer ? cloneExpression(variable.initializer) : null,
      name: variable.name.trim().replace(/^@+/, '')
    }))
    .filter((variable) => variable.name || variable.initializer);
  normalized.parameters = normalized.parameters
    .map((parameter) => ({
      dataType: normalizeVariableValueType(parameter.dataType),
      expression: parameter.expression ? cloneExpression(parameter.expression) : null,
      name: parameter.name.trim().replace(/^@+/, '')
    }))
    .filter((parameter) => parameter.name || parameter.expression);
  normalized.resultShape = {
    fields: (normalized.resultShape.fields.length > 0
      ? normalized.resultShape.fields
      : inferMicroflowSqlScriptResultFields(normalized.script))
      .map((field) => ({
        ...cloneMicroflowField(field),
        fieldCode: field.fieldCode.trim(),
        fieldName: field.fieldName.trim() || field.fieldCode.trim()
      }))
      .filter((field) => field.fieldCode),
    valueType: normalizeVariableValueType(normalized.resultShape.valueType)
  };

  if (!normalized.dataSourceId && !normalized.script && normalized.parameters.length === 0 && normalized.localVariables.length === 0 && normalized.resultShape.fields.length === 0) {
    return null;
  }

  return normalized;
}

function cloneExpression(expression: MicroflowValueExpression): MicroflowValueExpression {
  return JSON.parse(JSON.stringify(expression)) as MicroflowValueExpression;
}
