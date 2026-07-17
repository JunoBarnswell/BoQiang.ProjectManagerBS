import type { NodeProps } from '@xyflow/react';

import { FlowiseCanvasNode } from '../../../canvas/FlowiseCanvasNode';
import type { FlowiseCanvasNode as FlowiseCanvasNodeType } from '../../../types/canvas.types';


export function WorkflowNode(props: NodeProps<FlowiseCanvasNodeType>) {
  return (
    <div className="flowise-agent-node">
      <FlowiseCanvasNode {...props} />
    </div>
  );
}
