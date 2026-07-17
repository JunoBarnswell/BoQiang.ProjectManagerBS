import type { ApplicationDataCenterResourcePath } from '../../../api/application-data-center/applicationDataCenter.api';
import type { ApplicationDataCenterModuleKey } from '../../../api/application-data-center/applicationDataCenter.types';
import type { AppIconName } from '../../../shared/icons/AppIcon';

export interface DataCenterModulePermissions {
  add: string;
  delete: string;
  disable: string;
  edit: string;
  enable: string;
  preview: string;
  publish: string;
  reference: string;
  test: string;
  view: string;
}

export interface DataCenterModuleConfig {
  defaultObjectType: string;
  description: string;
  icon: AppIconName;
  itemName: string;
  moduleKey: ApplicationDataCenterModuleKey;
  permissions: DataCenterModulePermissions;
  resourcePath: ApplicationDataCenterResourcePath;
  routePath: string;
  title: string;
}

function permissions(module: ApplicationDataCenterModuleKey): DataCenterModulePermissions {
  const prefix = `app:data-center:${module}`;
  return {
    add: `${prefix}:add`,
    delete: `${prefix}:delete`,
    disable: `${prefix}:disable`,
    edit: `${prefix}:edit`,
    enable: `${prefix}:enable`,
    preview: `${prefix}:preview`,
    publish: `${prefix}:publish`,
    reference: `${prefix}:reference`,
    test: `${prefix}:test`,
    view: `${prefix}:view`
  };
}

export const dataCenterModules: Record<ApplicationDataCenterResourcePath, DataCenterModuleConfig> = {
  'data-sources': {
    defaultObjectType: 'Sqlite',
    description: '维护应用级数据库、受支持的外部数据库以及 Excel/CSV 文件数据源配置，并支持完整诊断。',
    icon: 'database',
    itemName: '数据源',
    moduleKey: 'data-source',
    permissions: permissions('data-source'),
    resourcePath: 'data-sources',
    routePath: 'data-center/data-sources',
    title: '数据源管理'
  },
  'connection-tests': {
    defaultObjectType: 'Connectivity',
    description: '编排应用级数据源检测任务，记录检测模板、检测参数、运行结果和处理建议。',
    icon: 'shield',
    itemName: '检测任务',
    moduleKey: 'connection-test',
    permissions: permissions('connection-test'),
    resourcePath: 'connection-tests',
    routePath: 'data-center/connection-tests',
    title: '连接检测'
  },
  models: {
    defaultObjectType: 'FromDataSource',
    description: '定义应用运行时数据模型、字段、主键、数据源和可执行 CRUD 操作，并发布到当前应用库。',
    icon: 'table',
    itemName: '数据模型',
    moduleKey: 'data-model',
    permissions: permissions('data-model'),
    resourcePath: 'models',
    routePath: 'data-center/models',
    title: '数据模型'
  },
  'api-services': {
    defaultObjectType: 'SqlQuery',
    description: '管理应用 API 路由、HTTP 方法、来源对象、权限要求和发布状态。',
    icon: 'braces',
    itemName: 'API 服务',
    moduleKey: 'api-service',
    permissions: permissions('api-service'),
    resourcePath: 'api-services',
    routePath: 'data-center/api-services',
    title: 'API 服务'
  },
  microflows: {
    defaultObjectType: 'Microflow',
    description: '设计微流、领域对象、变量、if/for、函数链、数据查询写入节点和 API 发布端点。',
    icon: 'module',
    itemName: '微流',
    moduleKey: 'microflow',
    permissions: permissions('microflow'),
    resourcePath: 'microflows',
    routePath: 'data-center/microflows',
    title: '微流管理'
  },
  'entities-fields': {
    defaultObjectType: 'Text',
    description: '维护应用级实体、字段、主键、可空、字段顺序、字段风险和字段引用范围。',
    icon: 'table',
    itemName: '实体字段',
    moduleKey: 'entity-field',
    permissions: permissions('entity-field'),
    resourcePath: 'entities-fields',
    routePath: 'data-center/entities-fields',
    title: '实体与字段'
  },
  'dictionaries-codes': {
    defaultObjectType: 'DictionaryType',
    description: '维护应用级字典分类、编码规则、字典项来源和导入导出配置。',
    icon: 'book',
    itemName: '字典编码',
    moduleKey: 'dictionary-code',
    permissions: permissions('dictionary-code'),
    resourcePath: 'dictionaries-codes',
    routePath: 'data-center/dictionaries-codes',
    title: '字典与编码'
  },
  'query-datasets': {
    defaultObjectType: 'QueryView',
    description: '创建查询视图和报表数据集，支持预览、字段识别、引用统计和发布校验。',
    icon: 'activity',
    itemName: '查询数据集',
    moduleKey: 'query-dataset',
    permissions: permissions('query-dataset'),
    resourcePath: 'query-datasets',
    routePath: 'data-center/query-datasets',
    title: '查询视图与数据集'
  },
  'integration-tasks': {
    defaultObjectType: 'DatabaseToDatabase',
    description: '配置应用级同步任务、字段映射、触发方式、试运行和同步日志。',
    icon: 'refresh',
    itemName: '同步任务',
    moduleKey: 'integration-task',
    permissions: permissions('integration-task'),
    resourcePath: 'integration-tasks',
    routePath: 'data-center/integration-tasks',
    title: '数据同步与集成任务'
  }
};

export const dataCenterModuleList = Object.values(dataCenterModules);
