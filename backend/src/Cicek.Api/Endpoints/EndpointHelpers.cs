using System.Security.Claims;
using Cicek.Api.Auth;
using Cicek.Api.ErrorHandling;
using Cicek.Application.Common;
using Cicek.SharedKernel.Results;

namespace Cicek.Api.Endpoints;

internal static class EndpointHelpers
{
    /// <summary>The authenticated user's id. Endpoints using this require authorization, so a missing id is a server bug.</summary>
    public static Guid RequiredUserId(this ClaimsPrincipal user) =>
        user.GetUserId() ?? throw new InvalidOperationException("Authenticated request without a valid subject claim.");

    public static IResult ToHttp<T>(this Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();

    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : result.Error.ToProblem();

    public static PageRequest ToPageRequest(this PagingParameters paging) =>
        new(paging.Page ?? 1, paging.PageSize ?? PageRequest.DefaultPageSize);
}

/// <summary>Common paging query parameters (<c>?page=1&amp;pageSize=20</c>; max page size 50).</summary>
internal sealed record PagingParameters(
    [property: Microsoft.AspNetCore.Mvc.FromQuery(Name = "page")] int? Page,
    [property: Microsoft.AspNetCore.Mvc.FromQuery(Name = "pageSize")] int? PageSize);
