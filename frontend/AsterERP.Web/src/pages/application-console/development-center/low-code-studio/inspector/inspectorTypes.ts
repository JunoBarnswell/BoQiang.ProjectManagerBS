import type { DesignerCommandBus } from "../commands/DesignerCommandBus";
import type { ComponentManifest } from "../components/ComponentManifest";
import type { DesignerDocument, DesignerDocumentNode } from "../document/DesignerDocument";
import type { ResponsiveBreakpoint } from "../responsive/responsiveModel";

import type { InspectorPropertyDescriptor } from "./contract/InspectorPropertyDescriptor";

export type InspectorPropertyDefinition = InspectorPropertyDescriptor;

export interface InspectorPanelProps {
  document: DesignerDocument;
  selectedNodeIds: readonly string[];
  manifest?: ComponentManifest | null;
  manifests?: import("../components/ComponentRegistry").ComponentRegistry;
  bindingDocument?: Parameters<typeof import("../binding/BindingPopover").BindingPopover>[0]["document"];
  commandBus?: DesignerCommandBus;
  onDocumentChange?: (document: DesignerDocument) => void;
  onExpressionEdit?: (nodeId: string, propertyKey: string) => void;
  responsiveBreakpoints?: readonly ResponsiveBreakpoint[];
  selectedBreakpoint?: ResponsiveBreakpoint | null;
  className?: string;
}

export interface InspectorMutationContext {
  document: DesignerDocument;
  nodes: readonly DesignerDocumentNode[];
  commandBus?: DesignerCommandBus;
  onDocumentChange?: (document: DesignerDocument) => void;
}
