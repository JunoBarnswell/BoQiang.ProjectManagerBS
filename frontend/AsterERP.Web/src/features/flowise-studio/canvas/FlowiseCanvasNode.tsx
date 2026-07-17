import { IconButton } from '@mui/material';
import type { NodeProps } from '@xyflow/react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { FlowCanvasInputHandles, FlowCanvasOutputHandles } from '../../../shared/flow-canvas/FlowCanvasHandles';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import type { FlowiseCanvasNode as FlowiseCanvasNodeType } from '../types/canvas.types';

export function FlowiseCanvasNode({ data, selected }: NodeProps<FlowiseCanvasNodeType>) {
  const { translate } = useI18n();
  const visibleInputParams = (data.inputParams ?? []).filter((param) => param.display !== false);
  const visibleParams = visibleInputParams.filter((param) => !param.additionalParams).slice(0, 4);
  const additionalCount = visibleInputParams.filter((param) => param.additionalParams).length;
  const title = String(data.label ?? data.displayName);
  const isStartAgentflow = data.name === 'startAgentflow' || data.nodeType === 'startAgentflow';
  const status = typeof data.status === 'string' ? data.status.toUpperCase() : '';
  const statusClassName = status ? `flowise-node-card--${status.toLowerCase()}` : '';

  return (
    <div className={['flowise-node-card', statusClassName, selected ? 'flowise-node-card--selected' : ''].filter(Boolean).join(' ')}>
      <FlowCanvasInputHandles
        anchors={data.inputAnchors}
        rootClassName="flowise-node-handles flowise-node-handles--inputs"
        rowClassName="flowise-node-handle-row"
      />
      <div className="flowise-node-card__header">
        <span className="flowise-node-card__icon">
          <AppIcon name={data.icon ?? 'module'} />
        </span>
        <div>
          <strong>{title}</strong>
          <span>{data.category ?? data.nodeType}</span>
        </div>
        {data.version ? <em>{`v${data.version}`}</em> : null}
      </div>
      {status ? (
        <div className="flowise-node-card__status" data-status={status}>
          <span>{status}</span>
          {data.error ? <small>{data.error}</small> : null}
        </div>
      ) : null}
      {data.description ? <p className="flowise-node-card__description">{data.description}</p> : null}
      {visibleParams.length > 0 ? (
        <div className="flowise-node-card__params">
          {visibleParams.map((param) => (
            <span key={param.name}>{param.label}</span>
          ))}
          {additionalCount > 0 ? <span>+{additionalCount}</span> : null}
        </div>
      ) : null}
      <div className="flowise-node-card__tools">
        <IconButton data-flowise-node-action="info" size="small" title={translate(flowiseI18nKeys.canvas.info)} type="button">
          <AppIcon name="info" />
        </IconButton>
        {!isStartAgentflow ? (
          <IconButton data-flowise-node-action="duplicate" size="small" title={translate(flowiseI18nKeys.actions.duplicate)} type="button">
            <AppIcon name="copy" />
          </IconButton>
        ) : null}
        <IconButton data-flowise-node-action="delete" size="small" title={translate(flowiseI18nKeys.actions.delete)} type="button">
          <AppIcon name="trash" />
        </IconButton>
      </div>
      <FlowCanvasOutputHandles
        anchors={data.outputAnchors}
        rootClassName="flowise-node-handles flowise-node-handles--outputs"
        rowClassName="flowise-node-handle-row"
      />
    </div>
  );
}
