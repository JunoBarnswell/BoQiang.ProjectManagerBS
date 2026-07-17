import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { ConfigFormSchema } from '../configFormTypes';

const commonFields = [
  { component: 'modelSelect', label: translateCurrentLiteral("模型"), name: 'modelId', span: 2 },
  { component: 'tableSelect', label: translateCurrentLiteral("来源表"), name: 'sourceTable' },
  { component: 'text', defaultValue: 'id', label: translateCurrentLiteral("主键字段"), name: 'keyField' },
  { component: 'text', label: translateCurrentLiteral("字段编码"), name: 'fieldCode', required: true },
  { component: 'text', label: translateCurrentLiteral("字段名称"), name: 'fieldName', required: true },
  { component: 'switch', defaultValue: true, label: translateCurrentLiteral("允许查询"), name: 'queryable' },
  { component: 'switch', defaultValue: true, label: translateCurrentLiteral("允许排序"), name: 'sortable' },
  { component: 'switch', defaultValue: true, label: translateCurrentLiteral("允许写入"), name: 'writable' }
] as ConfigFormSchema['sections'][number]['fields'];

function schema(objectType: string, title: string, extraFields = [] as ConfigFormSchema['sections'][number]['fields']): ConfigFormSchema {
  return {
    description: `${title}配置。`,
    moduleKey: 'entity-field',
    objectType,
    sections: [{ fields: [...commonFields, ...extraFields], key: 'basic', title: '字段配置' }],
    title
  };
}

export const entityFieldSchemas: ConfigFormSchema[] = [
  schema('Text', '文本字段', [{ component: 'number', label: translateCurrentLiteral("最大长度"), name: 'maxLength' }, { component: 'textarea', label: '默认值 JSON', name: 'defaultValueJson', span: 2 }]),
  schema('Number', '数字字段', [{ component: 'number', label: translateCurrentLiteral("最小值"), name: 'min' }, { component: 'number', label: translateCurrentLiteral("最大值"), name: 'max' }, { component: 'number', label: translateCurrentLiteral("小数位"), name: 'precision' }]),
  schema('Date', '日期字段', [{ component: 'select', defaultValue: 'yyyy-MM-dd', label: translateCurrentLiteral("显示格式"), name: 'format', options: [{ label: translateCurrentLiteral("日期"), value: 'yyyy-MM-dd' }, { label: translateCurrentLiteral("日期时间"), value: 'yyyy-MM-dd HH:mm:ss' }] }]),
  schema('Dictionary', '字典字段', [{ component: 'objectSelect', label: translateCurrentLiteral("字典类型"), name: 'dictType', objectResourcePath: 'dictionaries-codes', objectTypes: ['DictionaryType'], required: true }]),
  schema('Relation', '关联字段', [{ component: 'modelSelect', label: translateCurrentLiteral("关联模型"), name: 'relationModelId', required: true }, { component: 'text', label: translateCurrentLiteral("显示字段"), name: 'displayField' }]),
  schema('File', '文件字段', [{ component: 'select', defaultValue: 'single', label: translateCurrentLiteral("上传模式"), name: 'uploadMode', options: [{ label: translateCurrentLiteral("单文件"), value: 'single' }, { label: translateCurrentLiteral("多文件"), value: 'multiple' }] }]),
  schema('Boolean', '布尔字段', [{ component: 'text', defaultValue: '是', label: translateCurrentLiteral("真值显示"), name: 'trueText' }, { component: 'text', defaultValue: '否', label: translateCurrentLiteral("假值显示"), name: 'falseText' }]),
  schema('System', '系统字段', [{ component: 'select', label: translateCurrentLiteral("系统来源"), name: 'systemSource', options: [{ label: translateCurrentLiteral("创建人"), value: 'createdBy' }, { label: translateCurrentLiteral("创建时间"), value: 'createdTime' }, { label: translateCurrentLiteral("更新人"), value: 'updatedBy' }, { label: translateCurrentLiteral("更新时间"), value: 'updatedTime' }] }]),
  schema('Computed', '计算字段', [{ component: 'textarea', label: translateCurrentLiteral("计算表达式"), name: 'expression', required: true, span: 2 }])
];
