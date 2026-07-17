import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class SemanticSectionInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('semantic.section');
    this.onlyInherited();
  }
}
