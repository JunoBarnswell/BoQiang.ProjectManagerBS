import type { ComponentManifest } from '../pages/application-console/development-center/low-code-studio/components/ComponentManifest';
import { latestComponentManifests } from '../pages/application-console/development-center/low-code-studio/components/latestComponentManifestCatalog';

import { hasButtonRuntimeRenderer, renderButtonRuntime } from './RuntimeButtonRenderer';
import { hasChartRuntimeRenderer, renderChartRuntime } from './RuntimeChartRenderer';
import { hasChoiceRuntimeRenderer, renderChoiceRuntime } from './RuntimeChoiceRenderer';
import type { RuntimeComponentRenderer } from './RuntimeComponentTypes';
import { hasDataTableRuntimeRenderer, renderDataTableRuntime } from './RuntimeDataTableRenderer';
import { hasInputRuntimeRenderer, renderInputRuntime } from './RuntimeInputRenderer';
import { hasMetricRuntimeRenderer, renderMetricRuntime } from './RuntimeMetricRenderer';
import { hasStandardRuntimeRenderer, renderStandardRuntime } from './RuntimeStandardRenderer';
import { hasTableRuntimeRenderer, renderTableRuntime } from './RuntimeTableRenderer';

export class RuntimeComponentRegistry {
  private readonly renderers: ReadonlyMap<string, RuntimeComponentRenderer>;

  public constructor(entries: ReadonlyMap<string, RuntimeComponentRenderer>) {
    this.renderers = new Map(entries);
  }

  public get(type: string): RuntimeComponentRenderer | undefined {
    return this.renderers.get(type);
  }

  public has(type: string): boolean {
    return this.renderers.has(type);
  }

  public types(): readonly string[] {
    return [...this.renderers.keys()];
  }
}

type RuntimeRendererFactory = (type: string) => RuntimeComponentRenderer | undefined;

const rendererFactories: readonly RuntimeRendererFactory[] = [
  (type) => hasButtonRuntimeRenderer(type) ? renderButtonRuntime : undefined,
  (type) => hasChoiceRuntimeRenderer(type) ? renderChoiceRuntime : undefined,
  (type) => hasDataTableRuntimeRenderer(type) ? renderDataTableRuntime : undefined,
  (type) => hasInputRuntimeRenderer(type) ? renderInputRuntime : undefined,
  (type) => hasMetricRuntimeRenderer(type) ? renderMetricRuntime : undefined,
  (type) => hasTableRuntimeRenderer(type) ? renderTableRuntime : undefined,
  (type) => hasChartRuntimeRenderer(type) ? renderChartRuntime : undefined,
  (type) => hasStandardRuntimeRenderer(type) ? renderStandardRuntime : undefined
];

function resolveNativeRenderer(type: string): RuntimeComponentRenderer | undefined {
  for (const factory of rendererFactories) {
    const renderer = factory(type);
    if (renderer) return renderer;
  }
  return undefined;
}

function resolveManifestRenderer(manifest: ComponentManifest): RuntimeComponentRenderer | undefined {
  const nativeRenderer = resolveNativeRenderer(manifest.type);
  if (nativeRenderer) return nativeRenderer;

  switch (manifest.runtime.renderer) {
    case 'button':
      return renderButtonRuntime;
    case 'container':
    case 'text':
    case 'standard':
      return renderStandardRuntime;
    case 'input':
      return (context) => renderInputRuntime({ ...context, componentType: 'input.text' });
    case 'textarea':
      return (context) => renderInputRuntime({ ...context, componentType: 'input.textarea' });
    case 'file':
      return (context) => renderInputRuntime({ ...context, componentType: 'input.file' });
    case 'signature':
      return (context) => renderInputRuntime({ ...context, componentType: 'media.signature' });
    case 'dialog':
      return (context) => renderStandardRuntime({ ...context, componentType: 'interaction.dialog' });
    case 'drawer':
      return (context) => renderStandardRuntime({ ...context, componentType: 'interaction.popover' });
    case 'chart':
      return renderChartRuntime;
    default:
      return undefined;
  }
}

function createRendererEntries(): ReadonlyMap<string, RuntimeComponentRenderer> {
  const entries = new Map<string, RuntimeComponentRenderer>();
  for (const manifest of latestComponentManifests) {
    const renderer = resolveManifestRenderer(manifest);
    if (!renderer) {
      throw new Error(`No runtime renderer registered for manifest type: ${manifest.type}`);
    }
    entries.set(manifest.type, renderer);
  }
  return entries;
}

const rendererEntries = createRendererEntries();

export const runtimeComponentRegistry = new RuntimeComponentRegistry(rendererEntries);

export function createRuntimeManifestRegistry(): ReadonlyMap<string, ComponentManifest> {
  return new Map<string, ComponentManifest>(latestComponentManifests.map((manifest) => [manifest.type, manifest]));
}
