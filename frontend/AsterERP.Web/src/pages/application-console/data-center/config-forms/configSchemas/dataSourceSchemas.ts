import {
  applicationDataSourceDefaultSslMode,
  getApplicationDataSourceSslModes
} from '../../../../../api/application-data-center/applicationDataSourceSslMode';
import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { ConfigFieldSchema, ConfigFormSchema } from '../configFormTypes';

const databaseAdvanced = [
  { component: 'number', defaultValue: 15, label: translateCurrentLiteral("连接超时秒数"), name: 'timeoutSeconds', span: 1 },
  { component: 'number', defaultValue: 20, label: translateCurrentLiteral("连接池大小"), name: 'poolSize', span: 1 },
  { component: 'text', defaultValue: 'utf8mb4', label: translateCurrentLiteral("字符集"), name: 'charset', span: 1 }
] as const;

const databaseSsl = (defaultValue: string = 'Preferred'): ConfigFieldSchema[] => [
  { component: 'select', defaultValue, label: 'SSL mode', name: 'sslMode', options: [{ label: 'Preferred', value: 'Preferred' }, { label: 'Required', value: 'Required' }, { label: 'Verify CA', value: 'VerifyCA' }, { label: 'Verify full', value: 'VerifyFull' }], span: 1 }
];

function databaseSchema(objectType: string, title: string, port: number, advancedFields: readonly ConfigFormSchema['sections'][number]['fields'][number][] = [...databaseSsl(), ...databaseAdvanced]): ConfigFormSchema {
  return {
    description: `${title} 连接配置，凭据单独加密保存。`,
    moduleKey: 'data-source',
    objectType,
    sections: [
      {
        fields: [
          { component: 'text', label: translateCurrentLiteral("主机"), name: 'host', required: true },
          { component: 'number', defaultValue: port, label: translateCurrentLiteral("端口"), name: 'port', required: true, validation: [{ min: 1, max: 65535, type: 'numberRange' }] },
          { component: 'text', label: translateCurrentLiteral("数据库名称"), name: 'database', required: true },
          { component: 'text', label: translateCurrentLiteral("用户名"), name: 'user', required: true },
          { component: 'password', label: translateCurrentLiteral("密码"), name: 'password', sensitive: true, target: 'secret' }
        ],
        key: 'basic',
        title: '基础配置'
      },
      { collapsible: true, defaultCollapsed: true, fields: [...advancedFields], key: 'advanced', title: '高级配置' }
    ],
    title
  };
}

