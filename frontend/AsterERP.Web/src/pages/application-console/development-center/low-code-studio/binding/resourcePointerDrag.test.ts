import { describe, expect, it } from 'vitest';

import { createResourceDropEvent, RESOURCE_DROP_EVENT } from './resourcePointerDrag';

describe('resource pointer drag contract', () => {
  it('creates a bubbling drop event with a stable resource id', () => {
    const event = createResourceDropEvent('resource:page:customer.name');

    expect(event.type).toBe(RESOURCE_DROP_EVENT);
    expect(event.bubbles).toBe(true);
    expect(event.detail).toEqual({ resourceId: 'resource:page:customer.name' });
  });
});
