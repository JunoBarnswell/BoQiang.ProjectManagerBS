export interface RuntimeActionManifest {
  cancelable: boolean;
  errorPolicy: 'continue' | 'stop';
  inputSchema: Record<string, unknown>;
  outputSchema: Record<string, unknown>;
  permissions: string[];
  sideEffect: 'none' | 'read' | 'write' | 'external';
  timeoutMs: number;
  triggers: string[];
  type: string;
}
