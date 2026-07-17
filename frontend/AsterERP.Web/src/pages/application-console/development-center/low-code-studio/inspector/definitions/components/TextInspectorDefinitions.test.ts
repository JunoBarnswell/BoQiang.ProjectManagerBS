import { describe, expect, it } from 'vitest';

import { TextBlockquoteInspectorDefinition } from './TextBlockquoteInspectorDefinition';
import { TextBrInspectorDefinition } from './TextBrInspectorDefinition';
import { TextCodeInspectorDefinition } from './TextCodeInspectorDefinition';
import { TextEmInspectorDefinition } from './TextEmInspectorDefinition';
import { TextH1InspectorDefinition } from './TextH1InspectorDefinition';
import { TextH2InspectorDefinition } from './TextH2InspectorDefinition';
import { TextH3InspectorDefinition } from './TextH3InspectorDefinition';
import { TextH4InspectorDefinition } from './TextH4InspectorDefinition';
import { TextH5InspectorDefinition } from './TextH5InspectorDefinition';
import { TextH6InspectorDefinition } from './TextH6InspectorDefinition';
import { TextHeadingInspectorDefinition } from './TextHeadingInspectorDefinition';
import { TextHrInspectorDefinition } from './TextHrInspectorDefinition';
import { TextInspectorDefinition } from './TextInspectorDefinition';
import { TextLinkInspectorDefinition } from './TextLinkInspectorDefinition';
import { TextMarkInspectorDefinition } from './TextMarkInspectorDefinition';
import { TextParagraphInspectorDefinition } from './TextParagraphInspectorDefinition';
import { TextPreInspectorDefinition } from './TextPreInspectorDefinition';
import { TextQuoteInspectorDefinition } from './TextQuoteInspectorDefinition';
import { TextSmallInspectorDefinition } from './TextSmallInspectorDefinition';
import { TextStrongInspectorDefinition } from './TextStrongInspectorDefinition';
import { TextTimeInspectorDefinition } from './TextTimeInspectorDefinition';

const definitions = [
  ['text', TextInspectorDefinition],
  ['text.paragraph', TextParagraphInspectorDefinition],
  ['text.heading', TextHeadingInspectorDefinition],
  ['text.h1', TextH1InspectorDefinition],
  ['text.h2', TextH2InspectorDefinition],
  ['text.h3', TextH3InspectorDefinition],
  ['text.h4', TextH4InspectorDefinition],
  ['text.h5', TextH5InspectorDefinition],
  ['text.h6', TextH6InspectorDefinition],
  ['text.link', TextLinkInspectorDefinition],
  ['text.em', TextEmInspectorDefinition],
  ['text.strong', TextStrongInspectorDefinition],
  ['text.small', TextSmallInspectorDefinition],
  ['text.mark', TextMarkInspectorDefinition],
  ['text.blockquote', TextBlockquoteInspectorDefinition],
  ['text.quote', TextQuoteInspectorDefinition],
  ['text.code', TextCodeInspectorDefinition],
  ['text.pre', TextPreInspectorDefinition],
  ['text.br', TextBrInspectorDefinition],
  ['text.hr', TextHrInspectorDefinition],
  ['text.time', TextTimeInspectorDefinition],
] as const;

describe('Text inspector definitions', () => {
  it('defines the shared props.text contract for all 21 text components', () => {
    expect(definitions).toHaveLength(21);

    for (const [componentType, Definition] of definitions) {
      const definition = new Definition().build();
      const text = definition.properties.find((property) => property.path === 'props.text');

      expect(definition.componentType).toBe(componentType);
      expect(definition.onlyInherited).toBe(true);
      expect(text).toMatchObject({ defaultValue: 'Text', runtimeConsumer: 'runtime.component.props.text', valueType: 'string' });
    }
  });

  it('declares only the href and target properties that the link renderer consumes', () => {
    const link = new TextLinkInspectorDefinition().build();
    const linkProperties = link.properties.filter((property) => property.path === 'props.href' || property.path === 'props.target');

    expect(linkProperties).toMatchObject([
      { defaultValue: '#', path: 'props.href', runtimeConsumer: 'runtime.component.props.href', valueType: 'string' },
      { defaultValue: '_self', path: 'props.target', runtimeConsumer: 'runtime.component.props.target', valueType: 'string' },
    ]);
    expect(new TextInspectorDefinition().build().properties.some((property) => property.path === 'props.href')).toBe(false);
    expect(new TextTimeInspectorDefinition().build().properties.some((property) => property.path === 'props.dateTime' || property.path === 'props.datetime')).toBe(false);
  });
});
