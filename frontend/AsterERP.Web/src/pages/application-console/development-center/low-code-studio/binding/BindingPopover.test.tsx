// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import { I18nProvider, localeStorageKey } from '../../../../../core/i18n/I18nProvider';
import { loadLocaleMessages } from '../../../../../core/i18n/messageLoader';

import { BindingPopover } from './BindingPopover';

describe('BindingPopover latest PropertyValue flow', () => {
  beforeAll(async () => {
    window.localStorage.setItem(localeStorageKey, 'zh-CN');
    await loadLocaleMessages('zh-CN');
  });
  afterEach(cleanup);

  it('unbinding reports null without writing a bindings.props path', () => {
    const onChange = vi.fn();
    render(<I18nProvider><BindingPopover
      expectedType="string"
      expression={{ expectedType: 'string', graph: { root: { kind: 'literal', value: 'bound', valueType: 'string' } }, helpers: [] }}
      onChange={onChange}
    /></I18nProvider>);

    fireEvent.click(screen.getByRole('button', { name: '解除绑定' }));

    expect(onChange).toHaveBeenCalledWith(null);
  });
});
