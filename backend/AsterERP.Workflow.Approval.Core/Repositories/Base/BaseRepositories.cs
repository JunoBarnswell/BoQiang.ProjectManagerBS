using AsterERP.Workflow.Approval.Api.Models.Base;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Base;

public class AppRepository : SqlSugarRepository<App>, IAppRepository
{
    public AppRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class AreaRepository : SqlSugarRepository<Area>, IAreaRepository
{
    public AreaRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class CategoryRepository : SqlSugarRepository<Category>, ICategoryRepository
{
    public CategoryRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class DicItemRepository : SqlSugarRepository<DicItem>, IDicItemRepository
{
    public DicItemRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class DicTypeRepository : SqlSugarRepository<DicType>, IDicTypeRepository
{
    public DicTypeRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class DictionaryRepository : SqlSugarRepository<Dictionary>, IDictionaryRepository
{
    public DictionaryRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class SystemConfigRepository : SqlSugarRepository<SystemConfig>, ISystemConfigRepository
{
    public SystemConfigRepository(ISqlSugarClient db) : base(db)
    {
    }
}
