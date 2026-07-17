import type { RuntimeDiagnostic } from './Diagnostics';

export interface RuntimePermissionContext {
  granted: ReadonlySet<string>;
  isSystemAdmin?: boolean;
}

export interface RuntimePermissionRequirement {
  code?: string | null;
  required?: boolean;
}

export class PermissionEvaluator {
  public constructor(private readonly context: RuntimePermissionContext) {}

  public can(requirement: RuntimePermissionRequirement | null | undefined): boolean {
    if (!requirement?.required && !requirement?.code) return true;
    if (this.context.isSystemAdmin) return true;
    return Boolean(requirement.code && this.context.granted.has(requirement.code));
  }

  public diagnose(requirement: RuntimePermissionRequirement | null | undefined, path: string): RuntimeDiagnostic | null {
    return this.can(requirement)
      ? null
      : { code: 'permissionDenied', message: `Permission is required for ${path}`, path, severity: 'error' };
  }
}
