import {
  BaseEdge,
  EdgeLabelRenderer,
  getBezierPath,
  useReactFlow,
  type Edge,
  type EdgeProps
} from '@xyflow/react';

export interface FlowCanvasButtonEdgeData extends Record<string, unknown> {
  onDeleteEdge?: (edgeId: string) => void;
}

export function FlowCanvasButtonEdge({
  data,
  id,
  label,
  markerEnd,
  sourcePosition,
  sourceX,
  sourceY,
  style,
  targetPosition,
  targetX,
  targetY
}: EdgeProps<Edge<FlowCanvasButtonEdgeData>>) {
  const { setEdges } = useReactFlow();
  const [edgePath, labelX, labelY] = getBezierPath({
    sourcePosition,
    sourceX,
    sourceY,
    targetPosition,
    targetX,
    targetY
  });

  return (
    <>
      <BaseEdge markerEnd={markerEnd} path={edgePath} style={style} />
      <EdgeLabelRenderer>
        <div className="nodrag nopan flow-canvas-edge-label" style={{ transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)` }}>
          {label || data?.label ? <span>{String(label ?? data?.label)}</span> : null}
          <button
            aria-label="Delete edge"
            className="nodrag nopan flow-canvas-edge-delete"
            title="Delete edge"
            type="button"
            onClick={() => {
              if (data?.onDeleteEdge) {
                data.onDeleteEdge(id);
                return;
              }

              setEdges((edges) => edges.filter((edge) => edge.id !== id));
            }}
          >
            ×
          </button>
        </div>
      </EdgeLabelRenderer>
    </>
  );
}
