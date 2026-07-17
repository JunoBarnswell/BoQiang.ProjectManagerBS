import { useMemo, type SetStateAction } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { AdvancedDataGrid } from '../../shared/components/advanced-data-grid/AdvancedDataGrid';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { DictTag } from '../../shared/dict/DictTag';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { useTabPageState } from '../../shared/tabs/useTabPageState';

interface ModuleRow {
  category: string;
  code: string;
  id: number;
  name: string;
  owner: string;
  status: string;
}

interface ModuleFormState {
  category: string;
  code: string;
  name: string;
  owner: string;
  status: string;
}

interface ModuleSearchState {
  keyword: string;
  status: string;
}

interface ModulesPageState {
  appliedSearch: ModuleSearchState;
  draftSearch: ModuleSearchState;
  editingId: number | null;
  formState: ModuleFormState;
  isModalOpen: boolean;
  rows: ModuleRow[];
}

const defaultSearch: ModuleSearchState = {
  keyword: '',
  status: ''
};

const defaultFormState: ModuleFormState = {
  category: '',
  code: '',
  name: '',
  owner: '',
  status: '1'
};

function buildInitialRows(translate: (key: string) => string): ModuleRow[] {
  return [
    { category: translate('nav.infrastructure'), code: 'SYS', id: 1, name: translate('nav.settings'), owner: 'platform', status: '1' },
    { category: translate('nav.modules'), code: 'BAS', id: 2, name: translate('nav.systemDicts'), owner: 'platform', status: '1' },
    { category: translate('home.workbench.title'), code: 'WB', id: 3, name: translate('home.quickActions.user'), owner: 'workbench', status: '1' },
    { category: translate('home.modules.title'), code: 'TMP', id: 4, name: translate('home.quickActions.refresh'), owner: 'platform', status: '0' }
  ];
}

