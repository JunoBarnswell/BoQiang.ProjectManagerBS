import type { DesignerDocumentNode } from '../document/DesignerDocument';
import type { LayoutMode } from '../layout/layoutOperations';

export interface MoveNodesRequest {
  breakpointId?: string | null;
  insertionIndex?: number;
  layoutPatches?: Readonly<Record<string, DesignerDocumentNode['layout']>>;
  nodeIds: readonly string[];
  parentId: string | null;
  targetLayoutMode?: LayoutMode;
}
