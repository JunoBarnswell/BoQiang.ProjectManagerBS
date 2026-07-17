import {
  BaseEdge,
  EdgeLabelRenderer,
  getBezierPath,
  type EdgeProps
} from '@xyflow/react';

import type { FlowiseCanvasEdge } from '../../../types/canvas.types';

export function WorkflowEdge({
  data,
  markerEnd,
  sourcePosition,
  sourceX,
  sourceY,
  style,
  targetPosition,
  targetX,
  targetY
}: EdgeProps<FlowiseCanvasEdge>) {
  const [edgePath, labelX, labelY] = getBezierPath({
    sourcePosition,
    sourceX,
    sourceY,
    targetPosition,
    targetX,
    targetY
  });
  const label = data?.edgeLabel ?? data?.conditionLabel ?? data?.humanInputLabel ?? data?.label;
  const sourceColor = typeof data?.sourceColor === 'string' ? data.sourceColor : undefined;
  const targetColor = typeof data?.targetColor === 'string' ? data.targetColor : undefined;
  const stroke = sourceColor ?? targetColor ?? style?.stroke;

  return (
    <>
      <BaseEdge markerEnd={markerEnd} path={edgePath} style={{ ...style, stroke, strokeWidth: 2 }} />
      {label ? (
        <EdgeLabelRenderer>
          <span
            className="flowise-agent-edge-label"
            style={{ color: stroke, transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)` }}
          >
            {label}
          </span>
        </EdgeLabelRenderer>
      ) : null}
    </>
  );
}
