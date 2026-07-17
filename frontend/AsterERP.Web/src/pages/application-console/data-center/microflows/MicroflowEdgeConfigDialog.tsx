import { GitBranch, Trash2 } from 'lucide-react';

import type { MicroflowDefinition, MicroflowEdge } from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { ResponsiveModal } from '../../../../shared/responsive/ResponsiveModal';

import { deleteMicroflowCanvasEdge, updateMicroflowEdgeCondition } from './microflowCanvasModel';

interface MicroflowEdgeConfigDialogProps {
  definition: MicroflowDefinition;
  edge: MicroflowEdge | null;
  open: boolean;
  onChange: (definition: MicroflowDefinition) => void;
  onClose: () => void;
  onSelectEdge: (edgeId: string | null) => void;
}

export function MicroflowEdgeConfigDialog({
  definition,
  edge,
  open,
  onChange,
  onClose,
  onSelectEdge
}: MicroflowEdgeConfigDialogProps) {
  if (!edge) {
    return null;
  }

  const sourceNode = definition.nodes.find((node) => node.id === edge.sourceNodeId) ?? null;
  const targetNode = definition.nodes.find((node) => node.id === edge.targetNodeId) ?? null;

  return (
    <ResponsiveModal
      bodyClassName="microflow-edge-config-dialog__body"
      className="microflow-edge-config-dialog"
      maxWidth={520}
      mode="modal"
      open={open}
      title={translateCurrentLiteral("连线配置")}
      onClose={onClose}
    >
      <div className="microflow-edge-config-dialog__shell">
        <section className="microflow-edge-config-dialog__summary">
          <GitBranch className="h-4 w-4 text-primary-500" />
          <div>
            <strong>{sourceNode?.name ?? edge.sourceNodeId}</strong>
            <span>到 {targetNode?.name ?? edge.targetNodeId}</span>
          </div>
        </section>

        <label className="microflow-node-config-field">
          <span>{translateCurrentLiteral("条件表达式")}</span>
          <textarea
            className="form-input min-h-24 text-xs"
            placeholder={translateCurrentLiteral("Decision 分支可填写 true / false 或表达式")}
            value={edge.condition ?? ''}
            onChange={(event) => onChange(updateMicroflowEdgeCondition(definition, edge.id, event.target.value))}
          />
        </label>

        <footer className="microflow-edge-config-dialog__actions">
          <button
            className="danger-button h-8 text-xs"
            type="button"
            onClick={() => {
              onChange(deleteMicroflowCanvasEdge(definition, edge.id));
              onSelectEdge(null);
              onClose();
            }}
          >
            <Trash2 className="h-3.5 w-3.5" />{translateCurrentLiteral("删除连线")}</button>
          <button className="primary-button h-8 text-xs" type="button" onClick={onClose}>{translateCurrentLiteral("完成")}</button>
        </footer>
      </div>
    </ResponsiveModal>
  );
}
