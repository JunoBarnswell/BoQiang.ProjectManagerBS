import { runLimitedParallel } from '../../utils/runLimitedParallel';

export interface BatchDeleteFailure {
  id: string;
  reason: string;
}

export interface BatchDeleteResult {
  failedItems: BatchDeleteFailure[];
  successIds: string[];
}

export async function runLimitedBatchDelete(
  ids: string[],
  deleteItem: (id: string) => Promise<unknown>,
  concurrency = 3
): Promise<BatchDeleteResult> {
  const result = await runLimitedParallel(ids, deleteItem, concurrency);
  return {
    failedItems: result.failedItems.map((item) => ({ id: item.input, reason: item.reason })),
    successIds: result.successInputs
  };
}