export const dataSourceSchemas: ConfigFormSchema[] = [
  {
    description: 'SQLite 文件型数据源配置。',
    moduleKey: 'data-source',
    objectType: 'Sqlite',
    sections: [
      {
        fields: [
          { component: 'text', defaultValue: 'app.db', helpText: '相对路径会保存到 API data 目录下。', label: translateCurrentLiteral("数据库文件名/路径"), name: 'databaseName', required: true },
          { component: 'switch', defaultValue: false, label: translateCurrentLiteral("只读模式"), name: 'readOnly' },
          { component: 'switch', defaultValue: true, label: translateCurrentLiteral("自动创建文件"), name: 'autoCreate' }
        ],
        key: 'basic',
        title: '基础配置'
      }
    ],
    title: 'SQLite 数据源'
  },
  {
    description: '当前应用库数据源，用于引用应用工作区自己的运行时数据。',
    moduleKey: 'data-source',
    objectType: 'ApplicationDatabase',
    sections: [
      {
        fields: [
          { component: 'switch', defaultValue: true, label: translateCurrentLiteral("系统托管"), name: 'systemManaged' },
          { component: 'text', defaultValue: 'application-data-center.sql-table', label: 'Provider Key', name: 'providerKey' },
          { component: 'textarea', helpText: '可选，只允许受控查询预览使用。', label: translateCurrentLiteral("预览 SQL"), name: 'previewSql', span: 2 }
        ],
        key: 'basic',
        title: '基础配置'
      }
    ],
    title: '应用库数据源'
  },
  databaseSchema('MySql', 'MySQL 数据源', 3306),
  databaseSchema('PostgreSQL', 'PostgreSQL 数据源', 5432),
  {
    ...databaseSchema('SqlServer', 'SQL Server 数据源', 1433, databaseAdvanced),
    sections: [
      ...databaseSchema('SqlServer', 'SQL Server 数据源', 1433, databaseAdvanced).sections,
      {
        collapsible: true,
        defaultCollapsed: true,
        fields: [
          { component: 'switch', defaultValue: true, helpText: '生产环境保持 Encrypt=true。', label: 'Encrypt', name: 'encrypt', span: 1 },
          { component: 'switch', defaultValue: false, helpText: '仅在已确认风险时关闭证书校验。', label: 'Trust server certificate', name: 'trustServerCertificate', span: 1 }
        ],
        key: 'transport-security',
        title: 'Transport security'
      }
    ]
  },
  {
    description: 'Excel 工作簿字段识别与预览配置。',
    moduleKey: 'data-source',
    objectType: 'Excel',
    sections: [
      {
        fields: [
          { component: 'text', label: translateCurrentLiteral("文件路径"), name: 'filePath', required: true, span: 2 },
          { component: 'text', label: translateCurrentLiteral("Sheet 名称"), name: 'sheetName' },
          { component: 'number', defaultValue: 1, label: translateCurrentLiteral("表头行"), name: 'headerRow' },
          { component: 'number', defaultValue: 2, label: translateCurrentLiteral("数据起始行"), name: 'dataStartRow' },
          { component: 'select', defaultValue: 'Header', label: translateCurrentLiteral("字段识别方式"), name: 'fieldDetectMode', options: [{ label: translateCurrentLiteral("表头识别"), value: 'Header' }, { label: translateCurrentLiteral("列序号识别"), value: 'ColumnIndex' }] },
          { component: 'select', defaultValue: 'Manual', label: translateCurrentLiteral("刷新方式"), name: 'refreshMode', options: [{ label: translateCurrentLiteral("手动"), value: 'Manual' }, { label: translateCurrentLiteral("定时"), value: 'Scheduled' }] }
        ],
        key: 'basic',
        title: '基础配置'
      }
    ],
    title: 'Excel 文件'
  },
  {
    description: 'CSV 文件字段识别与预览配置。',
    moduleKey: 'data-source',
    objectType: 'Csv',
    sections: [
      {
        fields: [
          { component: 'text', label: translateCurrentLiteral("文件路径"), name: 'filePath', required: true, span: 2 },
          { component: 'text', defaultValue: ',', label: translateCurrentLiteral("分隔符"), name: 'delimiter' },
          { component: 'select', defaultValue: 'utf8', label: translateCurrentLiteral("编码"), name: 'encoding', options: [{ label: 'UTF-8', value: 'utf8' }, { label: 'Unicode', value: 'unicode' }] },
          { component: 'switch', defaultValue: true, label: translateCurrentLiteral("首行为表头"), name: 'firstRowHeader' },
          { component: 'number', defaultValue: 1, label: translateCurrentLiteral("数据起始行"), name: 'dataStartRow' }
        ],
        key: 'basic',
        title: '基础配置'
      }
    ],
    title: 'CSV 文件'
  }
];

for (const databaseType of ['MySql', 'PostgreSQL'] as const) {
  const database = dataSourceSchemas.find((item) => item.objectType === databaseType);
  const sslMode = database?.sections.flatMap((section) => section.fields).find((field) => field.name === 'sslMode');
  if (sslMode) {
    sslMode.defaultValue = applicationDataSourceDefaultSslMode;
    sslMode.options = getApplicationDataSourceSslModes(databaseType).map((value) => ({ label: value, value }));
  }

  if (databaseType === 'PostgreSQL') {
    const charset = database?.sections.flatMap((section) => section.fields).find((field) => field.name === 'charset');
    if (charset) {
      charset.defaultValue = 'UTF8';
    }
  }
}

const sqlServer = dataSourceSchemas.find((item) => item.objectType === 'SqlServer');
if (sqlServer) {
  for (const section of sqlServer.sections) {
    section.fields = section.fields.filter((field) => field.name !== 'charset');
  }
}
