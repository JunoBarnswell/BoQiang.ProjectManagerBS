import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { ConfigFormSchema } from '../configFormTypes';

const commonTaskFields = [
  { component: 'objectSelect', label: translateCurrentLiteral("来源对象"), name: 'sourceObjectId', objectResourcePaths: ['data-sources', 'microflows', 'query-datasets'], required: true, span: 2 },
  { component: 'objectSelect', label: translateCurrentLiteral("目标对象"), name: 'targetObjectId', objectResourcePaths: ['data-sources', 'microflows', 'query-datasets'], required: true, span: 2 },
  { component: 'select', defaultValue: 'Manual', label: translateCurrentLiteral("触发方式"), name: 'triggerMode', options: [{ label: translateCurrentLiteral("手动"), value: 'Manual' }, { label: translateCurrentLiteral("定时"), value: 'Scheduled' }, { label: translateCurrentLiteral("事件触发"), value: 'Event' }] },
  { component: 'switch', defaultValue: true, label: translateCurrentLiteral("启用任务"), name: 'isEnabled' },
  { component: 'mappingList', label: translateCurrentLiteral("字段映射"), name: 'mappings', required: true, span: 2 }
] as ConfigFormSchema['sections'][number]['fields'];

function schema(objectType: string, title: string, extraFields = [] as ConfigFormSchema['sections'][number]['fields']): ConfigFormSchema {
  return {
    description: `${title}配置。`,
    moduleKey: 'integration-task',
    objectType,
    sections: [
      { fields: [...commonTaskFields, ...extraFields], key: 'basic', title: '同步链路' },
      {
        collapsible: true,
        defaultCollapsed: true,
        fields: [
          { component: 'textarea', label: translateCurrentLiteral("来源 SQL"), name: 'sourceSql', span: 2 },
          { component: 'tableSelect', label: translateCurrentLiteral("来源表"), name: 'sourceTable', tableDataSourceField: 'sourceObjectId' },
          { component: 'number', defaultValue: 3, label: translateCurrentLiteral("失败重试次数"), name: 'retryCount' },
          { component: 'number', defaultValue: 60, label: translateCurrentLiteral("重试间隔秒"), name: 'retryIntervalSeconds' },
          { component: 'keyValueList', label: translateCurrentLiteral("告警配置"), name: 'alerts', span: 2 }
        ],
        key: 'advanced',
        title: '高级配置'
      }
    ],
    title
  };
}

export const integrationTaskSchemas: ConfigFormSchema[] = [
  schema('DatabaseToDatabase', '数据库到数据库同步'),
  schema('DatabaseToApi', '数据库到 API 推送', [
    { component: 'text', label: translateCurrentLiteral("目标 API 地址"), name: 'targetApiUrl', span: 2, validation: [{ type: 'url' }] },
    { component: 'password', label: translateCurrentLiteral("目标 API Token"), name: 'targetApiToken', sensitive: true, target: 'secret' }
  ]),
  schema('ApiToDatabase', 'API 到数据库拉取', [
    { component: 'text', label: translateCurrentLiteral("来源 API 地址"), name: 'sourceApiUrl', span: 2, validation: [{ type: 'url' }] },
    { component: 'password', label: translateCurrentLiteral("来源 API Token"), name: 'sourceApiToken', sensitive: true, target: 'secret' }
  ]),
  schema('FileImport', '文件导入同步', [
    { component: 'select', defaultValue: 'Excel', label: translateCurrentLiteral("文件类型"), name: 'fileType', options: [{ label: 'Excel', value: 'Excel' }, { label: 'CSV', value: 'CSV' }] }
  ]),
  schema('QueueConsumer', '消息队列消费', [
    { component: 'text', label: 'Topic/Queue', name: 'queueName' },
    { component: 'select', defaultValue: 'Json', label: translateCurrentLiteral("消息格式"), name: 'messageFormat', options: [{ label: 'JSON', value: 'Json' }, { label: translateCurrentLiteral("文本"), value: 'Text' }] }
  ]),
  schema('WebhookReceiver', 'Webhook 接收同步', [
    { component: 'text', label: translateCurrentLiteral("接收路径"), name: 'webhookPath', span: 2 },
    { component: 'password', label: translateCurrentLiteral("签名密钥"), name: 'secret', sensitive: true, target: 'secret' }
  ]),
  schema('Scheduled', '定时任务', [
    { component: 'text', defaultValue: '0 0 * * *', label: translateCurrentLiteral("Cron 表达式"), name: 'cronExpression' }
  ]),
  schema('EventTriggered', '事件触发任务', [
    { component: 'text', label: translateCurrentLiteral("事件编码"), name: 'eventCode', required: true }
  ])
];
