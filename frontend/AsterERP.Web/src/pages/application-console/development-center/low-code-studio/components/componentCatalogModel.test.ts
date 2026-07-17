import { describe, expect, it } from 'vitest';

import { filterComponentCatalog } from './componentCatalogModel';
import type { ComponentManifest } from './ComponentManifest';

describe('component catalog model', () => {
  const manifests = [
    { type: 'text.paragraph' },
    { type: 'action.button' }
  ] as ComponentManifest[];

  it('keeps the compact catalog as one searchable list', () => {
    expect(filterComponentCatalog(manifests, '').map((item) => item.type)).toEqual(['action.button', 'text.paragraph']);
    expect(filterComponentCatalog(manifests, 'paragraph').map((item) => item.type)).toEqual(['text.paragraph']);
  });
});
