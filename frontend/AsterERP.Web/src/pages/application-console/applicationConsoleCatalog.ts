import type { AppIconName } from '../../shared/icons/AppIcon';

export type ApplicationConsolePageKey =
  | 'home'
  | 'console'
  | 'development-center'
  | 'data-center';

export interface ApplicationConsoleNavItem {
  description: string;
  icon: AppIconName;
  key: ApplicationConsolePageKey;
  permissionCode: string;
  slug: string;
  title: string;
}

export interface ApplicationCapabilityItem {
  description: string;
  icon: AppIconName;
  links: string[];
  permissionCode?: string;
  routePath?: string;
  title: string;
  tone: 'blue' | 'emerald' | 'orange' | 'purple';
}

export const applicationConsoleNavItems: ApplicationConsoleNavItem[] = [
  {
    description: '查看当前应用概况、指标、快捷入口与最近动态。',
    icon: 'home',
    key: 'home',
    permissionCode: 'app:home:view',
    slug: 'home',
    title: '首页'
  },
  {
    description: '作为应用开发入口台，聚合数据建模、页面设计、流程与数据中心的真实承接能力。',
    icon: 'module',
    key: 'console',
    permissionCode: 'app:console:view',
    slug: 'console',
    title: '应用控制台'
  },
  {
    description: '构建应用页面、流程、模板、运行时页面和公共资源。',
    icon: 'code',
    key: 'development-center',
    permissionCode: 'app:development-center:view',
    slug: 'development-center',
    title: '开发中心'
  },
  {
    description: '管理数据源、微流编排、领域对象、查询视图和同步集成。',
    icon: 'database',
    key: 'data-center',
    permissionCode: 'app:data-center:view',
    slug: 'data-center',
    title: '数据中心'
  }
];

export const capabilityGroups: Record<Exclude<ApplicationConsolePageKey, 'home' | 'console'>, ApplicationCapabilityItem[]> = {
  'development-center': [
    {
      description: '设计、预览并发布业务页面。',
      icon: 'files',
      links: [],
      permissionCode: 'app:development-center:business-object:view',
      routePath: 'development-center/pages',
      title: '页面设计器',
      tone: 'emerald'
    }
  ],
  'data-center': [
    {
      description: '从数据源列表进入真实数据库工作台，维护表、视图、接口、映射缓存和运行检查。',
      icon: 'database',
      links: ['数据源列表', '进入工作台', '表与视图', '接口与缓存'],
      permissionCode: 'app:data-center:data-source:view',
      routePath: 'data-center/data-sources',
      title: '数据库工作台',
      tone: 'purple'
    },
    {
      description: '维护应用系统、运行版本、无权限显示和授权对象。',
      icon: 'shield',
      links: ['应用系统', '运行版本', '无权限显示', '授权对象'],
      permissionCode: 'app:data-center:data-source:view',
      routePath: 'data-center/application-assignments',
      title: '应用系统分配',
      tone: 'purple'
    },
    {
      description: '测试数据源连通性、认证信息和性能。',
      icon: 'shield',
      links: ['连接测试', '性能检测', '连通性历史', '告警与通知'],
      permissionCode: 'app:data-center:connection-test:view',
      routePath: 'data-center/connection-tests',
      title: '数据库连接测试',
      tone: 'purple'
    },
    {
      description: '设计微流、领域对象、变量、函数链、数据查询写入节点和 API 发布端点。',
      icon: 'module',
      links: ['领域对象', 'if/for', '函数链', '接口发布'],
      permissionCode: 'app:data-center:microflow:view',
      routePath: 'data-center/microflows',
      title: '微流管理',
      tone: 'purple'
    },
    {
      description: '维护实体、字段、关联关系和字段权限。',
      icon: 'table',
      links: ['实体管理', '字段管理', '关联关系', '字段权限'],
      permissionCode: 'app:data-center:entity-field:view',
      routePath: 'data-center/entities-fields',
      title: '数据实体与字段',
      tone: 'purple'
    },
    {
      description: '管理字典分类、编码规则和导入导出。',
      icon: 'book',
      links: ['字典分类', '字典项管理', '编码规则', '导入导出'],
      permissionCode: 'app:data-center:dictionary-code:view',
      routePath: 'data-center/dictionaries-codes',
      title: '字典与编码',
      tone: 'purple'
    },
    {
      description: '创建查询视图和报表数据集。',
      icon: 'activity',
      links: ['查询视图', '报表数据集', '字段映射', '使用统计'],
      permissionCode: 'app:data-center:query-dataset:view',
      routePath: 'data-center/query-datasets',
      title: '查询视图与报表数据集',
      tone: 'purple'
    },
    {
      description: '配置同步任务、运行监控和同步日志。',
      icon: 'refresh',
      links: ['任务列表', '任务配置', '运行监控', '同步日志'],
      permissionCode: 'app:data-center:integration-task:view',
      routePath: 'data-center/integration-tasks',
      title: '数据同步与集成任务',
      tone: 'purple'
    }
  ]
};

const navItemByKey = new Map(applicationConsoleNavItems.map((item) => [item.key, item]));

export function getApplicationConsoleNavItem(key: ApplicationConsolePageKey): ApplicationConsoleNavItem {
  return navItemByKey.get(key) ?? applicationConsoleNavItems[0];
}
