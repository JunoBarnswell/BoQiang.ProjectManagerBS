using AsterERP.Workflow.Approval.Api.Constants;
using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using Microsoft.Extensions.Logging;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using System.Security.Cryptography;
using System.Text;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public sealed class SystemInitializer
{
    private readonly IAppPrivilegeValueRepository _appPrivilegeValueRepository;
    private readonly IAppRepository _appRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SystemInitializer> _logger;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public SystemInitializer(
        IAppPrivilegeValueRepository appPrivilegeValueRepository,
        IAppRepository appRepository,
        ICategoryRepository categoryRepository,
        IUserRepository userRepository,
        ILogger<SystemInitializer> logger,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _appPrivilegeValueRepository = appPrivilegeValueRepository;
        _appRepository = appRepository;
        _categoryRepository = categoryRepository;
        _userRepository = userRepository;
        _logger = logger;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAdminUserAsync(cancellationToken);
        await EnsurePrivilegeValuesAsync(cancellationToken);
        await EnsureDefaultAppAsync(cancellationToken);
        await EnsureFlowCategoriesAsync(cancellationToken);
    }

    private async Task EnsurePrivilegeValuesAsync(CancellationToken cancellationToken)
    {
        var count = await _appPrivilegeValueRepository.Db.Queryable<AppPrivilegeValue>().CountAsync(cancellationToken);
        if (count > 0)
        {
            return;
        }

        var list = new List<AppPrivilegeValue>
        {
            CreatePrivilegeValue(0, "添加", 1),
            CreatePrivilegeValue(1, "查询", 2),
            CreatePrivilegeValue(2, "修改", 3),
            CreatePrivilegeValue(3, "删除", 4),
            CreatePrivilegeValue(4, "打印", 5),
            CreatePrivilegeValue(5, "导出", 6),
            CreatePrivilegeValue(6, "授权", 7),
            CreatePrivilegeValue(7, "发布", 8)
        };

        await _appPrivilegeValueRepository.Db.Insertable(list).ExecuteCommandAsync(cancellationToken);
        _logger.LogInformation("SystemInitializer: AppPrivilegeValue data initialized");
    }

    private async Task EnsureAdminUserAsync(CancellationToken cancellationToken)
    {
        var password = GetMd5Password("123456");
        var existingAdmin = await _userRepository.Db.Queryable<User>()
            .FirstAsync(user => user.Username == "admin" || user.UserNo == "admin", cancellationToken);
        if (existingAdmin != null)
        {
            existingAdmin.Username = "admin";
            existingAdmin.UserNo = "admin";
            existingAdmin.RealName = string.IsNullOrWhiteSpace(existingAdmin.RealName) ? "Administrator" : existingAdmin.RealName;
            existingAdmin.Password = password;
            existingAdmin.DelFlag = 1;
            existingAdmin.UpdateTime = _clock.Now;
            existingAdmin.Updator = "system";
            existingAdmin.Keyword = "admin Administrator";
            await _userRepository.Db.Updateable(existingAdmin).ExecuteCommandAsync(cancellationToken);
            _logger.LogInformation("SystemInitializer: default admin user updated");
            return;
        }

        var count = await _userRepository.Db.Queryable<User>().CountAsync(cancellationToken);
        if (count > 0)
        {
            return;
        }

        await _userRepository.Db.Insertable(new User
        {
            Id = _guidGenerator.Create().ToString("N"),
            Username = "admin",
            UserNo = "admin",
            RealName = "Administrator",
            Password = password,
            Tel = string.Empty,
            Mobile = string.Empty,
            Phone = string.Empty,
            Email = string.Empty,
            Image = string.Empty,
            CompanyId = string.Empty,
            DepartmentId = string.Empty,
            Sex = 0,
            Address = string.Empty,
            Fax = string.Empty,
            FailMonth = 0,
            FailureTime = _clock.Now.AddYears(10),
            PwdFtime = _clock.Now,
            PwdInit = 0,
            AclTimestamp = new DateTimeOffset(_clock.Now).ToUnixTimeMilliseconds(),
            Creator = "system",
            CreateTime = _clock.Now,
            Updator = "system",
            UpdateTime = _clock.Now,
            DelFlag = 1,
            Keyword = "admin Administrator"
        }).ExecuteCommandAsync(cancellationToken);

        _logger.LogInformation("SystemInitializer: default admin user initialized");
    }

    private async Task EnsureDefaultAppAsync(CancellationToken cancellationToken)
    {
        var exists = await _appRepository.Db.Queryable<App>()
            .AnyAsync(app => app.Sn == "FLOWMASTER", cancellationToken);
        if (exists)
        {
            return;
        }

        await _appRepository.Db.Insertable(new App
        {
            Id = _guidGenerator.Create().ToString("N"),
            Name = "FlowMaster",
            Sn = "FLOWMASTER",
            SecretKey = _guidGenerator.Create().ToString("N"),
            Status = 1,
            Url = "http://localhost:5081",
            IndexUrl = "http://127.0.0.1:3201",
            Image = string.Empty,
            Note = "Default local integration app",
            OrderNo = 1,
            PlatformEnabled = 1,
            Creator = "system",
            CreateTime = _clock.Now,
            Updator = "system",
            UpdateTime = _clock.Now,
            DelFlag = 1,
            Keyword = "FlowMaster FLOWMASTER"
        }).ExecuteCommandAsync(cancellationToken);

        _logger.LogInformation("SystemInitializer: default app initialized");
    }

    private async Task EnsureFlowCategoriesAsync(CancellationToken cancellationToken)
    {
        var exists = await _categoryRepository.Db.Queryable<Category>()
            .AnyAsync(category => category.Code == "FLOW", cancellationToken);
        if (exists)
        {
            return;
        }

        var categories = new List<Category>
        {
            CreateCategory("0", "流程分类", "FLOW", "流程", 100),
            CreateCategory("FLOW", "通用审批", "FLOW_GENERAL", "通用", 90),
            CreateCategory("FLOW", "人事流程", "FLOW_HR", "人事", 80)
        };

        await _categoryRepository.Db.Insertable(categories).ExecuteCommandAsync(cancellationToken);
        _logger.LogInformation("SystemInitializer: default flow categories initialized");
    }

    private AppPrivilegeValue CreatePrivilegeValue(int position, string name, int orderNo)
    {
        return new AppPrivilegeValue
        {
            Id = _guidGenerator.Create().ToString("N"),
            Position = position,
            Name = name,
            OrderNo = orderNo,
            Remark = string.Empty,
            Creator = "system",
            CreateTime = _clock.Now,
            Updator = "system",
            UpdateTime = _clock.Now,
            DelFlag = 1,
            Keyword = name
        };
    }

    private static string GetMd5Password(string password)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(WorkflowApprovalConstants.Md5Prefix + password.Trim());
        var hashBytes = md5.ComputeHash(inputBytes);
        return string.Concat(hashBytes.Select(item => item.ToString("x2")));
    }

    private Category CreateCategory(string pid, string name, string code, string shortName, int orderNo)
    {
        return new Category
        {
            Id = _guidGenerator.Create().ToString("N"),
            Pid = pid,
            Name = name,
            Code = code,
            FrontShow = 1,
            ShortName = shortName,
            Note = string.Empty,
            OrderNo = orderNo,
            CompanyId = string.Empty,
            Creator = "system",
            CreateTime = _clock.Now,
            Updator = "system",
            UpdateTime = _clock.Now,
            DelFlag = 1,
            Keyword = $"{name} {code}"
        };
    }
}
