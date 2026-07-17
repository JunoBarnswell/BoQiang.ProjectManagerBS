import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextQuoteInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.quote');
    this.onlyInherited();
  }
}
