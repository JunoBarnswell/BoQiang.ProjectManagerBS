import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextH6InspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.h6');
    this.onlyInherited();
  }
}
