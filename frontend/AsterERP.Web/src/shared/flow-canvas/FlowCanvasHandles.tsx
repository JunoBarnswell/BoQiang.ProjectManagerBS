import { Handle, Position } from '@xyflow/react';

export interface FlowCanvasHandleAnchor {
  id?: string | null;
  label?: string | null;
  name: string;
}

interface FlowCanvasHandlesProps {
  anchors?: FlowCanvasHandleAnchor[];
  defaultHandleId?: string;
  rootClassName?: string;
  rowClassName?: string;
  showDefaultOutput?: boolean;
  topStart?: number;
  topStep?: number;
}

export function FlowCanvasInputHandles({
  anchors = [],
  rootClassName = 'flow-canvas-node-handles flow-canvas-node-handles--inputs',
  rowClassName = 'flow-canvas-node-handle-row',
  topStart = 82,
  topStep = 28
}: FlowCanvasHandlesProps) {
  if (anchors.length === 0) {
    return null;
  }

  return (
    <div className={rootClassName}>
      {anchors.map((anchor, index) => (
        <div className={rowClassName} key={anchor.id ?? anchor.name}>
          <Handle
            id={anchor.id ?? anchor.name}
            position={Position.Left}
            style={{ top: topStart + index * topStep }}
            type="target"
          />
          <span>{anchor.label ?? anchor.name}</span>
        </div>
      ))}
    </div>
  );
}

export function FlowCanvasOutputHandles({
  anchors = [],
  defaultHandleId = 'output',
  rootClassName = 'flow-canvas-node-handles flow-canvas-node-handles--outputs',
  rowClassName = 'flow-canvas-node-handle-row',
  showDefaultOutput = true,
  topStart = 82,
  topStep = 28
}: FlowCanvasHandlesProps) {
  if (anchors.length === 0) {
    return showDefaultOutput ? <Handle id={defaultHandleId} position={Position.Right} type="source" /> : null;
  }

  return (
    <div className={rootClassName}>
      {anchors.map((anchor, index) => (
        <div className={rowClassName} key={anchor.id ?? anchor.name}>
          <span>{anchor.label ?? anchor.name}</span>
          <Handle
            id={anchor.id ?? anchor.name}
            position={Position.Right}
            style={{ top: topStart + index * topStep }}
            type="source"
          />
        </div>
      ))}
    </div>
  );
}
