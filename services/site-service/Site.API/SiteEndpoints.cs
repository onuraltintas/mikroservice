using Microsoft.AspNetCore.Mvc;
using Site.Infrastructure;

namespace Site.API;

public static class SitePolicies
{
    public const string Admin = "SiteAdmin";
}

/// <summary>
/// eduivme.com: what visitors read (/api/site, no sign-in) and what the admin edits (/api/site-admin, system admin).
/// </summary>
public static class SiteEndpoints
{
    public static void MapSiteEndpoints(this WebApplication app)
    {
        var site = app.MapGroup("/api/site").AllowAnonymous().AddEndpointFilter(Refusals);
        site.MapGet("/home", async (HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 60);
            return Results.Ok(await content.GetHomeAsync(cancellationToken));
        });
        site.MapGet("/pages/{slug}", async (string slug, HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 60);
            return await content.GetPageAsync(slug, cancellationToken) is { } page ? Results.Ok(page) : Results.NotFound();
        });
        site.MapGet("/sitemap", async (HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 300);
            return Results.Ok(await content.GetSitemapAsync(cancellationToken));
        });
        site.MapGet("/media/{id:guid}", async (Guid id, HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            var file = await content.GetMediaFileAsync(id, cancellationToken);
            if (file is null) return Results.NotFound();
            // An uploaded image never changes under its id.
            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            return Results.File(file.Content, file.ContentType, enableRangeProcessing: false, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{file.Sha256}\""));
        });

        var admin = app.MapGroup("/api/site-admin").RequireAuthorization(SitePolicies.Admin).AddEndpointFilter(Refusals);
        admin.MapGet("/entries", (string? group, SiteContent content, CancellationToken cancellationToken) =>
            content.GetEntriesAsync(group, cancellationToken));
        admin.MapGet("/entries/{id:guid}", async (Guid id, SiteContent content, CancellationToken cancellationToken) =>
            await content.GetEntryAsync(id, cancellationToken) is { } entry ? Results.Ok(entry) : Results.NotFound());
        admin.MapPost("/entries", async (SiteEntryRequest request, SiteContent content, CancellationToken cancellationToken) =>
            Results.Ok(await content.CreateAsync(request, cancellationToken)));
        admin.MapPut("/entries/{id:guid}", async (Guid id, SiteEntryRequest request, SiteContent content, CancellationToken cancellationToken) =>
            Results.Ok(await content.UpdateAsync(id, request, cancellationToken)));
        admin.MapDelete("/entries/{id:guid}", async (Guid id, SiteContent content, CancellationToken cancellationToken) =>
            await content.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
        admin.MapPut("/entries/order", async (SiteOrderRequest request, SiteContent content, CancellationToken cancellationToken) =>
        {
            await content.ReorderAsync(request, cancellationToken);
            return Results.NoContent();
        });
        admin.MapGet("/entries/{id:guid}/revisions", (Guid id, SiteContent content, CancellationToken cancellationToken) =>
            content.GetRevisionsAsync(id, cancellationToken));
        admin.MapPost("/entries/{id:guid}/revisions/{revisionId:guid}/restore", async (Guid id, Guid revisionId, SiteContent content, CancellationToken cancellationToken) =>
            Results.Ok(await content.RestoreAsync(id, revisionId, cancellationToken)));

        admin.MapGet("/media", (SiteContent content, CancellationToken cancellationToken) => content.GetMediaAsync(cancellationToken));
        admin.MapPost("/media", async (IFormFile? file, [FromForm] string? altText, SiteContent content, CancellationToken cancellationToken) =>
        {
            if (file is null) throw new SiteRequestException(400, "Bir görsel seçin.", new Dictionary<string, string[]> { ["file"] = ["Bir görsel seçin."] });
            if (file.Length > SiteRules.MaxMediaBytes)
                throw new SiteRequestException(400, "Görsel en çok 2 MB olabilir.", new Dictionary<string, string[]> { ["file"] = ["Görsel en çok 2 MB olabilir."] });
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            return Results.Ok(await content.UploadMediaAsync(file.FileName, buffer.ToArray(), altText, cancellationToken));
        }).DisableAntiforgery();
        admin.MapDelete("/media/{id:guid}", async (Guid id, SiteContent content, CancellationToken cancellationToken) =>
            await content.DeleteMediaAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
    }

    private static async ValueTask<object?> Refusals(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (SiteRequestException refusal)
        {
            return refusal.Errors.Count > 0
                ? Results.ValidationProblem(refusal.Errors, detail: refusal.Message, statusCode: refusal.Status)
                : Results.Problem(detail: refusal.Message, statusCode: refusal.Status);
        }
    }

    private static void Cache(HttpContext http, int seconds) =>
        http.Response.Headers.CacheControl = $"public, max-age={seconds}";
}
