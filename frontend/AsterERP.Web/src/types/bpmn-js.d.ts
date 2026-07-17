declare module 'bpmn-js/lib/Modeler' {
  export default class BpmnModeler {
    constructor(options: Record<string, unknown>);
    attachTo(container: HTMLElement): void;
    destroy(): void;
    get<T = unknown>(serviceName: string): T;
    importXML(xml: string): Promise<{ warnings: unknown[] }>;
    saveXML(options?: Record<string, unknown>): Promise<{ xml?: string }>;
  }
}

declare module 'bpmn-js/lib/Viewer' {
  export default class BpmnViewer {
    constructor(options: Record<string, unknown>);
    destroy(): void;
    get<T = unknown>(serviceName: string): T;
    importXML(xml: string): Promise<{ warnings: unknown[] }>;
  }
}

declare module 'bpmn-js-properties-panel' {
  export const BpmnPropertiesPanelModule: unknown;
  export const BpmnPropertiesProviderModule: unknown;
}

declare module 'diagram-js-minimap' {
  const minimapModule: unknown;
  export default minimapModule;
}

declare module '*.css';
