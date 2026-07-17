import { formatHttpErrorMessage } from './formatHttpError';

export function getErrorMessage(error: unknown, fallback: string): string {
  return formatHttpErrorMessage(error, fallback);
}
