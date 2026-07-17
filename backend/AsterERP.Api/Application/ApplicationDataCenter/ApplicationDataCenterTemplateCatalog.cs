using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterTemplateCatalog
{
    public IReadOnlyList<ApplicationDataCenterTypeOptionResponse> GetTypeOptions(string? moduleKey = null)
    {
        var options = BuildTypeOptions();
        return string.IsNullOrWhiteSpace(moduleKey)
            ? options
            : options.Where(item => string.Equals(item.ModuleKey, moduleKey, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public IReadOnlyList<ApplicationDataCenterTemplateResponse> GetTemplates(string? moduleKey = null)
    {
        var templates = BuildTemplates();
        return string.IsNullOrWhiteSpace(moduleKey)
            ? templates
            : templates.Where(item => string.Equals(item.ModuleKey, moduleKey, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public IReadOnlyList<ApplicationDataCenterNextActionResponse> BuildNextActions(string moduleKey, string objectId, string status) =>
        moduleKey switch
        {
            ApplicationDataCenterModuleKey.DataSource =>
            [
                new("test", "测试连接", "验证当前数据源能否访问并具备所需权限。", null, PermissionCodes.AppDataCenterDataSourceTest),
                new("create-microflow", "创建微流", "进入微流控制台配置领域对象、查询、写入和接口发布。", "/data-center/microflows", PermissionCodes.AppDataCenterMicroflowAdd),
                new("create-query", "创建查询数据集", "基于该数据源创建查询视图或报表数据集。", "/data-center/query-datasets", PermissionCodes.AppDataCenterQueryDatasetAdd),
                new("create-model", "创建数据模型", "基于该数据源创建运行时数据模型。", "/data-center/models", PermissionCodes.AppDataCenterDataModelAdd)
            ],
            ApplicationDataCenterModuleKey.ConnectionTest =>
            [
                new("rerun", "重新检测", "按当前检测模板重新执行一次。", null, PermissionCodes.AppDataCenterConnectionTestTest),
                new("fix-source", "修改数据源", "回到数据源管理调整连接配置。", "/data-center/data-sources", PermissionCodes.AppDataCenterDataSourceEdit)
            ],
            ApplicationDataCenterModuleKey.EntityField =>
            [
                new("bind-dict", "绑定字典或编码", "为状态、分类、编号字段绑定统一规则。", "/data-center/dictionaries-codes", PermissionCodes.AppDataCenterDictionaryCodeAdd),
                new("create-query", "生成查询视图", "使用字段定义创建列表查询数据集。", "/data-center/query-datasets", PermissionCodes.AppDataCenterQueryDatasetAdd)
            ],
            ApplicationDataCenterModuleKey.DictionaryCode =>
            [
                new("preview", "预览编码", "验证字典项或编码规则输出。", null, PermissionCodes.AppDataCenterDictionaryCodePreview),
                new("reference", "查看使用位置", "检查字段、页面和 API 是否引用该规则。", null, PermissionCodes.AppDataCenterDictionaryCodeReference)
            ],
            ApplicationDataCenterModuleKey.DataModel =>
            [
                new("publish-runtime-model", "发布运行时模型", "将字段和 CRUD 操作写入当前应用库的 system_data_models。", null, PermissionCodes.AppDataCenterDataModelPublish),
                new("reference", "查看使用位置", "检查页面、微流和接口是否引用此模型。", null, PermissionCodes.AppDataCenterDataModelReference)
            ],
            ApplicationDataCenterModuleKey.ApiService =>
            [
                new("test-route", "测试 API 配置", "校验路由、请求方法、来源和权限配置。", null, PermissionCodes.AppDataCenterApiServiceTest),
                new("preview-route", "预览 API 路由", "查看路由、来源和认证要求，不执行外部调用。", null, PermissionCodes.AppDataCenterApiServicePreview)
            ],
            ApplicationDataCenterModuleKey.QueryDataset =>
            [
                new("preview", "预览数据", "验证查询字段、条件、分页与返回结果。", null, PermissionCodes.AppDataCenterQueryDatasetPreview),
                new("bind-page", "交给页面使用", "将数据集作为页面或报表数据来源。", "/development-center", PermissionCodes.AppDevelopmentCenterView)
            ],
            ApplicationDataCenterModuleKey.IntegrationTask =>
            [
                new("run", "试运行任务", "按当前来源、目标和映射执行一次试运行。", null, PermissionCodes.AppDataCenterIntegrationTaskTest),
                new("logs", "查看同步日志", "检查运行结果、失败原因和处理建议。", null, PermissionCodes.AppDataCenterIntegrationTaskView)
            ],
            _ => []
        };

    private static IReadOnlyList<ApplicationDataCenterTypeOptionResponse> BuildTypeOptions() =>
    [
        new(ApplicationDataCenterModuleKey.DataSource, ApplicationDataSourceType.Sqlite, "SQLite 数据源", "应用级或外部 SQLite 数据库文件。", ["databaseName"], ["连接", "读权限", "写权限"]),
        new(ApplicationDataCenterModuleKey.DataSource, ApplicationDataSourceType.MySql, "MySQL 数据源", "MySQL/MariaDB 业务库。", ["host", "port", "database", "user"], ["连接", "读权限", "写权限"]),
        new(ApplicationDataCenterModuleKey.DataSource, ApplicationDataSourceType.PostgreSql, "PostgreSQL 数据源", "PostgreSQL 业务库。", ["host", "port", "database", "user"], ["连接", "SQL 方言"]),
        new(ApplicationDataCenterModuleKey.DataSource, ApplicationDataSourceType.SqlServer, "SQL Server 数据源", "SQL Server 业务库。", ["host", "database"], ["连接", "认证"]),
        new(ApplicationDataCenterModuleKey.DataSource, ApplicationDataSourceType.Excel, "Excel 文件", "从 Excel 工作簿预览和识别字段。", ["filePath", "sheetName"], ["预览", "字段识别"]),
        new(ApplicationDataCenterModuleKey.DataSource, ApplicationDataSourceType.Csv, "CSV 文件", "从 CSV 文件预览和识别字段。", ["filePath", "delimiter"], ["预览", "字段识别"]),
        new(ApplicationDataCenterModuleKey.ConnectionTest, ApplicationConnectionTestTemplate.Connectivity, "基础连通性", "检查能否连接目标数据源。", ["dataSourceId"], ["连接"]),
        new(ApplicationDataCenterModuleKey.ConnectionTest, ApplicationConnectionTestTemplate.ReadPermission, "读权限测试", "执行查询验证读权限。", ["dataSourceId", "sql"], ["查询"]),
        new(ApplicationDataCenterModuleKey.DataModel, ApplicationDataModelBuildMode.FromDataSource, "表数据模型", "从当前应用数据源识别并发布运行时字段。", ["sourceDataSourceId", "sourceTable", "fields"], ["字段识别", "发布"]),
        new(ApplicationDataCenterModuleKey.DataModel, ApplicationDataModelBuildMode.FromApiResponse, "API 响应模型", "从 API 响应字段定义运行时模型。", ["fields"], ["字段校验", "发布"]),
        new(ApplicationDataCenterModuleKey.ApiService, ApplicationApiServiceSourceType.Microflow, "微流 API", "将已配置的微流发布为应用 API 路由。", ["routePath", "sourceObjectId", "permissionCode"], ["路由校验", "发布"]),
        new(ApplicationDataCenterModuleKey.ApiService, ApplicationApiServiceSourceType.SqlQuery, "SQL 查询 API", "通过应用数据源执行受约束的查询并返回结果。", ["routePath", "sourceObjectId", "sql"], ["来源校验", "预览"]),
        new(ApplicationDataCenterModuleKey.ApiService, ApplicationApiServiceSourceType.ExternalProxy, "外部代理 API", "将应用路由代理到配置的 HTTP(S) 服务。", ["routePath", "baseUrl"], ["URL 校验", "发布"]),
        new(ApplicationDataCenterModuleKey.ApiService, ApplicationApiServiceSourceType.Webhook, "Webhook 接收 API", "登记应用入站 Webhook 路由和认证策略。", ["routePath", "requiresAuthentication"], ["路由校验", "发布"]),
        new(ApplicationDataCenterModuleKey.EntityField, ApplicationEntityFieldType.Text, "文本字段", "名称、编码、备注等文本属性。", ["fieldCode", "fieldName"], ["校验"]),
        new(ApplicationDataCenterModuleKey.EntityField, ApplicationEntityFieldType.Dictionary, "字典字段", "绑定标准字典或状态枚举。", ["fieldCode", "dictType"], ["校验"]),
        new(ApplicationDataCenterModuleKey.DictionaryCode, ApplicationDictionaryCodeObjectType.DictionaryType, "字典分类", "管理一组字典项。", ["dictCode", "dictName"], ["引用"]),
        new(ApplicationDataCenterModuleKey.DictionaryCode, ApplicationDictionaryCodeObjectType.CodeRule, "编码规则", "生成业务编号。", ["ruleCode", "segments"], ["预览"]),
        new(ApplicationDataCenterModuleKey.QueryDataset, ApplicationQueryDatasetType.QueryView, "查询视图", "列表页和检索数据源。", ["sourceObjectId", "fields"], ["预览"]),
        new(ApplicationDataCenterModuleKey.QueryDataset, ApplicationQueryDatasetType.ReportDataset, "报表数据集", "聚合统计数据集。", ["sourceObjectId", "dimensions", "measures"], ["预览"]),
        new(ApplicationDataCenterModuleKey.IntegrationTask, ApplicationIntegrationTaskType.DatabaseToDatabase, "数据库到数据库同步", "两个数据库表之间同步。", ["sourceObjectId", "targetObjectId", "mappings"], ["试运行"]),
        new(ApplicationDataCenterModuleKey.IntegrationTask, ApplicationIntegrationTaskType.FileImport, "文件导入同步", "Excel/CSV 导入到目标模型。", ["sourceObjectId", "targetObjectId", "mappings"], ["试运行"])
    ];

    private static IReadOnlyList<ApplicationDataCenterTemplateResponse> BuildTemplates() =>
    [
        new(ApplicationDataCenterModuleKey.DataSource, "sqlite-managed", "应用 SQLite 模板", ApplicationDataSourceType.Sqlite, "连接应用级或外部 SQLite 文件。", "{\"databaseName\":\"mes.db\",\"readOnly\":false}"),
        new(ApplicationDataCenterModuleKey.IntegrationTask, "erp-wms-master-sync", "ERP 到 WMS 主数据同步", ApplicationIntegrationTaskType.DatabaseToDatabase, "数据库表之间按字段映射同步。", "{\"triggerMode\":\"Manual\",\"mappings\":[]}")
    ];
}
