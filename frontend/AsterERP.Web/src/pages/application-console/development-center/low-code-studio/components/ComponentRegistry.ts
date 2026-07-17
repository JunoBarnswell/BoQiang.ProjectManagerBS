import { canContainComponent, canResizeComponent, resolveComponentInteractionPolicy, validateComponentInteractionPolicy, type ComponentResizeHandle } from './componentInteractionPolicy';
import type { ComponentManifest } from './ComponentManifest';

export interface ComponentRegistryDiagnostic {
  code: string;
  componentType: string;
  message: string;
}

export class ComponentRegistry {
  private readonly entries = new Map<string, ComponentManifest>();

  public constructor(manifests: readonly ComponentManifest[] = []) {
    manifests.forEach((manifest) => this.register(manifest));
    this.assertValid();
  }

  public register(manifest: ComponentManifest): void {
    if (this.entries.has(manifest.type)) throw new Error(`Duplicate component type: ${manifest.type}`);
    this.entries.set(manifest.type, manifest);
  }

  public get(type: string): ComponentManifest | undefined {
    return this.entries.get(type);
  }

  public getInteractionPolicy(type: string): ReturnType<typeof resolveComponentInteractionPolicy> | undefined {
    const manifest = this.entries.get(type);
    return manifest ? resolveComponentInteractionPolicy(manifest) : undefined;
  }

  public canContain(parentType: string, childType: string, parentLayout: Record<string, unknown> = {}): boolean {
    const parent = this.entries.get(parentType);
    const child = this.entries.get(childType);
    return Boolean(parent && child && canContainComponent(parent, child, parentLayout));
  }

  public canResize(type: string, handle: ComponentResizeHandle): boolean {
    const manifest = this.entries.get(type);
    return Boolean(manifest && canResizeComponent(manifest, handle));
  }

  public values(): readonly ComponentManifest[] {
    return [...this.entries.values()];
  }

  public validate(): ComponentRegistryDiagnostic[] {
    return [...this.entries.values()].flatMap((manifest) => validateManifest(manifest, this.entries));
  }

  public assertValid(): void {
    const diagnostics = this.validate();
    if (diagnostics.length > 0) {
      throw new Error(diagnostics.map((diagnostic) => `${diagnostic.code} (${diagnostic.componentType}): ${diagnostic.message}`).join('\n'));
    }
  }

  public validateRemoval(type: string, referencedTypes: readonly string[]): ComponentRegistryDiagnostic[] {
    const manifest = this.entries.get(type);
    if (!manifest || !referencedTypes.includes(type)) return [];
    const hasMigration = manifest.migrations.some((migration) => migration.from.trim().length > 0 && migration.migrate.trim().length > 0);
    return hasMigration
      ? []
      : [{ code: 'componentInUse', componentType: type, message: 'component cannot be removed while documents reference it without a migration' }];
  }

  public assertRemovable(type: string, referencedTypes: readonly string[]): void {
    const diagnostics = this.validateRemoval(type, referencedTypes);
    if (diagnostics.length > 0) throw new Error(diagnostics.map((item) => `${item.componentType}: ${item.message}`).join('\n'));
  }
}

function validateManifest(manifest: ComponentManifest, entries: ReadonlyMap<string, ComponentManifest>): ComponentRegistryDiagnostic[] {
  const diagnostics: ComponentRegistryDiagnostic[] = [];
  const add = (code: string, message: string) => diagnostics.push({ code, componentType: manifest.type, message });
  if (!manifest.type.trim()) add('missingType', 'component type is required');
  if (manifest.type.trim() !== manifest.type) add('invalidType', 'component type must be trimmed');
  if (!manifest.i18n || !manifest.i18n.labelKey.trim() || !manifest.i18n.helpKey.trim() || !manifest.i18n.diagnosticKey.trim()) add('missingI18n', 'label, help, and diagnostic message keys are required');
  if (!manifest.editor.inspector) add('missingInspectorDefinition', 'component inspector definition is required');
  else {
    if (manifest.editor.inspector.componentType !== manifest.type) add('inspectorTypeMismatch', 'inspector definition must belong to the manifest type');
    const paths = new Set<string>();
    for (const property of manifest.editor.inspector.properties) {
      if (!paths.add(property.path)) add('duplicateInspectorPath', `duplicate inspector property path: ${property.path}`);
      if (!property.editor) add('missingInspectorEditor', `inspector editor is required: ${property.path}`);
      if (!property.runtimeConsumer.trim()) add('missingInspectorRuntimeConsumer', `inspector Runtime consumer is required: ${property.path}`);
    }
  }
  if (!Array.isArray(manifest.capability.capabilities)) add('missingCapabilities', 'capability declaration is required');
  if (!manifest.runtime.renderer.trim()) add('missingRuntimeRenderer', 'runtime renderer is required');
  if (manifest.runtime.supportedScopes.length === 0) add('missingRuntimeScopes', 'at least one runtime scope is required');
  if (!manifest.editor.previewRenderer.trim()) add('missingPreviewRenderer', 'editor preview renderer is required');
  if (manifest.editor.inspectorSections.length === 0) add('missingInspectorSchema', 'at least one inspector section is required');
  if (manifest.editor.inspectorSections.some((section) => !section.trim())) add('invalidInspectorSchema', 'inspector sections must be non-empty');
  if (manifest.binding.acceptedTypes.length === 0) add('missingBindingTypes', 'at least one binding type is required');
  if (manifest.responsive.supportedLayouts.length === 0) add('missingResponsiveSchema', 'at least one responsive layout is required');
  if (manifest.security.requiresPermission && manifest.security.actionPermissions.some((permission) => !permission.trim())) add('invalidPermission', 'permission codes must be non-empty');
  if (!isRecord(manifest.validation.schema)) add('missingValidationSchema', 'validation schema is required');
  if (!isRecord(manifest.defaults.layout) || !isRecord(manifest.defaults.props) || !isRecord(manifest.defaults.style)) add('missingDefaults', 'layout, props, and style defaults are required');
  for (const diagnostic of validateComponentInteractionPolicy(manifest)) add(diagnostic.code, diagnostic.message);
  const eventNames = new Set<string>();
  for (const event of manifest.events) {
    if (!event.name.trim()) add('missingEventName', 'event name is required');
    if (!eventNames.add(event.name)) add('duplicateEvent', `duplicate event: ${event.name}`);
    if (!event.trigger.trim()) add('missingEventTrigger', `event ${event.name} has no trigger`);
    if (!isRecord(event.payloadSchema)) add('missingEventPayloadSchema', `event ${event.name} payload schema is required`);
  }
  const migrationSources = new Set<string>();
  for (const migration of manifest.migrations) {
    if (!migrationSources.add(migration.from)) add('duplicateMigration', `duplicate migration source: ${migration.from}`);
    if (!migration.migrate.trim()) add('missingMigration', `migration ${migration.from} has no implementation`);
    if (!migration.from.trim()) add('missingMigrationSource', 'migration source is required');
  }
  if (manifest.security.requiresPermission && manifest.security.actionPermissions.length === 0) add('missingPermission', 'permission declaration is required');
  if (!entries.has(manifest.type)) add('missingRegistration', 'manifest is not registered');
  return diagnostics;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}
