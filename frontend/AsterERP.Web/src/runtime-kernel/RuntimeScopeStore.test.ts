import { describe, expect, it } from 'vitest';

import { RuntimeScopeStore } from './RuntimeScopeStore';

describe('RuntimeScopeStore', () => {
  it('reads local values and inherited values, while writes remain local', () => {
    const store = new RuntimeScopeStore();
    const page = store.create('page', { tenant: { id: 'tenant-a' }, inherited: true });
    const component = store.create('component', { local: 'component' }, page);

    expect(store.read(component, 'tenant.id')).toBe('tenant-a');
    store.write(component, 'tenant.id', 'tenant-b');
    expect(store.read(component, 'tenant.id')).toBe('tenant-b');
    expect(store.read(page, 'tenant.id')).toBe('tenant-a');
    expect(store.inherit(component)?.id).toBe(page);
  });

  it('keeps an explicit undefined write local and destroys descendants with the owner scope', () => {
    const store = new RuntimeScopeStore();
    const page = store.create('page', { value: 'page' });
    const form = store.create('form', {}, page);
    const row = store.create('row', {}, form);

    store.write(form, 'value', undefined);
    expect(store.read(form, 'value')).toBeUndefined();
    store.destroy(form);
    expect(store.get(form)).toBeNull();
    expect(store.get(row)).toBeNull();
    expect(store.get(page)).not.toBeNull();
  });

  it('materializes inherited values as runtime scope records', () => {
    const store = new RuntimeScopeStore();
    const page = store.create('page', { locale: 'zh-TW' });
    store.create('modal', { modalId: 'edit' }, page);

    expect(store.snapshot().scopes).toEqual({
      page: { locale: 'zh-TW' },
      modal: { locale: 'zh-TW', modalId: 'edit' }
    });
  });

  it('isolates nested values on create, read, write, and snapshot boundaries', () => {
    const input = { nested: { items: [{ value: 'initial' }] } };
    const store = new RuntimeScopeStore();
    const page = store.create('page', input);

    input.nested.items[0].value = 'mutated-input';
    expect(store.read(page, 'nested.items.0.value')).toBe('initial');

    const readValue = store.read(page, 'nested') as { items: Array<{ value: string }> };
    readValue.items[0].value = 'mutated-read';
    expect(store.read(page, 'nested.items.0.value')).toBe('initial');

    store.write(page, 'nested.items', [{ value: 'written' }]);
    const snapshot = store.snapshot();
    snapshot.scopes.page.nested = { items: [{ value: 'mutated-snapshot' }] };
    expect(store.read(page, 'nested.items.0.value')).toBe('written');
  });
});
