import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextBlockquoteInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.blockquote');
    this.onlyInherited();
  }
}
