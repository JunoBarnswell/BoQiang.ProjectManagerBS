import { useEffect, useMemo, useState } from 'react';

import type {
  ApplicationDevelopmentModuleTreeNode,
  ApplicationDevelopmentPageListItem
} from '../../../api/application-development-center/applicationDevelopmentCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { FormRenderer } from '../../../shared/forms/FormRenderer';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';

import { flattenModuleTree } from './applicationDevelopmentModuleTreeUtils';

export interface PageCardEditorValues {
  moduleId: string;
  pageCode: string;
  pageName: string;
  parentPageId: string;
  sortOrder: number;
}

interface PageCardEditorDialogProps {
  modules: ApplicationDevelopmentModuleTreeNode[];
  open: boolean;
  page: ApplicationDevelopmentPageListItem | null;
  pages: ApplicationDevelopmentPageListItem[];
  saving?: boolean;
  onClose: () => void;
  onSubmit: (values: PageCardEditorValues) => void;
}

export function PageCardEditorDialog({ modules, onClose, onSubmit, open, page, pages, saving }: PageCardEditorDialogProps) {
  const [values, setValues] = useState<PageCardEditorValues>(() => toValues(page));
  const moduleOptions = useMemo(
    () => [
      { label: translateCurrentLiteral("不归属菜单"), value: '' },
      ...flattenModuleTree(modules).map((module) => ({ label: `${module.moduleName} / ${module.moduleCode}`, value: module.id }))
    ],
    [modules]
  );
  const parentPageOptions = useMemo(
    () => [
      { label: translateCurrentLiteral("无父页面"), value: '' },
      ...pages
        .filter((item) => item.id !== page?.id && item.pageType === 'standard')
        .map((item) => ({ label: `${item.pageName} / ${item.pageCode}`, value: item.id }))
    ],
    [page?.id, pages]
  );
  const fields = useMemo<FormFieldConfig<PageCardEditorValues>[]>(
    () => [
      { label: translateCurrentLiteral("页面名称"), name: 'pageName', required: true, section: '页面信息', type: 'text' },
      { label: translateCurrentLiteral("页面编码"), name: 'pageCode', required: true, section: '页面信息', type: 'text' },
      { label: translateCurrentLiteral("所属菜单"), name: 'moduleId', options: moduleOptions, section: '页面信息', type: 'select' },
      { label: translateCurrentLiteral("父页面"), name: 'parentPageId', options: parentPageOptions, section: '页面信息', type: 'select' },
      { label: translateCurrentLiteral("排序"), name: 'sortOrder', section: '页面信息', type: 'number' }
    ],
    [moduleOptions, parentPageOptions]
  );

  useEffect(() => {
    if (open) {
      setValues(toValues(page));
    }
  }, [open, page]);

  return (
    <ResponsiveModal
      footer={
        <>
          <button className="secondary-button h-8 text-xs" type="button" onClick={onClose}>{translateCurrentLiteral("取消")}</button>
          <button className="primary-button h-8 text-xs" disabled={saving || !page} type="button" onClick={() => onSubmit(values)}>
            {saving ? '保存中...' : '保存页面'}
          </button>
        </>
      }
      open={open}
      title={translateCurrentLiteral("编辑页面信息")}
      onClose={onClose}
    >
      <FormRenderer
        fields={fields}
        value={values}
        onValueChange={(name, value) => setValues((current) => ({ ...current, [name]: value }))}
      />
    </ResponsiveModal>
  );
}

function toValues(page: ApplicationDevelopmentPageListItem | null): PageCardEditorValues {
  return {
    moduleId: page?.moduleId ?? '',
    pageCode: page?.pageCode ?? '',
    pageName: page?.pageName ?? '',
    parentPageId: page?.parentPageId ?? '',
    sortOrder: page?.sortOrder ?? 0
  };
}
