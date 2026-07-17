import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text');
    this.onlyInherited();
  }
}
