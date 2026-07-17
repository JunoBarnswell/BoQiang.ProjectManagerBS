import type { ComponentInspectorDefinitionBase } from '../definitions/base/ComponentInspectorDefinitionBase';

import { ComponentInspectorRegistry } from './ComponentInspectorRegistry';

export class ComponentInspectorRegistryBuilder {
  private readonly definitions: ComponentInspectorDefinitionBase[] = [];

  public register(definition: ComponentInspectorDefinitionBase): this {
    this.definitions.push(definition);
    return this;
  }

  public registerAll(definitions: readonly ComponentInspectorDefinitionBase[]): this {
    definitions.forEach((definition) => this.register(definition));
    return this;
  }

  public build(expectedTypes?: readonly string[]): ComponentInspectorRegistry {
    const registry = new ComponentInspectorRegistry(this.definitions);
    registry.assertValid(expectedTypes);
    return registry;
  }
}
