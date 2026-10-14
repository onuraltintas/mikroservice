using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public static class LegacyInstitutionImportEndpoints
{
    public static void MapLegacyInstitutionImportEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/workspaces/{workspaceId:guid}/imports/legacy-institution/preview", async (
            Guid workspaceId, LegacyInstitutionImportRequest request, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var workspace = await GetOwnedInstitutionWorkspaceAsync(db, workspaceId, user, ct);
            if (workspace is null) return Results.NotFound();
            if (workspace.Type != WorkspaceType.Institution)
                return Results.Conflict(new { code = "LegacyInstitutionImport.InstitutionWorkspaceRequired" });
            var validationError = Normalize(request, out var import);
            if (validationError is not null) return Results.BadRequest(new { code = validationError });

            var targetName = import!.Name ?? workspace.Name;
            var targetCategory = import.InstitutionCategory ?? workspace.InstitutionCategory;
            return Results.Ok(new LegacyInstitutionImportPreviewResponse(
                CreatePreviewId(workspace.Id, import, workspace.Name, workspace.InstitutionCategory),
                import.SourceSystem,
                import.SelectedFields,
                workspace.Name,
                workspace.InstitutionCategory?.ToString(),
                targetName,
                targetCategory?.ToString()));
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());

        api.MapPost("/workspaces/{workspaceId:guid}/imports/legacy-institution", async (
            Guid workspaceId, LegacyInstitutionImportRequest request, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var workspace = await GetOwnedInstitutionWorkspaceAsync(db, workspaceId, user, ct);
            if (workspace is null) return Results.NotFound();
            if (workspace.Type != WorkspaceType.Institution)
                return Results.Conflict(new { code = "LegacyInstitutionImport.InstitutionWorkspaceRequired" });
            if (string.IsNullOrWhiteSpace(request.PreviewId))
                return Results.BadRequest(new { code = "LegacyInstitutionImport.PreviewRequired" });
            var validationError = Normalize(request, out var import);
            if (validationError is not null) return Results.BadRequest(new { code = validationError });
            var expectedName = request.ExpectedWorkspaceName?.Trim();
            if (string.IsNullOrWhiteSpace(expectedName) || expectedName.Length > 200
                || !TryParseCategory(request.ExpectedWorkspaceCategory, out var expectedCategory))
                return Results.BadRequest(new { code = "LegacyInstitutionImport.InvalidWorkspaceSnapshot" });

            var previewId = CreatePreviewId(workspace.Id, import!, expectedName, expectedCategory);
            if (!string.Equals(request.PreviewId, previewId, StringComparison.Ordinal))
                return Results.Conflict(new { code = "LegacyInstitutionImport.PreviewChanged" });

            var targetName = import!.Name ?? expectedName;
            var targetCategory = import.InstitutionCategory ?? expectedCategory;
            var alreadyAtTarget = workspace.Name == targetName && workspace.InstitutionCategory == targetCategory;
            var snapshotMatches = workspace.Name == expectedName && workspace.InstitutionCategory == expectedCategory;
            if (!snapshotMatches && !alreadyAtTarget)
                return Results.Conflict(new { code = "LegacyInstitutionImport.PreviewChanged" });
            if (alreadyAtTarget)
                return Results.Ok(new LegacyInstitutionImportCommitResponse(previewId, workspace.Name,
                    workspace.InstitutionCategory?.ToString(), true));

            workspace.ImportInstitutionProfile(import.Name, import.InstitutionCategory);
            db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspace.Id, UserId(user), workspace.Id,
                "Workspace.LegacyInstitutionImported"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new LegacyInstitutionImportCommitResponse(previewId, workspace.Name,
                workspace.InstitutionCategory?.ToString(), false));
        });
    }

    private static async Task<Workspace?> GetOwnedInstitutionWorkspaceAsync(CourseManagementDbContext db,
        Guid workspaceId, ClaimsPrincipal user, CancellationToken ct)
    {
        var ownerId = UserId(user);
        return await db.Workspaces.SingleOrDefaultAsync(workspace => workspace.Id == workspaceId
            && workspace.OwnerUserId == ownerId, ct);
    }

    private static string? Normalize(LegacyInstitutionImportRequest request,
        out NormalizedLegacyInstitutionImport? import)
    {
        import = null;
        var sourceSystem = request.SourceSystem?.Trim().ToLowerInvariant();
        if (sourceSystem is not ("coaching" or "speed-reading"))
            return "LegacyInstitutionImport.UnsupportedSource";
        if (request.SourceInstitutionId == Guid.Empty)
            return "LegacyInstitutionImport.InvalidSourceInstitution";
        if (request.SelectedFields is null || request.SelectedFields.Count is < 1 or > 2)
            return "LegacyInstitutionImport.InvalidFields";

        var fields = new List<string>(request.SelectedFields.Count);
        foreach (var field in request.SelectedFields)
        {
            var normalizedField = field?.Trim().ToLowerInvariant();
            if (normalizedField is not ("name" or "type") || fields.Contains(normalizedField, StringComparer.Ordinal))
                return "LegacyInstitutionImport.InvalidFields";
            fields.Add(normalizedField);
        }
        fields.Sort(StringComparer.Ordinal);

        string? name = null;
        if (fields.Contains("name", StringComparer.Ordinal))
        {
            name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
                return "LegacyInstitutionImport.InvalidName";
        }
        else if (request.Name is not null)
        {
            return "LegacyInstitutionImport.UnselectedFieldProvided";
        }

        InstitutionCategory? category = null;
        if (fields.Contains("type", StringComparer.Ordinal))
        {
            if (!TryParseCategory(request.InstitutionCategory, out var parsedCategory) || parsedCategory is null)
                return "LegacyInstitutionImport.InvalidType";
            category = parsedCategory;
        }
        else if (request.InstitutionCategory is not null)
        {
            return "LegacyInstitutionImport.UnselectedFieldProvided";
        }

        import = new NormalizedLegacyInstitutionImport(sourceSystem, request.SourceInstitutionId, fields, name, category);
        return null;
    }

    private static bool TryParseCategory(string? value, out InstitutionCategory? category)
    {
        category = null;
        if (value is null) return true;
        if (!Enum.TryParse<InstitutionCategory>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return false;
        category = parsed;
        return true;
    }

    private static string CreatePreviewId(Guid workspaceId, NormalizedLegacyInstitutionImport import,
        string expectedName, InstitutionCategory? expectedCategory)
    {
        var canonicalInput = JsonSerializer.Serialize(new
        {
            workspaceId,
            import.SourceSystem,
            import.SourceInstitutionId,
            import.SelectedFields,
            import.Name,
            institutionCategory = import.InstitutionCategory?.ToString(),
            expectedWorkspaceName = expectedName,
            expectedWorkspaceCategory = expectedCategory?.ToString()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalInput)));
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);

    private sealed record NormalizedLegacyInstitutionImport(string SourceSystem, Guid SourceInstitutionId,
        IReadOnlyList<string> SelectedFields, string? Name, InstitutionCategory? InstitutionCategory);
}

public sealed record LegacyInstitutionImportRequest(string? SourceSystem, Guid SourceInstitutionId,
    IReadOnlyList<string>? SelectedFields, string? Name, string? InstitutionCategory,
    string? PreviewId = null, string? ExpectedWorkspaceName = null, string? ExpectedWorkspaceCategory = null);

public sealed record LegacyInstitutionImportPreviewResponse(string PreviewId, string SourceSystem,
    IReadOnlyList<string> SelectedFields, string WorkspaceName, string? WorkspaceInstitutionCategory,
    string TargetName, string? TargetInstitutionCategory);

public sealed record LegacyInstitutionImportCommitResponse(string PreviewId, string Name,
    string? InstitutionCategory, bool AlreadyApplied);
