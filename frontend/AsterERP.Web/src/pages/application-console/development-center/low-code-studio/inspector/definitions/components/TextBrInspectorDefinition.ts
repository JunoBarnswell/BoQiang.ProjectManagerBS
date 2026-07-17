import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextBrInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.br');
    this.onlyInherited();
  }
}
