import type { ReactNode } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { NetworkError } from '../../status/NetworkError';
import { PageEmpty } from '../../status/PageEmpty';
import { PageLoading } from '../../status/PageLoading';
import { getErrorMessage } from '../../utils/errorMessage';

interface CrudStateViewProps {
  children: ReactNode;
  emptyText: ReactNode;
  error?: unknown;
  isEmpty?: boolean;
  isError?: boolean;
  isLoading?: boolean;
  onRetry?: () => void;
}

export function CrudStateView({
  children,
  emptyText,
  error,
  isEmpty = false,
  isError = false,
  isLoading = false,
  onRetry
}: CrudStateViewProps) {
  const { translate } = useI18n();

  if (isLoading) {
    return <PageLoading />;
  }

  if (isError) {
    return (
      <NetworkError
        action={
          onRetry ? (
            <button className="primary-button" type="button" onClick={() => void onRetry()}>
              {translate('common.retry')}
            </button>
          ) : undefined
        }
        description={getErrorMessage(error, translate('common.retry'))}
      />
    );
  }

  if (isEmpty) {
    return <PageEmpty description={emptyText} />;
  }

  return <>{children}</>;
}
