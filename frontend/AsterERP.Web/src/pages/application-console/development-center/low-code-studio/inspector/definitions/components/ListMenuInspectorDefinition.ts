import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class ListMenuInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('list.menu');
    this.onlyInherited();
  }
}