export function ModulesPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const [pageState, setPageState] = useTabPageState<ModulesPageState>(
    {
      appliedSearch: defaultSearch,
      draftSearch: defaultSearch,
      editingId: null,
      formState: defaultFormState,
      isModalOpen: false,
      rows: buildInitialRows(translate)
    },
    { cacheKey: 'modules-page' }
  );
  const { appliedSearch, draftSearch, editingId, formState, isModalOpen, rows } = pageState;
  const setPageField = <K extends keyof ModulesPageState>(key: K, value: SetStateAction<ModulesPageState[K]>) => {
    setPageState((current) => ({
      ...current,
      [key]: typeof value === 'function' ? (value as (previous: ModulesPageState[K]) => ModulesPageState[K])(current[key]) : value
    }));
  };
  const setRows = (value: SetStateAction<ModuleRow[]>) => setPageField('rows', value);
  const setDraftSearch = (value: SetStateAction<ModuleSearchState>) => setPageField('draftSearch', value);
  const setAppliedSearch = (value: SetStateAction<ModuleSearchState>) => setPageField('appliedSearch', value);
  const setIsModalOpen = (value: boolean) => setPageField('isModalOpen', value);
  const setEditingId = (value: number | null) => setPageField('editingId', value);
  const setFormState = (value: SetStateAction<ModuleFormState>) => setPageField('formState', value);

  const searchFields: FormFieldConfig<ModuleSearchState>[] = useMemo(
    () => [
      {
        label: translate('page.modules.search.keyword.label'),
        name: 'keyword',
        placeholder: translate('page.modules.search.keyword.placeholder'),
        type: 'text'
      },
      {
        dictType: 'sys_enabled_status',
        label: translate('page.modules.search.status.label'),
        name: 'status',
        type: 'dict'
      }
    ],
    [translate]
  );

  const formFields: FormFieldConfig<ModuleFormState>[] = useMemo(
    () => [
      {
        label: translate('page.modules.form.code.label'),
        name: 'code',
        placeholder: translate('page.modules.form.code.placeholder'),
        required: true,
        span: 1,
        type: 'text',
        section: translate('page.modules.section.basicInfo')
      },
      {
        label: translate('page.modules.form.name.label'),
        name: 'name',
        placeholder: translate('page.modules.form.name.placeholder'),
        required: true,
        span: 1,
        type: 'text',
        section: translate('page.modules.section.basicInfo')
      },
      {
        label: translate('page.modules.form.category.label'),
        name: 'category',
        placeholder: translate('page.modules.form.category.placeholder'),
        required: true,
        span: 2,
        type: 'text',
        section: translate('page.modules.section.details')
      },
      {
        label: translate('page.modules.form.owner.label'),
        name: 'owner',
        placeholder: translate('page.modules.form.owner.placeholder'),
        span: 1,
        type: 'text',
        section: translate('page.modules.section.details')
      },
      {
        dictType: 'sys_enabled_status',
        label: translate('page.modules.form.status.label'),
        name: 'status',
        required: true,
        span: 1,
        type: 'dict',
        section: translate('page.modules.section.details')
      }
    ],
    [translate]
  );

  const filteredRows = useMemo(() => {
    const keyword = appliedSearch.keyword.trim().toLowerCase();

    return rows.filter((item) => {
      const keywordMatched =
        keyword.length === 0 ||
        [item.code, item.name, item.category, item.owner].some((value) => value.toLowerCase().includes(keyword));
      const statusMatched = appliedSearch.status.length === 0 || item.status === appliedSearch.status;
      return keywordMatched && statusMatched;
    });
  }, [appliedSearch.keyword, appliedSearch.status, rows]);

  const tableColumns: DataTableColumn<ModuleRow>[] = useMemo(
    () => [
      { key: 'code', title: translate('page.modules.table.code'), width: '120px', responsivePriority: 100 },
      { key: 'name', title: translate('page.modules.table.name'), responsivePriority: 95 },
      { key: 'category', title: translate('page.modules.table.category'), width: '160px', responsivePriority: 70, hideBelow: 'lg' },
      {
        key: 'status',
        title: translate('page.modules.table.status'),
        width: '120px',
        responsivePriority: 85,
        render: (row) => <DictTag dictType="sys_enabled_status" value={row.status} />
      },
      { key: 'owner', title: translate('page.modules.table.owner'), width: '140px', responsivePriority: 40, hideBelow: 'xl' }
    ],
    [translate]
  );

  const openCreateModal = () => {
    setEditingId(null);
    setFormState(defaultFormState);
    setIsModalOpen(true);
  };

  const openEditModal = (row: ModuleRow) => {
    setEditingId(row.id);
    setFormState({
      category: row.category,
      code: row.code,
      name: row.name,
      owner: row.owner,
      status: row.status
    });
    setIsModalOpen(true);
  };

  const handleDelete = (index: number) => {
    const targetRow = filteredRows[index];
    if (!targetRow) {
      return;
    }

    confirm({
      title: translate('page.modules.confirm.delete'),
      content: translate('page.modules.confirm.delete').replace('{name}', targetRow.name),
      onConfirm: async () => {
        setRows((currentRows) => currentRows.filter((item) => item.id !== targetRow.id));
      }
    });
  };

  const handleSave = () => {
    if (!formState.code.trim() || !formState.name.trim() || !formState.category.trim()) {
      message.error(translate('page.modules.alert.required'));
      return;
    }

    if (editingId === null) {
      setRows((currentRows) => [
        ...currentRows,
        {
          ...formState,
          id: currentRows.length > 0 ? Math.max(...currentRows.map((item) => item.id)) + 1 : 1
        }
      ]);
    } else {
      setRows((currentRows) => currentRows.map((item) => (item.id === editingId ? { ...item, ...formState } : item)));
    }

    setIsModalOpen(false);
  };

  return (
    <CrudPage
      description={translate('page.modules.toolbar.description')}
      eyebrow={translate('nav.modules')}
      title={translate('page.modules.title')}
    >
      <AdvancedDataGrid<ModuleRow, ModuleSearchState>
        description={translate('page.modules.toolbar.description')}
        eyebrow={translate('nav.modules')}
        search={{
          columns: { xs: 1, md: 2, xl: 2 },
          defaultCollapsed: true,
          fields: searchFields,
          maxRows: 1,
          onReset: () => {
            setDraftSearch(defaultSearch);
            setAppliedSearch(defaultSearch);
          },
          onSubmit: (nextValue) => setAppliedSearch(nextValue),
          onValueChange: setDraftSearch,
          value: draftSearch
        }}
        table={{
          columns: tableColumns,
          fitScreen: true,
          pagination: {
            current: 1,
            pageSize: filteredRows.length || 1,
            total: filteredRows.length
          },
          reservedHeight: 0,
          rowActions: (row, index) => (
            <TableActions>
              <button className="ghost-button" type="button" onClick={() => openEditModal(row)}>
                {translate('common.edit')}
              </button>
              <button className="ghost-button" type="button" onClick={() => handleDelete(index)}>
                {translate('common.delete')}
              </button>
            </TableActions>
          ),
          rowKey: (row) => String(row.id),
          rows: filteredRows
        }}
        title={translate('page.modules.title')}
        toolbar={{
          actions: [
            {
              code: 'system:module:add',
              onClick: openCreateModal,
              priority: 100,
              text: translate('page.modules.action.create'),
              type: 'button',
              variant: 'primary'
            },
            {
              onClick: () => message.info(translate('page.modules.toolbar.description')),
              priority: 60,
              text: translate('page.modules.action.import'),
              type: 'button'
            },
            {
              onClick: () => message.info(translate('page.modules.toolbar.description')),
              priority: 40,
              text: translate('page.modules.action.export'),
              type: 'button'
            }
          ],
          title: translate('page.modules.toolbar.title')
        }}
      />

      <ModalForm
        actions={[
          {
            label: translate('common.cancel'),
            onClick: () => setIsModalOpen(false),
            variant: 'ghost'
          },
          {
            label: translate('common.save'),
            onClick: handleSave,
            type: 'button',
            variant: 'primary'
          }
        ]}
        fields={formFields}
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onValueChange={(name, nextValue) =>
          setFormState((currentValue) => ({
            ...currentValue,
            [name]: nextValue as string
          }))
        }
        title={editingId === null ? translate('page.modules.modal.createTitle') : translate('page.modules.modal.editTitle')}
        value={formState}
      >
        {translate('page.modules.toolbar.description')}
      </ModalForm>
    </CrudPage>
  );
}
