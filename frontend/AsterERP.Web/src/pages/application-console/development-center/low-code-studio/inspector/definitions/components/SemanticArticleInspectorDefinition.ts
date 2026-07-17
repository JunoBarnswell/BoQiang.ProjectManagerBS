import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class SemanticArticleInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('semantic.article');
    this.onlyInherited();
  }
}
