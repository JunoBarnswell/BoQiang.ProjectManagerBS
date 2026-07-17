import { AdaptiveSearchForm } from '../../responsive/AdaptiveSearchForm';
import { ResponsivePage } from '../../responsive/ResponsivePage';
import { ResponsiveToolbar } from '../../responsive/ResponsiveToolbar';
import { AutoHeightTable } from '../../table/AutoHeightTable';

import type { AdvancedDataGridProps } from './types';

export function AdvancedDataGrid<TItem, TSearchValues extends object>({
  actions,
  children,
  className,
  description,
  editor,
  eyebrow,
  fitScreen = true,
  footer,
  search,
  sidePanel,
  table,
  title,
  toolbar
}: AdvancedDataGridProps<TItem, TSearchValues>) {
  const searchNode = search ? (
    <AdaptiveSearchForm
      columns={search.columns}
      defaultCollapsed={search.defaultCollapsed}
      fields={search.fields}
      loading={search.loading}
      maxRows={search.maxRows}
      onReset={search.onReset}
      onSubmit={search.onSubmit}
      onValueChange={search.onValueChange}
      value={search.value}
    />
  ) : undefined;

  const toolbarNode = toolbar ? <ResponsiveToolbar actions={toolbar.actions} title={toolbar.title} /> : undefined;

  return (
    <ResponsivePage
      actions={actions}
      className={className}
      description={description}
      footer={footer}
      eyebrow={eyebrow}
      fitScreen={fitScreen}
      searchArea={searchNode}
      title={title}
      toolbar={toolbarNode}
    >
      {sidePanel ? (
        <div className="flex flex-col xl:flex-row flex-1 min-h-0 gap-[var(--erp-section-gap)] xl:items-stretch overflow-auto xl:overflow-visible">
          <div className="flex flex-none xl:flex-initial flex-col w-full xl:w-[clamp(280px,20vw,360px)] min-h-0">{sidePanel}</div>
          <div className="flex flex-1 flex-col min-w-0 min-h-0">
            <AutoHeightTable {...table} />
          </div>
        </div>
      ) : (
        <AutoHeightTable {...table} />
      )}
      {children}
      {editor}
    </ResponsivePage>
  );
}
