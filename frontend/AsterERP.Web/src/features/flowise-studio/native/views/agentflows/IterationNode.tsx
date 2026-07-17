import type { NodeProps } from '@xyflow/react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { FlowiseCanvasNode } from '../../../canvas/FlowiseCanvasNode';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseCanvasNode as FlowiseCanvasNodeType } from '../../../types/canvas.types';

export function IterationNode(props: NodeProps<FlowiseCanvasNodeType>) {
  const { translate } = useI18n();
  return (
    <div className="flowise-iteration-node">
      <div className="flowise-iteration-node__label">{translate(flowiseI18nKeys.canvas.iteration)}</div>
      <FlowiseCanvasNode {...props} />
    </div>
  );
}
