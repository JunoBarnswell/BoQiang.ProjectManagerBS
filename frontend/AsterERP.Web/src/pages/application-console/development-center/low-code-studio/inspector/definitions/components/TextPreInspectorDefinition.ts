import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextPreInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.pre');
    this.onlyInherited();
  }
}
