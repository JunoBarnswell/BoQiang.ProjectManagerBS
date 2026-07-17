import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextParagraphInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.paragraph');
    this.onlyInherited();
  }
}
