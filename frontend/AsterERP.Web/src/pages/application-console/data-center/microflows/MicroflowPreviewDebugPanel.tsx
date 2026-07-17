import { Bug, Variable } from 'lucide-react';
import type { ReactNode } from 'react';

import type { MicroflowPreviewResponse } from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';


interface MicroflowPreviewDebugPanelProps {
  result: MicroflowPreviewResponse | null;
}

export function MicroflowPreviewDebugPanel({ result }: MicroflowPreviewDebugPanelProps) {
  if (!result) {
    return null;
  }

  return (
    <details className="microflow-preview-debug">
      <summary><Bug className="h-3.5 w-3.5" />{translateCurrentLiteral("调试详情")}</summary>
      <DebugSection icon={<Variable className="h-3.5 w-3.5" />} title={translateCurrentLiteral("变量摘要")}>
        <div className="microflow-preview-variable-list">
          {result.variables.map((variable) => (
            <div className="microflow-preview-variable" key={variable.name}>
              <div>
                <strong>{variable.name}</strong>
                <span>{variable.valueType}</span>
              </div>
              <p>{variable.displayValue}</p>
              {variable.datasetKey ? <small>数据集：{variable.datasetKey}</small> : null}
            </div>
          ))}
          {result.variables.length === 0 ? <div className="microflow-preview-muted">{translateCurrentLiteral("无变量")}</div> : null}
        </div>
      </DebugSection>

      <details className="microflow-preview-raw">
        <summary><Bug className="h-3.5 w-3.5" />{translateCurrentLiteral("原始结果")}</summary>
        <pre>{JSON.stringify(result.rawResult ?? result, null, 2)}</pre>
      </details>
    </details>
  );
}

function DebugSection({
  children,
  icon,
  title
}: {
  children: ReactNode;
  icon: ReactNode;
  title: string;
}) {
  return (
    <section className="microflow-preview-debug-section">
      <h4>{icon}{title}</h4>
      {children}
    </section>
  );
}
