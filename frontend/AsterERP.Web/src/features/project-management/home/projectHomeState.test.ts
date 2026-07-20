import { describe, expect, it } from 'vitest';

import { densityRowHeight, parseProjectHomeUrlState, projectHomeQuery } from './projectHomeState';

describe('projectHomeState', () => {
  it('normalizes legacy and unified URL parameters', () => {
    const state = parseProjectHomeUrlState(new URLSearchParams('collection=recent&search=alpha&sort=name&order=asc&density=compact&insightsTab=leads&columns=name,status'));
    expect(state.collection).toBe('recent');
    expect(state.keyword).toBe('alpha');
    expect(state.sortBy).toBe('name');
    expect(state.sortDirection).toBe('asc');
    expect(state.columns).toEqual(['name', 'status']);
    expect(projectHomeQuery(state).keyword).toBe('alpha');
  });

  it('uses the design density heights', () => {
    expect(densityRowHeight('compact')).toBe(42);
    expect(densityRowHeight('default')).toBe(48);
    expect(densityRowHeight('comfortable')).toBe(56);
  });
});
