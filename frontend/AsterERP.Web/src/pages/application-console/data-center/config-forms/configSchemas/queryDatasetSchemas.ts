import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { ConfigFormSchema } from '../configFormTypes';

const sourceFields = [
  { component: 'objectSelect', helpText: '选择数据源或微流；从数据源工作台进入时会默认指向当前数据库。', label: translateCurrentLiteral("来源对象"), name: 'sourceObjectId', objectResourcePaths: ['data-sources', 'microflows'], required: true, span: 2 },
  { component: 'textarea', label: 'SQL', name: 'sql', span: 2 },
  { component: 'tableSelect', label: translateCurrentLiteral("来源表"), name: 'tableName', tableDataSourceField: 'sourceObjectId' },
  { component: 'fieldList', label: translateCurrentLiteral("输出字段"), name: 'fields', span: 2 },
  { component: 'mappingList', label: translateCurrentLiteral("字段映射"), name: 'mappings', span: 2 }
] as ConfigFormSchema['sections'][number]['fields'];

function schema(objectType: string, title: string, extraFields = [] as ConfigFormSchema['sections'][number]['fields']): ConfigFormSchema {
  return {
    description: `${title}配置。`,
    moduleKey: 'query-dataset',
    objectType,
    sections: [
      { fields: [...sourceFields, ...extraFields], key: 'basic', title: '数据来源' },
      {
        collapsible: true,
        defaultCollapsed: true,
        fields: [
          { component: 'keyValueList', label: translateCurrentLiteral("查询条件"), name: 'filters', span: 2 },
          { component: 'keyValueList', label: translateCurrentLiteral("排序规则"), name: 'sorts', span: 2 },
          { component: 'number', defaultValue: 20, label: translateCurrentLiteral("默认分页大小"), name: 'pageSize' },
          { component: 'switch', defaultValue: true, label: translateCurrentLiteral("允许导出"), name: 'exportable' }
        ],
        key: 'advanced',
        title: '查询与导出'
      }
    ],
    title
  };
}

export const queryDatasetSchemas: ConfigFormSchema[] = [
  schema('QueryView', '查询视图'),
  schema('ReportDataset', '报表数据集', [
    { component: 'fieldList', label: translateCurrentLiteral("维度"), name: 'dimensions', span: 2 },
    { component: 'fieldList', label: translateCurrentLiteral("指标"), name: 'measures', span: 2 }
  ]),
  schema('ChartDataset', '图表数据集', [
    { component: 'select', defaultValue: 'bar', label: translateCurrentLiteral("图表类型"), name: 'chartType', options: [{ label: translateCurrentLiteral("柱状图"), value: 'bar' }, { label: translateCurrentLiteral("折线图"), value: 'line' }, { label: translateCurrentLiteral("饼图"), value: 'pie' }] }
  ]),
  schema('ExportDataset', '导出模板数据集', [
    { component: 'select', defaultValue: 'xlsx', label: translateCurrentLiteral("导出格式"), name: 'exportFormat', options: [{ label: 'Excel', value: 'xlsx' }, { label: 'CSV', value: 'csv' }] }
  ]),
  schema('SelectorDataset', '页面选择器数据源', [
    { component: 'text', label: translateCurrentLiteral("值字段"), name: 'valueField' },
    { component: 'text', label: translateCurrentLiteral("显示字段"), name: 'labelField' }
  ])
];
