import { describe, expect, it } from 'vitest';

import type { TaskCardGroup } from './taskCardProjectionModel';
import { moveTaskGroup, orderTaskGroups, toggleTaskGroup } from './taskGroupPreferenceModel';

const group = (key: string): TaskCardGroup => ({ key, label: key, rows: [] });

describe('taskGroupPreferenceModel', () => {
  it('orders known groups and keeps new groups after saved groups', () => {
    expect(orderTaskGroups([group('new'), group('b'), group('a')], { collapsedKeys: [], orderedKeys: ['a', 'b'] }).map((item) => item.key)).toEqual(['a', 'b', 'new']);
  });

  it('toggles collapse and moves a group without duplicate keys', () => {
    const collapsed = toggleTaskGroup({ collapsedKeys: [], orderedKeys: ['a', 'b'] }, 'a');
    expect(collapsed.collapsedKeys).toEqual(['a']);
    expect(moveTaskGroup(collapsed, 'b', 'a').orderedKeys).toEqual(['b', 'a']);
  });
});
