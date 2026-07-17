import type { DesignerDocument } from '../document/DesignerDocument';

export interface DesignerCommandContext {
  readonly document: DesignerDocument;
}

export interface DesignerCommandResult {
  readonly diagnostics: readonly string[];
  readonly document: DesignerDocument;
  readonly changed: boolean;
  readonly inverse?: DesignerCommand;
}

export interface DesignerCommand {
  readonly id: string;
  readonly label: string;
  readonly mergeKey?: string;
  readonly execute: (context: DesignerCommandContext) => DesignerCommandResult;
  readonly inverse?: DesignerCommand;
}

export interface DesignerCommandExecutor {
  execute(command: DesignerCommand): DesignerCommandResult;
}

export function hasBlockingDesignerDiagnostics(diagnostics: readonly string[]): boolean {
  return diagnostics.some((diagnostic) => !diagnostic.startsWith('warning:'));
}
