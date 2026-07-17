using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Domain.System.Announcements;

public static class AnnouncementDomainPolicy
{
    public static void EnsureUpsertRequest(string title, string content, string announcementType, string scope)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ValidationException("公告标题不能为空");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("公告内容不能为空");
        }

        if (string.IsNullOrWhiteSpace(announcementType))
        {
            throw new ValidationException("公告类型不能为空");
        }

        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ValidationException("公告范围不能为空");
        }
    }
}
