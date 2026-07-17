import {
  getBezierPath,
  type ConnectionLineComponentProps
} from '@xyflow/react';
import { memo } from 'react';

import type { MicroflowCanvasNode } from './microflowCanvasModel';

function MicroflowConnectionLineComponent({
  fromPosition,
  fromX,
  fromY,
  toPosition,
  toX,
  toY
}: ConnectionLineComponentProps<MicroflowCanvasNode>) {
  const [edgePath] = getBezierPath({
    sourcePosition: fromPosition,
    sourceX: fromX,
    sourceY: fromY,
    targetPosition: toPosition,
    targetX: toX,
    targetY: toY
  });

  return (
    <g>
      <path
        className="animated microflow-connection-line"
        d={edgePath}
        fill="none"
        stroke="#2563eb"
        strokeWidth={1.8}
      />
      <g transform={`translate(${toX - 9}, ${toY - 9}) scale(0.75)`}>
        <path d="M0 0h24v24H0z" fill="none" stroke="none" />
        <path
          d="M12 2c5.523 0 10 4.477 10 10a10 10 0 0 1 -20 0c0 -5.523 4.477 -10 10 -10m-.293 6.293a1 1 0 0 0 -1.414 0l-.083 .094a1 1 0 0 0 .083 1.32l2.292 2.293l-2.292 2.293a1 1 0 0 0 1.414 1.414l3 -3a1 1 0 0 0 0 -1.414z"
          fill="#2563eb"
        />
      </g>
    </g>
  );
}

export const MicroflowConnectionLine = memo(MicroflowConnectionLineComponent);
