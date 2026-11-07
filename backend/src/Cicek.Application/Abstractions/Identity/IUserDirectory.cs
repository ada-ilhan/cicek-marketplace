using Cicek.Application.Common;

namespace Cicek.Application.Abstractions.Identity;

/// <summary>Administrator-facing user row. Deliberately minimal: no password data, phone numbers or tokens.</summary>
public sealed record UserListItemDto(Guid Id, string Email, IReadOnlyList<string> Roles, bool IsLockedOut, DateTimeOffset CreatedAt);

/// <summary>Read-only directory over the identity store (implemented in Infrastructure with ASP.NET Core Identity).</summary>
public interface IUserDirectory
{
    Task<PagedResult<UserListItemDto>> ListAsync(string? emailSearch, string? role, PageRequest page, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}
