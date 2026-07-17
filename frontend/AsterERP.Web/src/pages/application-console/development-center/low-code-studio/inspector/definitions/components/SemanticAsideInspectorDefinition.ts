import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class SemanticAsideInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('semantic.aside');
    this.onlyInherited();
  }
}
