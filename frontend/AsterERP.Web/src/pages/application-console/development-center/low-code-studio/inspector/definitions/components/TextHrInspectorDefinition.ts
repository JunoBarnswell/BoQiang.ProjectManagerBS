import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextHrInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.hr');
    this.onlyInherited();
  }
}
