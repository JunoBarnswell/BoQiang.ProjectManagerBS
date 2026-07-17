import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class SemanticDivInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('semantic.div');
    this.onlyInherited();
  }
}
