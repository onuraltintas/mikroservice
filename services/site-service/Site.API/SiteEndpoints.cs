using Microsoft.AspNetCore.Mvc;
using Site.Infrastructure;

namespace Site.API;

public sealed record NewsletterTokenRequest(string? Token);
public sealed record AnsweredRequest(bool Answered);

public static class SitePolicies
{
    public const string Admin = "SiteAdmin";
}

/// <summary>
/// eduivme.com and maxkampus.com: what visitors read (/api/site, no sign-in) and what the admin edits (/api/site-admin, system admin).
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

        // maxkampus.com (19.11.2026). The site name is part of the address so a third site needs no new routes.
        var sites = site.MapGroup("/sites/{siteKey}").AddEndpointFilter(KnownSite);
        sites.MapGet("/home", async (string siteKey, HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 60);
            return Results.Ok(await content.GetMaxkampusHomeAsync(cancellationToken));
        });
        sites.MapGet("/pages/{slug}", async (string siteKey, string slug, HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 60);
            return await content.GetMaxkampusPageAsync(slug, cancellationToken) is { } page ? Results.Ok(page) : Results.NotFound();
        });
        sites.MapGet("/posts", async (string siteKey, string? category, int? skip, int? take, HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 60);
            return Results.Ok(await content.GetPostsAsync(category, skip ?? 0, take ?? 12, cancellationToken));
        });
        sites.MapGet("/posts/{slug}", async (string siteKey, string slug, HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 60);
            return await content.GetPostAsync(slug, cancellationToken) is { } post ? Results.Ok(post) : Results.NotFound();
        });
        sites.MapGet("/notices", async (string siteKey, string? audience, HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 60);
            return Results.Ok(await content.GetNoticesAsync(audience, cancellationToken));
        });
        sites.MapGet("/sitemap", async (string siteKey, HttpContext http, SiteContent content, CancellationToken cancellationToken) =>
        {
            Cache(http, 300);
            return Results.Ok(await content.GetMaxkampusSitemapAsync(cancellationToken));
        });
        sites.MapPost("/contact", async (string siteKey, ContactRequest request, SiteForms forms, CancellationToken cancellationToken) =>
        {
            await forms.SendContactAsync(siteKey, request, cancellationToken);
            return Results.Accepted();
        });
        sites.MapPost("/newsletter", async (string siteKey, SubscribeRequest request, SiteForms forms, CancellationToken cancellationToken) =>
        {
            await forms.SubscribeAsync(siteKey, request, cancellationToken);
            return Results.Accepted();
        });
        sites.MapPost("/newsletter/confirm", async (string siteKey, NewsletterTokenRequest request, SiteForms forms, CancellationToken cancellationToken) =>
            await forms.ConfirmAsync(request.Token, cancellationToken) ? Results.NoContent()
                : Results.Problem(detail: "Bu onay bağlantısı geçersiz ya da süresi dolmuş. Bülten formundan yeniden abone olabilirsiniz.", statusCode: 404));
        sites.MapPost("/newsletter/unsubscribe", async (string siteKey, NewsletterTokenRequest request, SiteForms forms, CancellationToken cancellationToken) =>
            await forms.UnsubscribeAsync(request.Token, cancellationToken) ? Results.NoContent()
                : Results.Problem(detail: "Bu bağlantı geçersiz. Bültenden ayrılmak için bize iletişim formundan yazabilirsiniz.", statusCode: 404));

        var admin = app.MapGroup("/api/site-admin").RequireAuthorization(SitePolicies.Admin).AddEndpointFilter(Refusals);
        admin.MapGet("/entries", (string? group, string? site, SiteContent content, CancellationToken cancellationToken) =>
            content.GetEntriesAsync(group, cancellationToken, site));
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

        var forms = admin.MapGroup("/sites/{siteKey}").AddEndpointFilter(KnownSite);
        forms.MapGet("/messages", (string siteKey, string? status, int? skip, int? take, SiteForms service, CancellationToken cancellationToken) =>
            service.GetMessagesAsync(siteKey, status, skip ?? 0, take ?? 50, cancellationToken));
        forms.MapPut("/messages/{id:guid}/answered", async (string siteKey, Guid id, AnsweredRequest request, HttpContext http, SiteForms service, CancellationToken cancellationToken) =>
            await service.MarkAnsweredAsync(id, AdminId(http), request.Answered, cancellationToken) ? Results.NoContent() : Results.NotFound());
        forms.MapDelete("/messages/{id:guid}", async (string siteKey, Guid id, SiteForms service, CancellationToken cancellationToken) =>
            await service.DeleteMessageAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
        forms.MapGet("/subscribers", (string siteKey, string? status, int? skip, int? take, SiteForms service, CancellationToken cancellationToken) =>
            service.GetSubscribersAsync(siteKey, status, skip ?? 0, take ?? 100, cancellationToken));
        forms.MapGet("/subscribers.csv", async (string siteKey, HttpContext http, SiteForms service, CancellationToken cancellationToken) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(System.Text.Encoding.UTF8.GetBytes(await service.ExportConfirmedCsvAsync(siteKey, cancellationToken)),
                "text/csv; charset=utf-8", $"{siteKey}-bulten-aboneleri.csv");
        });
        forms.MapDelete("/subscribers/{id:guid}", async (string siteKey, Guid id, SiteForms service, CancellationToken cancellationToken) =>
            await service.DeleteSubscriberAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
    }

    private static Guid AdminId(HttpContext http) =>
        Guid.TryParse(http.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    /// <summary>Only maxkampus.com has these routes; eduivme.com keeps its own (above).</summary>
    private static async ValueTask<object?> KnownSite(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Request.RouteValues["siteKey"] as string == SiteRules.Maxkampus ? await next(context) : Results.NotFound();

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
