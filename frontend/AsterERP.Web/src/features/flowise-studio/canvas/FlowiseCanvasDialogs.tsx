import { useEffect } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { EditNodeDialog, type EditNodeDialogTab } from '../native/views/agentflows/EditNodeDialog';
import type { FlowiseCanvasEdge, FlowiseCanvasNode, FlowiseCanvasValidationResult } from '../types/canvas.types';

import { FlowiseValidationPanel } from './FlowiseValidationPanel';

type InspectorTab = EditNodeDialogTab;

interface FlowiseCanvasDialogsProps {
  edges: FlowiseCanvasEdge[];
  nodes: FlowiseCanvasNode[];
  selectedEdge?: FlowiseCanvasEdge | null;
  selectedNode?: FlowiseCanvasNode | null;
  validation?: FlowiseCanvasValidationResult | null;
  activeTab: InspectorTab;
  onNodeConfigChange: (nodeId: string, name: string, value: unknown) => void;
  onTabChange: (tab: InspectorTab) => void;
}

export function FlowiseCanvasDialogs({
  edges,
  nodes,
  selectedEdge,
  selectedNode,
  validation,
  activeTab,
  onNodeConfigChange,
  onTabChange
}: FlowiseCanvasDialogsProps) {
  const { translate } = useI18n();

  useEffect(() => {
    onTabChange('details');
  }, [onTabChange, selectedNode?.id, selectedEdge?.id]);

  return (
    <aside className="flowise-canvas-inspector">
      <div className="flowise-panel-title">{translate(flowiseI18nKeys.canvas.inspector)}</div>
      {selectedNode ? (
        <EditNodeDialog
          activeTab={activeTab}
          edges={edges}
          node={selectedNode}
          nodes={nodes}
          onNodeConfigChange={onNodeConfigChange}
          onTabChange={onTabChange}
        />
      ) : selectedEdge ? (
        <div className="flowise-inspector-body">
          <h3>{String(selectedEdge.label ?? selectedEdge.id)}</h3>
          <p>{selectedEdge.source} {'->'} {selectedEdge.target}</p>
          <pre>{JSON.stringify(selectedEdge.data ?? {}, null, 2)}</pre>
        </div>
      ) : (
        <div className="flowise-inspector-body">
          <p>{translate(flowiseI18nKeys.canvas.emptySelection)}</p>
        </div>
      )}
      <FlowiseValidationPanel result={validation} />
    </aside>
  );
}
