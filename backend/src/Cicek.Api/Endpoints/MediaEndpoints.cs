using System.Security.Claims;
using Cicek.Api.ErrorHandling;
using Cicek.Api.RateLimiting;
using Cicek.Application.Media;
using Microsoft.AspNetCore.Mvc;

namespace Cicek.Api.Endpoints;

/// <summary>Image upload (shop members) and public image delivery. Bytes are stored outside Oracle (ADR-0014).</summary>
internal static class MediaEndpoints
{
    /// <summary>Hard transport cap; the handler enforces the configured (smaller) MaxBytes while streaming.</summary>
    private const long RequestCap = 8_000_000;

    public static RouteGroupBuilder MapMediaEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/my/shops/{shopId:guid}/images", async (Guid shopId, HttpRequest request, ClaimsPrincipal user,
                    UploadShopImageHandler handler, CancellationToken ct) =>
                {
                    if (!request.HasFormContentType)
                    {
                        return new Cicek.SharedKernel.Results.Error("media.multipart_required", "Send the image as multipart/form-data.", Cicek.SharedKernel.Results.ErrorKind.Validation).ToProblem();
                    }

                    IFormCollection form;
                    try
                    {
                        form = await request.ReadFormAsync(ct);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException or IOException)
                    {
                        return MediaErrors.Empty.ToProblem();
                    }

                    if (form.Files.Count != 1)
                    {
                        return MediaErrors.Empty.ToProblem();
                    }

                    await using var stream = form.Files[0].OpenReadStream();
                    var result = await handler.HandleAsync(shopId, user.RequiredUserId(), stream, ct);
                    return result.IsSuccess
                        ? Results.Created(result.Value.Url, result.Value)
                        : result.Error.ToProblem();
                })
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Upload)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(RequestCap))
            .WithTags("Seller: Images").WithName("UploadShopImage")
            .WithSummary("Upload one JPEG/PNG/WebP image (type decided from the file bytes). Returns a /media/{id} reference to use in product or shop images.")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadedImageDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        api.MapGet("/media/{id:guid}", async (Guid id, HttpContext http, GetMediaHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(id, ct);
                if (!result.IsSuccess)
                {
                    return result.Error.ToProblem();
                }

                var content = result.Value;
                if (http.Request.Headers.IfNoneMatch.ToString() == content.ETag)
                {
                    await content.Content.DisposeAsync();
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }

                http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                http.Response.Headers["X-Content-Type-Options"] = "nosniff";
                return Results.Stream(content.Content, content.ContentType, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue(content.ETag));
            })
            .AllowAnonymous()
            .WithTags("Media").WithName("GetMedia").WithSummary("A stored image (public, immutable).")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
