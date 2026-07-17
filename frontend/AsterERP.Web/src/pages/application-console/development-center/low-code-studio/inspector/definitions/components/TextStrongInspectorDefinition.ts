import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextStrongInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.strong');
    this.onlyInherited();
  }
}
