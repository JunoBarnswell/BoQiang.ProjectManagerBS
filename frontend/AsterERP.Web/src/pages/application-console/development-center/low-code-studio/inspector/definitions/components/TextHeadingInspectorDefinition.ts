import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextHeadingInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.heading');
    this.onlyInherited();
  }
}
