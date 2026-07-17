export interface LimitedParallelFailure<TInput> {
  input: TInput;
  reason: string;
}

export interface LimitedParallelResult<TInput, TResult> {
  failedItems: LimitedParallelFailure<TInput>[];
  results: TResult[];
  successInputs: TInput[];
}

function getFailureReason(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return '未知错误';
}

export async function runLimitedParallel<TInput, TResult>(
  inputs: TInput[],
  worker: (input: TInput) => Promise<TResult>,
  concurrency = 3
): Promise<LimitedParallelResult<TInput, TResult>> {
  const boundedConcurrency = Math.max(1, concurrency);
  const results: TResult[] = [];
  const successInputs: TInput[] = [];
  const failedItems: LimitedParallelFailure<TInput>[] = [];

  for (let index = 0; index < inputs.length; index += boundedConcurrency) {
    const chunk = inputs.slice(index, index + boundedConcurrency);
    const settledResults = await Promise.allSettled(chunk.map((input) => worker(input)));

    settledResults.forEach((settledResult, resultIndex) => {
      const input = chunk[resultIndex];
      if (input === undefined) {
        return;
      }

      if (settledResult.status === 'fulfilled') {
        successInputs.push(input);
        results.push(settledResult.value);
        return;
      }

      failedItems.push({ input, reason: getFailureReason(settledResult.reason) });
    });
  }

  return { failedItems, results, successInputs };
}
