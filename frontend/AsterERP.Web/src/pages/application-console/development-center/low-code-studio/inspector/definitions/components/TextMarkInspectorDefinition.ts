import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextMarkInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.mark');
    this.onlyInherited();
  }
}
