using AsterERP.Shared;
using AsterERP.Contracts.System.Announcements;

namespace AsterERP.Api.Application.System.Announcements;

public interface IAnnouncementService
{
    Task<GridPageResult<AnnouncementListItemResponse>> GetPageAsync(
        GridQuery gridQuery,
        CancellationToken cancellationToken = default);

    Task<AnnouncementListItemResponse> CreateAsync(
        AnnouncementUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<AnnouncementListItemResponse> UpdateAsync(
        string id,
        AnnouncementUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<AnnouncementListItemResponse> PublishAsync(string id, CancellationToken cancellationToken = default);

    Task<AnnouncementListItemResponse> WithdrawAsync(string id, CancellationToken cancellationToken = default);

    Task<AnnouncementListItemResponse> SetTopAsync(
        string id,
        AnnouncementTopRequest request,
        CancellationToken cancellationToken = default);
}
