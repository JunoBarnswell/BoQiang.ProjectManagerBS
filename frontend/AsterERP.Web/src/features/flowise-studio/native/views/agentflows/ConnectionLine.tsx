import { useTheme } from '@mui/material/styles';
import { EdgeLabelRenderer, getBezierPath, useStore, type ConnectionLineComponentProps } from '@xyflow/react';
import { memo } from 'react';


import type { FlowiseCanvasNode } from '../../../types/canvas.types';

const agentflowIconColors: Record<string, string> = {
  conditionAgentAgentflow: '#7c3aed',
  conditionAgentflow: '#7c3aed',
  humanInputAgentflow: '#f97316'
};

function resolveConnectionNodeName(connectionHandleId: string | null | undefined) {
  return (connectionHandleId ?? '').split('_')[0] ?? '';
}

function resolveConnectionLabel(connectionHandleId: string | null | undefined, nodeName: string) {
  if (!connectionHandleId) {
    return undefined;
  }

  if (nodeName === 'conditionAgentflow' || nodeName === 'conditionAgentAgentflow') {
    const edgeLabel = connectionHandleId.split('-').pop();
    return Number.isNaN(Number(edgeLabel)) ? '0' : String(edgeLabel);
  }

  if (nodeName === 'humanInputAgentflow') {
    const edgeLabel = connectionHandleId.split('-').pop();
    const resolved = Number.isNaN(Number(edgeLabel)) ? '0' : String(edgeLabel);
    return resolved === '0' ? 'proceed' : 'reject';
  }

  return undefined;
}

function ConnectionLineComponent({ fromPosition, fromX, fromY, toPosition, toX, toY }: ConnectionLineComponentProps<FlowiseCanvasNode>) {
  const [edgePath] = getBezierPath({
    sourcePosition: fromPosition,
    sourceX: fromX,
    sourceY: fromY,
    targetPosition: toPosition,
    targetX: toX,
    targetY: toY
  });
  const connectionHandleId = useStore((state) => state.connection.fromHandle?.id);
  const theme = useTheme();
  const nodeName = resolveConnectionNodeName(connectionHandleId);
  const label = resolveConnectionLabel(connectionHandleId, nodeName);
  const color = agentflowIconColors[nodeName] ?? theme.palette.primary.main;
  const humanInput = nodeName === 'humanInputAgentflow';

  return (
    <g>
      <path className="animated" d={edgePath} fill="none" stroke={color} strokeWidth={1.5} />
      <g transform={`translate(${toX - 10}, ${toY - 10}) scale(0.8)`}>
        <path d="M0 0h24v24H0z" fill="none" stroke="none" />
        <path
          d="M12 2c5.523 0 10 4.477 10 10a10 10 0 0 1 -20 0c0 -5.523 4.477 -10 10 -10m-.293 6.293a1 1 0 0 0 -1.414 0l-.083 .094a1 1 0 0 0 .083 1.32l2.292 2.293l-2.292 2.293a1 1 0 0 0 1.414 1.414l3 -3a1 1 0 0 0 0 -1.414z"
          fill={color}
        />
      </g>
      {label ? (
        <EdgeLabelRenderer>
          <div
            className="nodrag nopan flowise-agent-connection-label"
            style={{
              color,
              left: humanInput ? 20 : 10,
              transform: `translate(-50%, 0%) translate(${fromX}px, ${fromY}px)`
            }}
          >
            {label}
          </div>
        </EdgeLabelRenderer>
      ) : null}
    </g>
  );
}

export const ConnectionLine = memo(ConnectionLineComponent);
