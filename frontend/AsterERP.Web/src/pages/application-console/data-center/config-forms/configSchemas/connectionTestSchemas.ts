import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { ConfigFormSchema } from '../configFormTypes';

function schema(objectType: string, title: string, extraFields = [] as ConfigFormSchema['sections'][number]['fields']): ConfigFormSchema {
  return {
    description: `${title}检测模板配置。`,
    moduleKey: 'connection-test',
    objectType,
    sections: [
      {
        fields: [
          { component: 'dataSourceSelect', helpText: '选择已创建的数据源；从数据源工作台进入时会自动带入当前数据库。', label: translateCurrentLiteral("数据源"), name: 'dataSourceId', required: true, span: 2 },
          { component: 'number', defaultValue: 1, label: translateCurrentLiteral("重试次数"), name: 'retryCount' },
          { component: 'number', defaultValue: 30, label: translateCurrentLiteral("超时秒数"), name: 'timeoutSeconds' },
          ...extraFields
        ],
        key: 'basic',
        title: '检测配置'
      }
    ],
    title
  };
}

export const connectionTestSchemas: ConfigFormSchema[] = [
  schema('Connectivity', '基础连通性'),
  schema('Authentication', '账号认证'),
  schema('ReadPermission', '读权限测试', [{ component: 'textarea', label: translateCurrentLiteral("读测试 SQL"), name: 'sql', span: 2 }]),
  schema('WritePermission', '写权限测试', [{ component: 'textarea', label: translateCurrentLiteral("写测试 SQL"), name: 'sql', span: 2 }]),
  schema('SqlDialect', 'SQL 方言测试', [{ component: 'textarea', label: translateCurrentLiteral("方言 SQL"), name: 'sql', span: 2 }]),
  schema('Performance', '性能检测', [{ component: 'number', defaultValue: 1000, label: translateCurrentLiteral("最大耗时 ms"), name: 'maxDurationMs' }]),
  schema('Tls', 'SSL / TLS 检测', [{ component: 'switch', defaultValue: true, label: translateCurrentLiteral("要求 TLS"), name: 'requireTls' }]),
  schema('CustomSql', '自定义 SQL 检测', [{ component: 'textarea', label: translateCurrentLiteral("自定义 SQL"), name: 'sql', required: true, span: 2 }])
];
