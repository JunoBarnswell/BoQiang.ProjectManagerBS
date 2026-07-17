// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { afterEach, describe, expect, it } from 'vitest';

import { QueryFieldSelector, type QueryFieldSelectorOption } from './QueryFieldSelector';
import type { QueryModelSelection } from './queryModelTypes';

const fields: QueryFieldSelectorOption[] = [
  { fieldResourceId: 'data:orders:id', label: 'o.id', nodeId: 'node:orders' },
  { fieldResourceId: 'data:customers:id', label: 'c.id', nodeId: 'node:customers' }
];

describe('QueryFieldSelector', () => {
  afterEach(() => cleanup());

  it('adds and selects a field as an inseparable node and field reference', async () => {
    const user = userEvent.setup();
    render(<QueryFieldSelectorHarness />);

    await user.click(screen.getByRole('button', { name: 'applicationConsole.dataCenter.queryModel.addField' }));
    const fieldSelect = screen.getByRole('combobox', { name: /applicationConsole\.dataCenter\.queryModel\.field 1/ });
    await user.selectOptions(fieldSelect, screen.getAllByRole('option', { name: 'c.id' })[0]);

    expect(fieldSelect).toHaveProperty('value', JSON.stringify(['node:customers', 'data:customers:id']));
    expect(screen.getAllByText('c.id').length).toBeGreaterThan(0);
  });

  it('supports reorder and delete actions', async () => {
    const user = userEvent.setup();
    render(<QueryFieldSelectorHarness />);

    await user.click(screen.getAllByRole('button', { name: 'applicationConsole.dataCenter.queryModel.addField' })[0]);
    await user.selectOptions(screen.getByRole('combobox', { name: /field 2/ }), JSON.stringify(['node:customers', 'data:customers:id']));
    await user.click(screen.getByRole('button', { name: 'Move field 2 up' }));
    expect(screen.getByRole('combobox', { name: /field 1/ })).toHaveProperty('value', JSON.stringify(['node:customers', 'data:customers:id']));
    await user.click(screen.getByRole('button', { name: /remove field 1/i }));
    expect(screen.getAllByRole('combobox')).toHaveLength(2);
  });
});

function QueryFieldSelectorHarness() {
  const [selections, setSelections] = useState<QueryModelSelection[]>([{ id: 'selection:1', nodeId: 'node:orders', fieldResourceId: 'data:orders:id', alias: '', aggregate: 'none' }]);
  return <QueryFieldSelector fields={fields} onChange={setSelections} selections={selections} t={(key) => key} />;
}
