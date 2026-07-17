import type { DesignerValueType } from './expressionTypes';

export type ExpressionFunctionCategory = 'function' | 'operator';
export type ExpressionArgumentType = DesignerValueType | 'any' | 'sameAsFirst';

export interface ExpressionFunctionDefinition {
  name: string;
  label: string;
  category: ExpressionFunctionCategory;
  description: string;
  returnType: DesignerValueType | 'sameAsFirst';
  minArgs: number;
  maxArgs: number | null;
  argumentTypes: readonly ExpressionArgumentType[];
  argumentLabels: readonly string[];
}

const definitions: readonly ExpressionFunctionDefinition[] = [
  definition('coalesce', 'Coalesce', 'function', 'Returns the first non-empty value.', 'sameAsFirst', 1, null, ['any'], ['Value']),
  definition('concat', 'Concat', 'function', 'Joins values as text.', 'string', 1, null, ['any'], ['Value']),
  definition('length', 'Length', 'function', 'Returns string or array length.', 'number', 1, 1, ['string'], ['Value']),
  definition('lower', 'Lowercase', 'function', 'Converts text to lowercase.', 'string', 1, 1, ['string'], ['Text']),
  definition('upper', 'Uppercase', 'function', 'Converts text to uppercase.', 'string', 1, 1, ['string'], ['Text']),
  definition('trim', 'Trim', 'function', 'Removes surrounding whitespace.', 'string', 1, 1, ['string'], ['Text']),
  definition('contains', 'Contains', 'operator', 'Checks whether text contains another value.', 'boolean', 2, 2, ['string', 'string'], ['Text', 'Search']),
  definition('startsWith', 'Starts with', 'operator', 'Checks a text prefix.', 'boolean', 2, 2, ['string', 'string'], ['Text', 'Prefix']),
  definition('endsWith', 'Ends with', 'operator', 'Checks a text suffix.', 'boolean', 2, 2, ['string', 'string'], ['Text', 'Suffix']),
  definition('add', 'Add', 'operator', 'Adds numbers.', 'number', 2, 2, ['number', 'number'], ['Left', 'Right']),
  definition('subtract', 'Subtract', 'operator', 'Subtracts numbers.', 'number', 2, 2, ['number', 'number'], ['Left', 'Right']),
  definition('multiply', 'Multiply', 'operator', 'Multiplies numbers.', 'number', 2, 2, ['number', 'number'], ['Left', 'Right']),
  definition('divide', 'Divide', 'operator', 'Divides numbers.', 'number', 2, 2, ['number', 'number'], ['Left', 'Right']),
  definition('mod', 'Modulo', 'operator', 'Returns a numeric remainder.', 'number', 2, 2, ['number', 'number'], ['Left', 'Right']),
  definition('abs', 'Absolute', 'function', 'Returns an absolute number.', 'number', 1, 1, ['number'], ['Value']),
  definition('round', 'Round', 'function', 'Rounds a number.', 'number', 1, 1, ['number'], ['Value']),
  definition('toString', 'To text', 'function', 'Converts a value to text.', 'string', 1, 1, ['any'], ['Value']),
  definition('toNumber', 'To number', 'function', 'Converts text or a number to a number.', 'number', 1, 1, ['any'], ['Value']),
  definition('isEmpty', 'Is empty', 'operator', 'Checks null, empty text, or empty collection.', 'boolean', 1, 1, ['any'], ['Value']),
  definition('equals', 'Equals', 'operator', 'Compares two values.', 'boolean', 2, 2, ['sameAsFirst', 'sameAsFirst'], ['Left', 'Right']),
  definition('notEquals', 'Not equals', 'operator', 'Compares two values for inequality.', 'boolean', 2, 2, ['sameAsFirst', 'sameAsFirst'], ['Left', 'Right']),
  definition('greaterThan', 'Greater than', 'operator', 'Compares ordered values.', 'boolean', 2, 2, ['sameAsFirst', 'sameAsFirst'], ['Left', 'Right']),
  definition('greaterThanOrEqual', 'Greater than or equal', 'operator', 'Compares ordered values.', 'boolean', 2, 2, ['sameAsFirst', 'sameAsFirst'], ['Left', 'Right']),
  definition('lessThan', 'Less than', 'operator', 'Compares ordered values.', 'boolean', 2, 2, ['sameAsFirst', 'sameAsFirst'], ['Left', 'Right']),
  definition('lessThanOrEqual', 'Less than or equal', 'operator', 'Compares ordered values.', 'boolean', 2, 2, ['sameAsFirst', 'sameAsFirst'], ['Left', 'Right'])
];

const byName = new Map(definitions.map((item) => [item.name, item]));

export function listExpressionFunctions(category?: ExpressionFunctionCategory): ExpressionFunctionDefinition[] {
  return definitions.filter((item) => !category || item.category === category).map((item) => ({ ...item, argumentTypes: [...item.argumentTypes], argumentLabels: [...item.argumentLabels] }));
}

export function getExpressionFunction(name: string): ExpressionFunctionDefinition | undefined { return byName.get(name); }

export function inferExpressionFunctionType(definitionOrName: ExpressionFunctionDefinition | string, args: readonly { valueType: DesignerValueType }[], fallback: DesignerValueType = 'json'): DesignerValueType {
  const definition = typeof definitionOrName === 'string' ? getExpressionFunction(definitionOrName) : definitionOrName;
  if (!definition) return fallback;
  if (definition.returnType !== 'sameAsFirst') return definition.returnType;
  return args[0]?.valueType ?? fallback;
}

export function defaultExpressionFunctionArgs(definition: ExpressionFunctionDefinition, valueType: DesignerValueType): number {
  return Math.max(definition.minArgs, Math.min(definition.maxArgs ?? definition.minArgs, definition.minArgs || 1)) || (valueType ? 1 : 0);
}

function definition(name: string, label: string, category: ExpressionFunctionCategory, description: string, returnType: ExpressionFunctionDefinition['returnType'], minArgs: number, maxArgs: number | null, argumentTypes: ExpressionArgumentType[], argumentLabels: string[]): ExpressionFunctionDefinition {
  return { argumentLabels, argumentTypes, category, description, label, maxArgs, minArgs, name, returnType };
}
