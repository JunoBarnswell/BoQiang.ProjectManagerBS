// @vitest-environment jsdom

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { ApplicationMappingCacheColumn, ApplicationMappingCacheParameter } from '../../../../api/application-data-center/applicationDataCenter.types';

import { MappingCacheParameterDialog } from './MappingCacheParameterDialog';

const column: ApplicationMappingCacheColumn = { sourceResourceId: 'column-1', targetName: 'amount', dataType: 'decimal', nullable: false, ordinal: 1 };

describe('MappingCacheParameterDialog workflow', () => {
  it('adds a parameter with a selected column and canonical type', () => {
    const onSave = vi.fn();
    render(<MappingCacheParameterDialog open mode="configure" columns={[column]} existingParameters={[]} onClose={vi.fn()} onSave={onSave} />);

    fireEvent.change(screen.getByLabelText('Parameter name'), { target: { value: 'minimumAmount' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save parameter' }));

    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ name: 'minimumAmount', columnResourceId: 'column-1', dataType: 'number', required: true }));
  });

  it('requires required execution values and sends typed values', () => {
    const onRun = vi.fn();
    const parameter: ApplicationMappingCacheParameter = { resourceId: 'parameter-1', name: 'minimumAmount', columnResourceId: 'column-1', dataType: 'number', required: true };
    render(<MappingCacheParameterDialog open mode="execute" columns={[]} parameters={[parameter]} onClose={vi.fn()} onRun={onRun} />);

    fireEvent.click(screen.getByRole('button', { name: 'Run test' }));
    expect(screen.getByRole('alert').textContent).toContain('Required parameter is missing');
    fireEvent.change(screen.getByLabelText(/minimumAmount/), { target: { value: '12.50' } });
    fireEvent.click(screen.getByRole('button', { name: 'Run test' }));

    expect(onRun).toHaveBeenCalledWith({ 'parameter-1': 12.5 });
  });
});
