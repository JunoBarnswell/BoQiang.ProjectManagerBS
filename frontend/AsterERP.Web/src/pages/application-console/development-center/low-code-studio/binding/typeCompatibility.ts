import type { DesignerValueType } from '../expression/expressionTypes';

import type { BindingCompatibility } from './bindingTypes';
import { createConversionPipeline } from './conversionPipeline';


export interface TypeCompatibilityResult {
  compatibility: BindingCompatibility;
  score: number;
  reason: string;
}

export function scoreTypeCompatibility(from: DesignerValueType, to: DesignerValueType): TypeCompatibilityResult {
  const pipeline = createConversionPipeline(from, to);
  return { compatibility: pipeline.compatibility, reason: pipeline.reason, score: pipeline.compatibility === 'exact' ? 100 : pipeline.compatibility === 'safe' ? 80 : pipeline.compatibility === 'lossy' ? 50 : 0 };
}
