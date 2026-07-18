import { useMutation, type UseMutationOptions } from '@tanstack/react-query';

interface ApiMutationOptions<TData, TError, TVariables> {
  mutationFn: (variables: TVariables) => Promise<TData>;
  onError?: UseMutationOptions<TData, TError, TVariables>['onError'];
  onSuccess?: UseMutationOptions<TData, TError, TVariables>['onSuccess'];
  onSettled?: UseMutationOptions<TData, TError, TVariables>['onSettled'];
}

export function useApiMutation<TData, TError = Error, TVariables = void>(
  options: ApiMutationOptions<TData, TError, TVariables>
) {
  return useMutation<TData, TError, TVariables>({
    mutationFn: options.mutationFn,
    onError: options.onError,
    onSuccess: options.onSuccess,
    onSettled: options.onSettled
  });
}
