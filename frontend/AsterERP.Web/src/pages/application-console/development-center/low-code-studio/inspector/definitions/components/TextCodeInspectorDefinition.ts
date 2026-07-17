import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextCodeInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.code');
    this.onlyInherited();
  }
}
