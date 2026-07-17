// @vitest-environment jsdom

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { DataSourceWizardStepper } from './DataSourceWizardStepper';

describe('DataSourceWizardStepper DOM behavior', () => {
  it('locks confirm until diagnosis succeeds and emits selected step', () => {
    const onChange = vi.fn();
    render(<DataSourceWizardStepper activeStep="diagnosis" diagnosisReady={false} onChange={onChange} />);

    expect(screen.getByRole('button', { name: /4\. 确认保存/ })).toHaveProperty('disabled', true);
    fireEvent.click(screen.getByRole('button', { name: /2\. 连接/ }));

    expect(onChange).toHaveBeenCalledWith('connection');
  });
});
