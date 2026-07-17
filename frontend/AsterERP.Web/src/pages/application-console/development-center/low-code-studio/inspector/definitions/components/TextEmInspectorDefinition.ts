import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextEmInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.em');
    this.onlyInherited();
  }
}
