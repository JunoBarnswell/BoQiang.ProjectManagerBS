import { useState } from 'react';

import { systemFilesApi } from '../../../api/system/files.api';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useMessage } from '../../../shared/feedback/useMessage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { toAssetToken } from '../utils/assetTokenResolver';

interface PrintAssetPickerProps {
  onSelectToken?: (token: string) => void;
}

export function PrintAssetPicker({ onSelectToken }: PrintAssetPickerProps) {
  const { translate } = useI18n();
  const message = useMessage();
  const [keyword, setKeyword] = useState('');

  const filesQuery = useApiQuery({
    queryFn: ({ signal }) =>
      systemFilesApi.list({
        keyword,
        pageIndex: 1,
        pageSize: 12
      }, signal),
    queryKey: ['print-center', 'asset-picker', keyword]
  });

  const handleUseToken = async (fileId: string) => {
    const token = toAssetToken(fileId);
    onSelectToken?.(token);
    try {
      await navigator.clipboard.writeText(token);
      message.success(formatMessage(translate('print.assetPicker.copySuccess'), { token }));
    } catch {
      message.success(formatMessage(translate('print.assetPicker.copyFallback'), { token }));
    }
  };

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-gray-900">{translate('print.assetPicker.title')}</h3>
          <p className="mt-1 text-xs text-gray-500">{translate('print.assetPicker.description')}</p>
        </div>
      </div>

      <div className="mb-3 flex items-center gap-2">
        <input
          className="w-full rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-500"
          placeholder={translate('print.assetPicker.searchPlaceholder')}
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
        />
        <button
          className="rounded border border-gray-300 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50"
          type="button"
          title={translate('print.assetPicker.refresh')}
          onClick={() => void filesQuery.refetch()}
        >
          <AppIcon name="arrows-clockwise" />
        </button>
      </div>

      <div className="space-y-2">
        {(filesQuery.data?.data.items ?? []).map((file) => (
          <div key={file.id} className="flex items-center justify-between gap-3 rounded border border-gray-200 px-3 py-2">
            <div className="min-w-0">
              <div className="truncate text-sm font-medium text-gray-900">{file.fileName}</div>
              <div className="truncate text-xs text-gray-500">{file.id}</div>
            </div>
            <button
              className="shrink-0 rounded bg-primary-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-primary-700"
              type="button"
              onClick={() => void handleUseToken(file.id)}
            >
              {translate('print.assetPicker.copyToken')}
            </button>
          </div>
        ))}

        {filesQuery.isLoading ? <div className="py-6 text-center text-sm text-gray-500">{translate('print.assetPicker.loading')}</div> : null}
        {!filesQuery.isLoading && (filesQuery.data?.data.items?.length ?? 0) === 0 ? (
          <div className="py-6 text-center text-sm text-gray-500">{translate('print.assetPicker.empty')}</div>
        ) : null}
      </div>
    </div>
  );
}
