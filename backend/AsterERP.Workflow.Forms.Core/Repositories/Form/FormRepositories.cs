using AsterERP.Workflow.Approval.Core.Repositories;
using AsterERP.Workflow.Forms.Api.Models.Form;
using SqlSugar;

namespace AsterERP.Workflow.Forms.Core.Repositories.Form;

public class FormInfoRepository : SqlSugarRepository<FormInfo>, IFormInfoRepository
{
    public FormInfoRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class FormDataInfoRepository : SqlSugarRepository<FormDataInfo>, IFormDataInfoRepository
{
    public FormDataInfoRepository(ISqlSugarClient db) : base(db)
    {
    }
}
