import { describe, expect, it } from 'vitest';

import { RUNTIME_CAPABILITY_CONTRACT } from '../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract';

import { lowCodeStudioMessagesEnUS } from './lowCodeStudioMessages.en-US';
import { lowCodeStudioMessagesZhCN } from './lowCodeStudioMessages.zh-CN';

describe('low-code studio message catalogs', () => {
  it('provides a human-facing label for every runtime component in both locales', () => {
    for (const type of RUNTIME_CAPABILITY_CONTRACT.components) {
      const key = `lowCode.component.${type.replaceAll('.', '_')}.label`;
      expect(lowCodeStudioMessagesZhCN[key]).toBeTruthy();
      expect(lowCodeStudioMessagesZhCN[key]).not.toContain('组件 ');
      expect(lowCodeStudioMessagesEnUS[key]).toBeTruthy();
    }
  });
});
