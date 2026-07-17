import { describe, expect, it } from 'vitest';

import { createConversionPipeline } from './conversionPipeline';
import { scoreTypeCompatibility } from './typeCompatibility';

describe('phase 2 binding compatibility', () => {
  it('scores exact and safe conversions', () => {
    expect(scoreTypeCompatibility('string', 'string').compatibility).toBe('exact');
    expect(scoreTypeCompatibility('number', 'string').compatibility).toBe('safe');
    expect(createConversionPipeline('number', 'string').steps[0]?.name).toBe('numberToString');
  });

  it('blocks incompatible conversions', () => {
    expect(scoreTypeCompatibility('date', 'array').compatibility).toBe('incompatible');
    expect(createConversionPipeline('date', 'array').valid).toBe(false);
  });

  it('allows only conversion paths that the pipeline can execute', () => {
    expect(createConversionPipeline('string', 'number').valid).toBe(true);
    expect(createConversionPipeline('object', 'boolean').valid).toBe(false);
  });

  it('keeps unsupported json string conversion blocked in both models', () => {
    const score = scoreTypeCompatibility('json', 'string');
    const pipeline = createConversionPipeline('json', 'string');
    expect(score.compatibility).toBe('incompatible');
    expect(pipeline.valid).toBe(false);
  });
});
