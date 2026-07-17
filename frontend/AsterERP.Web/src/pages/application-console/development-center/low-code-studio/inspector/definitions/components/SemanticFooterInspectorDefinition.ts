import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class SemanticFooterInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('semantic.footer');
    this.onlyInherited();
  }
}
