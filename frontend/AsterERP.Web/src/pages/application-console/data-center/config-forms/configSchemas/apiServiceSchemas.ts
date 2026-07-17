import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { ConfigFormSchema } from '../configFormTypes';

const commonFields = [
  { component: 'select', label: translateCurrentLiteral('请求方法'), name: 'httpMethod', defaultValue: 'GET', options: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].map((value) => ({ label: value, value })) },
  { component: 'text', label: translateCurrentLiteral('路由路径'), name: 'routePath', required: true, span: 2 },
  { component: 'switch', label: translateCurrentLiteral('要求登录'), name: 'requiresAuthentication', defaultValue: true },
  { component: 'text', label: translateCurrentLiteral('权限码'), name: 'permissionCode' }
] as ConfigFormSchema['sections'][number]['fields'];

export const apiServiceSchemas: ConfigFormSchema[] = [
  {
    description: '将微流绑定为独立的应用 API 路由。',
    moduleKey: 'api-service',
    objectType: 'Microflow',
    sections: [{ fields: [...commonFields, { component: 'objectSelect', label: translateCurrentLiteral('来源微流'), name: 'sourceObjectId', objectResourcePath: 'microflows', required: true }, { component: 'text', label: 'Flow Code', name: 'flowCode' }], key: 'basic', title: '微流 API' }],
    title: '微流 API'
  },
  {
    description: '通过应用数据源执行受约束的 SQL 查询。',
    moduleKey: 'api-service',
    objectType: 'SqlQuery',
    sections: [{ fields: [...commonFields, { component: 'objectSelect', label: translateCurrentLiteral('来源数据源'), name: 'sourceObjectId', objectResourcePath: 'data-sources', required: true }, { component: 'textarea', label: 'SQL', name: 'sql', required: true, span: 2 }], key: 'basic', title: 'SQL 查询 API' }],
    title: 'SQL 查询 API'
  },
  {
    description: '将应用路由代理到外部 HTTP(S) 服务。',
    moduleKey: 'api-service',
    objectType: 'ExternalProxy',
    sections: [{ fields: [...commonFields, { component: 'text', label: translateCurrentLiteral('目标 URL'), name: 'baseUrl', required: true, validation: [{ type: 'url' }] }, { component: 'password', label: 'Bearer Token', name: 'token', target: 'secret' }], key: 'basic', title: '外部代理' }],
    title: '外部代理 API'
  },
  {
    description: '登记应用入站 Webhook 路由和认证策略。',
    moduleKey: 'api-service',
    objectType: 'Webhook',
    sections: [{ fields: commonFields, key: 'basic', title: 'Webhook' }],
    title: 'Webhook 接收 API'
  }
];
