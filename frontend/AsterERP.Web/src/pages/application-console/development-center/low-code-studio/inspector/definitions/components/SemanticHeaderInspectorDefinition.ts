import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class SemanticHeaderInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('semantic.header');
    this.onlyInherited();
  }
}
