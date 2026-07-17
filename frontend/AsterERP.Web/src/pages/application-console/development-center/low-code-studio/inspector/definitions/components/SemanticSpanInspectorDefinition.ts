import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class SemanticSpanInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('semantic.span');
    this.onlyInherited();
  }
}
