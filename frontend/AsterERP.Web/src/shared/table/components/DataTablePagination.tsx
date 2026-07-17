import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../icons/AppIcon';

interface DataTablePaginationProps {
  currentPage: number;
  onPageChange: (nextPage: number) => void;
  onPageSizeChange?: (nextPageSize: number) => void;
  pageSize: number;
  pageSizeOptions: number[];
  total: number;
  totalPages: number;
}

export function DataTablePagination({
  currentPage,
  onPageChange,
  onPageSizeChange,
  pageSize,
  pageSizeOptions,
  total,
  totalPages
}: DataTablePaginationProps) {
  const { translate } = useI18n();
  return (
    <div className="data-table-pagination flex items-center justify-between py-3 px-3 text-sm text-gray-600">
      <span>{formatMessage(translate('table.totalData'), { total })}</span>
      <div className="flex items-center gap-1">
        <button
          className="p-1 min-w-[28px] h-[28px] rounded hover:bg-gray-100 disabled:opacity-30 disabled:cursor-not-allowed flex items-center justify-center border border-transparent transition-colors"
          disabled={currentPage <= 1}
          type="button"
          onClick={() => onPageChange(Math.max(1, currentPage - 1))}
        >
          <AppIcon name="caret-left" />
        </button>
        <button className="p-1 min-w-[28px] h-[28px] rounded bg-primary-50 text-primary-600 font-medium flex items-center justify-center border border-primary-100" type="button">
          {Math.min(currentPage, totalPages)}
        </button>
        <button
          className="p-1 min-w-[28px] h-[28px] rounded hover:bg-gray-100 disabled:opacity-30 disabled:cursor-not-allowed flex items-center justify-center border border-transparent transition-colors"
          disabled={currentPage >= totalPages}
          type="button"
          onClick={() => onPageChange(Math.min(totalPages, currentPage + 1))}
        >
          <AppIcon name="caret-right" />
        </button>
        {onPageSizeChange ? (
          <label className="inline-flex items-center gap-[6px] ml-4">
            <select
              className="border border-gray-300 rounded px-1.5 py-1 focus:outline-none focus:border-primary-500 bg-white cursor-pointer"
              value={pageSize}
              onChange={(event) => onPageSizeChange(Number(event.target.value))}
            >
              {pageSizeOptions.map((option) => (
                <option key={option} value={option}>
                  {formatMessage(translate('table.pageSize'), { size: option })}
                </option>
              ))}
            </select>
          </label>
        ) : null}
      </div>
    </div>
  );
}
