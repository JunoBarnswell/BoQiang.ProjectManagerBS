import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { ConfigFormSchema } from '../configFormTypes';

const fields = [
  { component: 'objectSelect', label: translateCurrentLiteral('来源数据源'), name: 'sourceDataSourceId', objectResourcePaths: ['data-sources'], required: true },
  { component: 'tableSelect', label: translateCurrentLiteral('来源表'), name: 'sourceTable', tableDataSourceField: 'sourceDataSourceId', required: true },
  { component: 'select', label: translateCurrentLiteral('主键生成'), name: 'idGeneration', options: [{ label: 'Guid', value: 'guid' }, { label: translateCurrentLiteral('手工主键'), value: 'manual' }, { label: 'Snowflake', value: 'snowflake' }], defaultValue: 'guid' },
  { component: 'text', label: translateCurrentLiteral('主键字段'), name: 'keyField', required: true },
  { component: 'fieldList', label: translateCurrentLiteral('字段定义'), name: 'fields', span: 2, required: true },
  { component: 'keyValueList', label: translateCurrentLiteral('运行时操作'), name: 'operations', span: 2, required: true },
  { component: 'text', label: translateCurrentLiteral('权限码'), name: 'permissionCode' }
] as ConfigFormSchema['sections'][number]['fields'];

export const dataModelSchemas: ConfigFormSchema[] = [
  {
    description: '配置应用级数据模型的来源、字段和运行时操作。',
    moduleKey: 'data-model',
    objectType: 'FromDataSource',
    sections: [{ fields, key: 'model', title: '模型定义' }],
    title: 'SQL 表模型'
  },
  {
    description: '配置文件或 API 返回结构的只读运行时模型。',
    moduleKey: 'data-model',
    objectType: 'FromApiResponse',
    sections: [{ fields: fields.filter((field) => field.name !== 'sourceTable'), key: 'model', title: '模型定义' }],
    title: '外部响应模型'
  }
];
