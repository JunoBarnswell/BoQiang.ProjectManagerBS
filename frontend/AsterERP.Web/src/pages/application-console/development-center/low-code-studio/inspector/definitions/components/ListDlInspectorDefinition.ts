import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class ListDlInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('list.dl');
    this.onlyInherited();
  }
}
