import type { DepartmentListItemDto, PositionListItemDto, UserEmploymentDto } from '../../../api/system/system.types';
import { AppIcon } from '../../../shared/icons/AppIcon';

interface UserEmploymentEditorProps {
  departments: DepartmentListItemDto[];
  onChange: (value: UserEmploymentDto[]) => void;
  positions: PositionListItemDto[];
  translate: (key: string) => string;
  value: UserEmploymentDto[];
}

const EmptyEmployment: UserEmploymentDto = {
  deptId: '',
  deptName: null,
  employmentName: null,
  id: null,
  isPrimary: false,
  positionId: '',
  positionName: null,
  sortOrder: 1,
  status: 'Enabled'
};

export function UserEmploymentEditor({
  departments,
  onChange,
  positions,
  translate,
  value
}: UserEmploymentEditorProps) {
  const rows = value.length > 0 ? value : [{ ...EmptyEmployment, isPrimary: true }];
  const duplicates = findDuplicateKeys(rows);

  const updateRow = (index: number, patch: Partial<UserEmploymentDto>) => {
    const next = rows.map((row, rowIndex) => {
      if (rowIndex !== index) {
        return row;
      }

      const merged = { ...row, ...patch };
      if ('deptId' in patch) {
        const department = departments.find((item) => item.id === patch.deptId);
        merged.deptName = department?.deptName ?? null;
        merged.positionId = '';
        merged.positionName = null;
      }

      if ('positionId' in patch) {
        const position = positions.find((item) => item.id === patch.positionId);
        merged.positionName = position?.positionName ?? null;
      }

      return merged;
    });
    onChange(ensurePrimary(next));
  };

  const addRow = () => {
    onChange([...rows, { ...EmptyEmployment, sortOrder: rows.length + 1, isPrimary: rows.every((item) => item.status !== 'Enabled') }]);
  };

  const removeRow = (index: number) => {
    const next = rows.filter((_row, rowIndex) => rowIndex !== index);
    onChange(ensurePrimary(next.length > 0 ? next : [{ ...EmptyEmployment, isPrimary: true }]));
  };

  const setPrimary = (index: number) => {
    onChange(rows.map((row, rowIndex) => ({ ...row, isPrimary: rowIndex === index, status: rowIndex === index ? 'Enabled' : row.status })));
  };

  return (
    <div className="mb-4 rounded border border-gray-200 bg-gray-50/60 p-3 text-gray-700">
      <div className="mb-2 flex items-center justify-between gap-2">
        <div className="font-medium text-gray-800">{translate('page.systemUsers.section.employments')}</div>
        <button className="ghost-button h-8 px-2 text-xs" type="button" onClick={addRow}>
          <AppIcon name="plus" /> {translate('common.add')}
        </button>
      </div>
      <div className="space-y-2">
        {rows.map((row, index) => {
          const positionOptions = positions.filter((position) => !row.deptId || position.deptId === row.deptId);
          const duplicate = row.deptId && row.positionId && duplicates.has(buildEmploymentKey(row));
          return (
            <div key={row.id || index} className="rounded border border-gray-200 bg-white p-2">
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <label className="flex flex-col gap-1">
                  <span className="text-xs text-gray-500">{translate('page.systemUsers.field.deptId')}</span>
                  <select className="h-9 rounded border border-gray-300 px-2 text-sm" value={row.deptId} onChange={(event) => updateRow(index, { deptId: event.target.value })}>
                    <option value="">{translate('common.selectOne')}</option>
                    {departments.map((department) => (
                      <option key={department.id} value={department.id}>{department.deptName} ({department.deptCode})</option>
                    ))}
                  </select>
                </label>
                <label className="flex flex-col gap-1">
                  <span className="text-xs text-gray-500">{translate('page.systemUsers.field.positionId')}</span>
                  <select className="h-9 rounded border border-gray-300 px-2 text-sm" value={row.positionId} onChange={(event) => updateRow(index, { positionId: event.target.value })}>
                    <option value="">{translate('common.selectOne')}</option>
                    {positionOptions.map((position) => (
                      <option key={position.id} value={position.id}>{position.positionName} ({position.positionCode})</option>
                    ))}
                  </select>
                </label>
                <label className="flex flex-col gap-1">
                  <span className="text-xs text-gray-500">{translate('page.systemUsers.field.employmentName')}</span>
                  <input className="h-9 rounded border border-gray-300 px-2 text-sm" value={row.employmentName ?? ''} onChange={(event) => updateRow(index, { employmentName: event.target.value })} />
                </label>
                <label className="flex flex-col gap-1">
                  <span className="text-xs text-gray-500">{translate('page.systemUsers.field.status')}</span>
                  <select className="h-9 rounded border border-gray-300 px-2 text-sm" value={row.status} onChange={(event) => updateRow(index, { status: event.target.value })}>
                    <option value="Enabled">{translate('common.enabled')}</option>
                    <option value="Disabled">{translate('common.disabled')}</option>
                  </select>
                </label>
              </div>
              <div className="mt-2 flex items-center justify-between gap-2">
                <label className="inline-flex items-center gap-2 text-xs text-gray-600">
                  <input checked={row.isPrimary} type="radio" onChange={() => setPrimary(index)} />
                  {translate('page.systemUsers.field.primaryEmployment')}
                </label>
                <button className="text-xs text-red-600 hover:text-red-700" type="button" onClick={() => removeRow(index)}>
                  <AppIcon name="trash" /> {translate('common.delete')}
                </button>
              </div>
              {duplicate ? <div className="mt-1 text-xs text-red-600">{translate('page.systemUsers.error.duplicateEmployment')}</div> : null}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ensurePrimary(rows: UserEmploymentDto[]): UserEmploymentDto[] {
  const enabledRows = rows.filter((item) => item.status === 'Enabled');
  if (enabledRows.length === 0 || rows.some((item) => item.isPrimary && item.status === 'Enabled')) {
    return rows;
  }

  const firstEnabled = rows.findIndex((item) => item.status === 'Enabled');
  return rows.map((row, index) => ({ ...row, isPrimary: index === firstEnabled }));
}

function findDuplicateKeys(rows: UserEmploymentDto[]): Set<string> {
  const counts = new Map<string, number>();
  rows
    .filter((row) => row.status === 'Enabled' && row.deptId && row.positionId)
    .forEach((row) => {
      const key = buildEmploymentKey(row);
      counts.set(key, (counts.get(key) ?? 0) + 1);
    });

  return new Set([...counts.entries()].filter((entry) => entry[1] > 1).map((entry) => entry[0]));
}

function buildEmploymentKey(row: UserEmploymentDto): string {
  return `${row.deptId}:${row.positionId}`;
}
