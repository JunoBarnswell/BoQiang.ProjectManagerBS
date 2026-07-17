import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextH4InspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.h4');
    this.onlyInherited();
  }
}
