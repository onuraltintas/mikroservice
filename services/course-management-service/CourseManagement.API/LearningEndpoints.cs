using System.Security.Claims;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CourseManagement.Application;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CourseManagement.API;

public static class LearningEndpoints
{
    public static void MapLearningEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/students", async (Guid workspaceId, int? skip, int? take,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await CanManageWorkspace(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (skip is < 0 || take is < 1 or > 200) return Results.BadRequest(new { code = "Student.InvalidPage" });
            var pageSkip = skip ?? 0;
            var pageSize = take ?? 200;
            var isOwner = await IsOwner(db, workspaceId, UserId(user), ct);
            var students = db.Students.AsNoTracking().Where(student => student.WorkspaceId == workspaceId);
            if (!isOwner)
                students = students.Where(student => student.IsActive && db.Enrollments.Any(enrollment => enrollment.StudentId == student.Id
                    && enrollment.Status == EnrollmentStatus.Active
                    && db.Groups.Any(group => group.Id == enrollment.GroupId && group.TeacherUserId == UserId(user))));
            return Results.Ok(await students.OrderByDescending(student => student.IsActive).ThenBy(student => student.DisplayName).ThenBy(student => student.Id)
                .Skip(pageSkip).Take(pageSize)
                .Select(student => new { student.Id, student.UserId, student.DisplayName, student.Email, student.PhoneNumber, student.IsActive }).ToListAsync(ct));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/students", async (Guid workspaceId, CreateStudentRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.DisplayName)
                || request.DisplayName.Trim().Length > 200 || request.Email?.Trim().Length > 320 || request.PhoneNumber?.Trim().Length > 40)
                return Results.BadRequest(new { code = "Student.InvalidInput" });
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await WorkspaceStudentCapacity.LockWorkspaceAsync(db, workspaceId, ct);
            if (!await WorkspaceStudentCapacity.CanAddAsync(db, workspaceId, 1, ct))
                return Results.Conflict(new { code = "Student.ActiveLimitReached" });
            var student = StudentRecord.Create(request.Id, workspaceId, request.DisplayName, request.Email, request.PhoneNumber, DateTimeOffset.UtcNow);
            db.Students.Add(student);
            AddAudit(db, workspaceId, UserId(user), student.Id, "Student.Created");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/students/{student.Id}", StudentView(student));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/imports/students/preview", async (Guid workspaceId, PreviewStudentCsvRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            try
            {
                var document = StudentCsvImportParser.Parse(request.Csv);
                return Results.Ok(await BuildStudentCsvPreview(workspaceId, request.Csv!, document, db, ct));
            }
            catch (StudentCsvImportParseException exception)
            {
                return Results.BadRequest(new { code = exception.Code });
            }
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());

        api.MapPost("/workspaces/{workspaceId:guid}/imports/students", async (Guid workspaceId, CommitStudentCsvRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.PreviewId)) return Results.BadRequest(new { code = "StudentImport.PreviewRequired" });
            StudentCsvImportPreviewResponse preview;
            try
            {
                var document = StudentCsvImportParser.Parse(request.Csv);
                preview = await BuildStudentCsvPreview(workspaceId, request.Csv!, document, db, ct);
            }
            catch (StudentCsvImportParseException exception)
            {
                return Results.BadRequest(new { code = exception.Code });
            }

            if (!string.Equals(request.PreviewId, preview.PreviewId, StringComparison.Ordinal))
                return Results.Conflict(new { code = "StudentImport.PreviewChanged" });
            if (preview.Rows.Count == 0) return Results.BadRequest(new { code = "StudentImport.EmptyRows" });
            if (preview.InvalidCount > 0 || preview.DuplicateCount > 0)
                return Results.BadRequest(new { code = "StudentImport.ResolveRows", preview });

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await WorkspaceStudentCapacity.LockWorkspaceAsync(db, workspaceId, ct);
            var currentDocument = StudentCsvImportParser.Parse(request.Csv!);
            var currentPreview = await BuildStudentCsvPreview(workspaceId, request.Csv!, currentDocument, db, ct);
            if (!string.Equals(request.PreviewId, currentPreview.PreviewId, StringComparison.Ordinal))
                return Results.Conflict(new { code = "StudentImport.PreviewChanged" });
            if (currentPreview.InvalidCount > 0 || currentPreview.DuplicateCount > 0)
                return Results.BadRequest(new { code = "StudentImport.ResolveRows", preview = currentPreview });
            if (!await WorkspaceStudentCapacity.CanAddAsync(db, workspaceId, currentPreview.ReadyCount, ct))
                return Results.Conflict(new { code = "Student.ActiveLimitReached" });

            foreach (var row in currentPreview.Rows.Where(row => row.Status == "Ready"))
            {
                var studentId = ImportedStudentId(currentPreview.PreviewId, row.RowNumber);
                var student = StudentRecord.Create(studentId, workspaceId, row.DisplayName, row.Email, row.PhoneNumber, DateTimeOffset.UtcNow);
                db.Students.Add(student);
                AddAudit(db, workspaceId, userId, student.Id, "Student.Imported");
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new StudentCsvImportCommitResponse(currentPreview.PreviewId,
                currentPreview.ReadyCount, currentPreview.ImportedCount));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/imports/legacy-students/preview", async (Guid workspaceId,
            LegacyStudentImportRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            var validationError = NormalizeLegacyStudentImport(request, out var import);
            if (validationError is not null) return Results.BadRequest(new { code = validationError });
            var preview = await BuildLegacyStudentImportPreview(workspaceId, import!, db, ct);
            return Results.Ok(preview);
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());

        api.MapPost("/workspaces/{workspaceId:guid}/imports/legacy-students", async (Guid workspaceId,
            LegacyStudentImportRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.PreviewId)) return Results.BadRequest(new { code = "LegacyStudentImport.PreviewRequired" });
            var validationError = NormalizeLegacyStudentImport(request, out var import);
            if (validationError is not null) return Results.BadRequest(new { code = validationError });

            var preview = await BuildLegacyStudentImportPreview(workspaceId, import!, db, ct);
            if (!string.Equals(request.PreviewId, preview.PreviewId, StringComparison.Ordinal))
                return Results.Conflict(new { code = "LegacyStudentImport.PreviewChanged" });
            if (preview.Rows.Count == 0) return Results.BadRequest(new { code = "LegacyStudentImport.EmptyRows" });
            if (preview.InvalidCount > 0 || preview.ConflictCount > 0 || preview.DuplicateCount > 0)
                return Results.BadRequest(new { code = "LegacyStudentImport.ResolveRows", preview });

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await WorkspaceStudentCapacity.LockWorkspaceAsync(db, workspaceId, ct);
            var currentPreview = await BuildLegacyStudentImportPreview(workspaceId, import!, db, ct);
            if (!string.Equals(request.PreviewId, currentPreview.PreviewId, StringComparison.Ordinal))
                return Results.Conflict(new { code = "LegacyStudentImport.PreviewChanged" });
            if (currentPreview.InvalidCount > 0 || currentPreview.ConflictCount > 0 || currentPreview.DuplicateCount > 0)
                return Results.BadRequest(new { code = "LegacyStudentImport.ResolveRows", preview = currentPreview });

            var activeReadyCount = currentPreview.Rows.Count(row => row.Status == "Ready" && row.IsActive);
            if (!await WorkspaceStudentCapacity.CanAddAsync(db, workspaceId, activeReadyCount, ct))
                return Results.Conflict(new { code = "Student.ActiveLimitReached" });

            var selectedFields = string.Join(',', import!.SelectedFields);
            var readyRows = import.Rows.Where((_, index) => currentPreview.Rows[index].Status == "Ready").ToArray();
            foreach (var row in readyRows)
            {
                var student = StudentRecord.Create(Guid.NewGuid(), workspaceId, row.DisplayName, row.Email, null, DateTimeOffset.UtcNow);
                student.SetActive(row.IsActive);
                db.Students.Add(student);
                db.LegacyStudentImports.Add(LegacyStudentImportMapping.Create(Guid.NewGuid(), workspaceId,
                    import.SourceSystem, row.SourceRecordId, student.Id, selectedFields, DateTimeOffset.UtcNow));
                AddAudit(db, workspaceId, userId, student.Id, "Student.ImportedFromLegacy");
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new LegacyStudentImportCommitResponse(currentPreview.PreviewId,
                currentPreview.ReadyCount, currentPreview.ImportedCount));
        });

        api.MapPut("/workspaces/{workspaceId:guid}/students/{studentId:guid}", async (Guid workspaceId, Guid studentId, UpdateStudentRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 200
                || request.Email?.Trim().Length > 320 || request.PhoneNumber?.Trim().Length > 40)
                return Results.BadRequest(new { code = "Student.InvalidInput" });
            var student = await db.Students.SingleOrDefaultAsync(item => item.Id == studentId && item.WorkspaceId == workspaceId, ct);
            if (student is null) return Results.NotFound();
            var isOwner = await IsOwner(db, workspaceId, userId, ct);
            if (!isOwner && (!student.IsActive || !await db.Enrollments.AnyAsync(enrollment => enrollment.StudentId == studentId
                    && enrollment.WorkspaceId == workspaceId && enrollment.Status == EnrollmentStatus.Active
                    && db.Groups.Any(group => group.Id == enrollment.GroupId && group.TeacherUserId == userId), ct)))
                return Results.NotFound();
            if (request.IsActive && !student.IsActive && !isOwner) return Results.NotFound();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (request.IsActive && !student.IsActive)
            {
                await WorkspaceStudentCapacity.LockWorkspaceAsync(db, workspaceId, ct);
                await db.Entry(student).ReloadAsync(ct);
                if (!student.IsActive && !await WorkspaceStudentCapacity.CanAddAsync(db, workspaceId, 1, ct))
                    return Results.Conflict(new { code = "Student.ActiveLimitReached" });
            }

            student.Update(request.DisplayName, request.Email, request.PhoneNumber);
            if (!request.IsActive && student.IsActive)
            {
                student.SetActive(false);
                var activeEnrollments = await db.Enrollments.Where(enrollment => enrollment.StudentId == studentId
                    && enrollment.WorkspaceId == workspaceId && enrollment.Status == EnrollmentStatus.Active).ToListAsync(ct);
                foreach (var enrollment in activeEnrollments) enrollment.Archive();
                if (student.UserId is Guid studentUserId)
                {
                    var membership = await db.Memberships.FindAsync([workspaceId, studentUserId], ct);
                    if (membership is { IsActive: true, Role: WorkspaceRole.Student }) membership.Revoke();
                }
                AddAudit(db, workspaceId, userId, student.Id, "Student.Archived");
            }
            else if (request.IsActive && !student.IsActive)
            {
                student.SetActive(true);
                AddAudit(db, workspaceId, userId, student.Id, "Student.Restored");
            }
            else AddAudit(db, workspaceId, userId, student.Id, "Student.Updated");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(StudentView(student));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/guardians", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var guardians = db.Guardians.AsNoTracking().Where(guardian => guardian.WorkspaceId == workspaceId);
            if (!await IsOwner(db, workspaceId, userId, ct))
                guardians = guardians.Where(guardian => db.StudentGuardianLinks.Any(link => link.GuardianContactId == guardian.Id
                    && link.WorkspaceId == workspaceId && link.IsActive
                    && db.Enrollments.Any(enrollment => enrollment.StudentId == link.StudentId && enrollment.WorkspaceId == workspaceId
                        && enrollment.Status == EnrollmentStatus.Active
                        && db.Groups.Any(group => group.Id == enrollment.GroupId && group.TeacherUserId == userId))));
            return Results.Ok(await guardians
                .OrderBy(guardian => guardian.DisplayName).Take(200)
                .Select(guardian => new { guardian.Id, guardian.DisplayName, guardian.Email, guardian.PhoneNumber }).ToListAsync(ct));
        });

        api.MapPut("/workspaces/{workspaceId:guid}/guardians/{guardianId:guid}", async (Guid workspaceId, Guid guardianId, UpdateGuardianContactRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 200
                || request.Email?.Trim().Length > 320 || request.PhoneNumber?.Trim().Length > 40)
                return Results.BadRequest(new { code = "Guardian.InvalidInput" });
            var guardian = await db.Guardians.SingleOrDefaultAsync(item => item.Id == guardianId && item.WorkspaceId == workspaceId, ct);
            if (guardian is null) return Results.NotFound();
            guardian.Update(request.DisplayName, request.Email, request.PhoneNumber);
            AddAudit(db, workspaceId, userId, guardian.Id, "GuardianContact.Updated");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { guardian.Id, guardian.DisplayName, guardian.Email, guardian.PhoneNumber });
        });

        api.MapGet("/workspaces/{workspaceId:guid}/students/{studentId:guid}/guardians", async (Guid workspaceId, Guid studentId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await CanManageStudent(db, workspaceId, studentId, UserId(user), ct)) return Results.NotFound();
            return Results.Ok(await db.StudentGuardianLinks.AsNoTracking()
                .Join(db.Guardians.AsNoTracking(), link => link.GuardianContactId, guardian => guardian.Id, (link, guardian) => new { link, guardian })
                .Where(item => item.link.WorkspaceId == workspaceId && item.link.StudentId == studentId && item.link.IsActive)
                .OrderBy(item => item.guardian.DisplayName)
                .Select(item => new { item.guardian.Id, item.guardian.DisplayName, item.guardian.Email, item.guardian.PhoneNumber,
                    relationship = item.link.Relationship.ToString() }).ToListAsync(ct));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/students/{studentId:guid}/guardians", async (Guid workspaceId, Guid studentId, LinkGuardianRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.GuardianId == Guid.Empty || !Enum.IsDefined(request.Relationship))
                return Results.BadRequest(new { code = "Guardian.InvalidInput" });
            var student = await db.Students.SingleOrDefaultAsync(item => item.Id == studentId && item.WorkspaceId == workspaceId && item.IsActive, ct);
            if (student is null) return Results.NotFound();

            var guardian = await db.Guardians.SingleOrDefaultAsync(item => item.Id == request.GuardianId && item.WorkspaceId == workspaceId, ct);
            if (guardian is null)
            {
                if (await db.Guardians.AnyAsync(item => item.Id == request.GuardianId, ct)) return Results.Conflict(new { code = "Guardian.WorkspaceConflict" });
                if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 200
                    || request.Email?.Trim().Length > 320 || request.PhoneNumber?.Trim().Length > 40)
                    return Results.BadRequest(new { code = "Guardian.InvalidInput" });
                guardian = GuardianContact.Create(request.GuardianId, workspaceId, request.DisplayName, request.Email, request.PhoneNumber, DateTimeOffset.UtcNow);
                db.Guardians.Add(guardian);
                AddAudit(db, workspaceId, userId, guardian.Id, "GuardianContact.Created");
            }

            var link = await db.StudentGuardianLinks.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.StudentId == studentId
                && item.GuardianContactId == guardian.Id, ct);
            if (link is not null)
            {
                if (!link.IsActive || link.Relationship != request.Relationship)
                {
                    link.SetRelationship(request.Relationship);
                    AddAudit(db, workspaceId, userId, link.Id, "StudentGuardian.Linked");
                    await db.SaveChangesAsync(ct);
                }
                return Results.Ok(new { guardian.Id, guardian.DisplayName, guardian.Email, guardian.PhoneNumber, relationship = link.Relationship.ToString() });
            }

            link = StudentGuardianLink.Create(Guid.NewGuid(), workspaceId, studentId, guardian.Id, request.Relationship, DateTimeOffset.UtcNow);
            db.StudentGuardianLinks.Add(link);
            AddAudit(db, workspaceId, userId, link.Id, "StudentGuardian.Linked");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/students/{studentId}/guardians",
                new { guardian.Id, guardian.DisplayName, guardian.Email, guardian.PhoneNumber, relationship = link.Relationship.ToString() });
        });

        api.MapDelete("/workspaces/{workspaceId:guid}/students/{studentId:guid}/guardians/{guardianId:guid}", async (Guid workspaceId, Guid studentId, Guid guardianId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            var link = await db.StudentGuardianLinks.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId
                && item.StudentId == studentId && item.GuardianContactId == guardianId, ct);
            if (link is null) return Results.NotFound();
            if (link.IsActive)
            {
                link.Deactivate();
                AddAudit(db, workspaceId, userId, link.Id, "StudentGuardian.Unlinked");
                await db.SaveChangesAsync(ct);
            }
            return Results.NoContent();
        });

        api.MapGet("/workspaces/{workspaceId:guid}/courses", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await CanManageWorkspace(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            var courses = db.Courses.AsNoTracking().Where(course => course.WorkspaceId == workspaceId);
            if (!await IsOwner(db, workspaceId, UserId(user), ct))
                courses = courses.Where(course => db.Groups.Any(group => group.CourseId == course.Id && group.TeacherUserId == UserId(user)));
            return Results.Ok(await courses.OrderBy(course => course.Name).Take(200)
                .Select(course => new { course.Id, course.Name, course.Subject }).ToListAsync(ct));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/courses", async (Guid workspaceId, CreateCourseRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160
                || string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length > 100)
                return Results.BadRequest(new { code = "Course.InvalidInput" });
            var course = Course.Create(request.Id, workspaceId, request.Name, request.Subject, DateTimeOffset.UtcNow);
            db.Courses.Add(course);
            AddAudit(db, workspaceId, UserId(user), course.Id, "Course.Created");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/courses/{course.Id}", new { course.Id, course.Name, course.Subject });
        });

        api.MapGet("/workspaces/{workspaceId:guid}/branches", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await CanManageWorkspace(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            return Results.Ok(await db.Branches.AsNoTracking().Where(branch => branch.WorkspaceId == workspaceId)
                .OrderBy(branch => branch.Name).Select(branch => new { branch.Id, branch.Name }).Take(200).ToListAsync(ct));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/branches", async (Guid workspaceId, CreateBranchRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
                return Results.BadRequest(new { code = "Branch.InvalidInput" });
            if (await db.Branches.AnyAsync(branch => branch.WorkspaceId == workspaceId && branch.Name == request.Name.Trim(), ct))
                return Results.Conflict(new { code = "Branch.DuplicateName" });
            var branch = CourseBranch.Create(request.Id, workspaceId, request.Name, DateTimeOffset.UtcNow);
            db.Branches.Add(branch);
            AddAudit(db, workspaceId, userId, branch.Id, "CourseBranch.Created");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/branches/{branch.Id}", new { branch.Id, branch.Name });
        });

        api.MapGet("/workspaces/{workspaceId:guid}/rooms", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await CanManageWorkspace(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            var rooms = await (from room in db.Rooms.AsNoTracking()
                join branch in db.Branches.AsNoTracking() on room.BranchId equals branch.Id
                where room.WorkspaceId == workspaceId
                orderby branch.Name, room.Name
                select new { room.Id, room.BranchId, branchName = branch.Name, room.Name, room.Capacity })
                .Take(500).ToListAsync(ct);
            return Results.Ok(rooms);
        });

        api.MapPost("/workspaces/{workspaceId:guid}/branches/{branchId:guid}/rooms", async (Guid workspaceId, Guid branchId,
            CreateRoomRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120
                || request.Capacity is < 1)
                return Results.BadRequest(new { code = "Room.InvalidInput" });
            if (!await db.Branches.AnyAsync(branch => branch.Id == branchId && branch.WorkspaceId == workspaceId, ct))
                return Results.NotFound();
            if (await db.Rooms.AnyAsync(room => room.BranchId == branchId && room.Name == request.Name.Trim(), ct))
                return Results.Conflict(new { code = "Room.DuplicateName" });
            var room = CourseRoom.Create(request.Id, workspaceId, branchId, request.Name, request.Capacity, DateTimeOffset.UtcNow);
            db.Rooms.Add(room);
            AddAudit(db, workspaceId, userId, room.Id, "CourseRoom.Created");
            await db.SaveChangesAsync(ct);
            var branchName = await db.Branches.Where(branch => branch.Id == branchId).Select(branch => branch.Name).SingleAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/rooms/{room.Id}",
                new { room.Id, room.BranchId, branchName, room.Name, room.Capacity });
        });

        api.MapGet("/workspaces/{workspaceId:guid}/groups", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await CanManageWorkspace(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            var groups = db.Groups.AsNoTracking().Where(group => group.WorkspaceId == workspaceId);
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) groups = groups.Where(group => group.TeacherUserId == UserId(user));
            return Results.Ok(await groups.Join(db.Courses.AsNoTracking(), group => group.CourseId, course => course.Id,
                    (group, course) => new { group.Id, group.CourseId, courseName = course.Name, group.Name, group.TeacherUserId })
                .OrderBy(group => group.Name).Take(200).ToListAsync(ct));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/groups", async (Guid workspaceId, CreateGroupRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.TeacherUserId != userId && !await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || request.CourseId == Guid.Empty || request.TeacherUserId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
                return Results.BadRequest(new { code = "Group.InvalidInput" });
            if (!await db.Courses.AnyAsync(course => course.Id == request.CourseId && course.WorkspaceId == workspaceId, ct))
                return Results.NotFound();
            if (request.TeacherUserId != userId && !await IsActiveTeacher(db, workspaceId, request.TeacherUserId, ct))
                return Results.BadRequest(new { code = "Group.TeacherNotInWorkspace" });
            var group = CourseGroup.Create(request.Id, workspaceId, request.CourseId, request.TeacherUserId, request.Name, DateTimeOffset.UtcNow);
            db.Groups.Add(group);
            AddAudit(db, workspaceId, userId, group.Id, "Group.Created");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/groups/{group.Id}", new { group.Id, group.CourseId, group.Name, group.TeacherUserId });
        });

        api.MapPost("/workspaces/{workspaceId:guid}/groups/{groupId:guid}/students/{studentId:guid}", async (Guid workspaceId, Guid groupId, Guid studentId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            if (!await db.Groups.AnyAsync(group => group.Id == groupId && group.WorkspaceId == workspaceId
                    && (group.TeacherUserId == userId || db.Workspaces.Any(workspace => workspace.Id == workspaceId && workspace.OwnerUserId == userId)), ct))
                return Results.NotFound();
            if (!await db.Students.AnyAsync(student => student.Id == studentId && student.WorkspaceId == workspaceId && student.IsActive, ct))
                return Results.NotFound();
            var existing = await db.Enrollments.SingleOrDefaultAsync(item => item.GroupId == groupId && item.StudentId == studentId, ct);
            if (existing is not null)
            {
                if (existing.Status == EnrollmentStatus.Active) return Results.Ok(new { existing.Id });
                existing.Reactivate();
                AddAudit(db, workspaceId, userId, existing.Id, "Enrollment.Restored");
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { existing.Id });
            }
            var enrollment = StudentGroupEnrollment.Create(Guid.NewGuid(), workspaceId, groupId, studentId, DateTimeOffset.UtcNow);
            db.Enrollments.Add(enrollment);
            AddAudit(db, workspaceId, userId, enrollment.Id, "Enrollment.Created");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/groups/{groupId}/students/{studentId}", new { enrollment.Id });
        });

        api.MapGet("/workspaces/{workspaceId:guid}/lessons", async (Guid workspaceId, DateTimeOffset? from, DateTimeOffset? to,
            int? skip, int? take,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var rangeStart = (from ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var rangeEnd = (to ?? rangeStart.AddDays(30)).ToUniversalTime();
            if (rangeEnd <= rangeStart || rangeEnd - rangeStart > TimeSpan.FromDays(180))
                return Results.BadRequest(new { code = "Lesson.InvalidRange" });
            if (skip is < 0 || take is < 1 or > 500) return Results.BadRequest(new { code = "Lesson.InvalidPage" });
            var pageSkip = skip ?? 0;
            var pageSize = take ?? 500;

            var owner = await IsOwner(db, workspaceId, userId, ct);
            var lessons = await (from lesson in db.LessonSessions.AsNoTracking()
                join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                join workspace in db.Workspaces.AsNoTracking() on lesson.WorkspaceId equals workspace.Id
                join seriesJoin in db.LessonSeries.AsNoTracking() on lesson.SeriesId equals (Guid?)seriesJoin.Id into seriesJoins
                from lessonSeries in seriesJoins.DefaultIfEmpty()
                join groupJoin in db.Groups.AsNoTracking() on lesson.GroupId equals groupJoin.Id into groupJoins
                from courseGroup in groupJoins.DefaultIfEmpty()
                join roomJoin in db.Rooms.AsNoTracking() on lesson.RoomId equals (Guid?)roomJoin.Id into roomJoins
                from courseRoom in roomJoins.DefaultIfEmpty()
                join branchJoin in db.Branches.AsNoTracking() on (courseRoom == null ? null : (Guid?)courseRoom.BranchId) equals (Guid?)branchJoin.Id into branchJoins
                from courseBranch in branchJoins.DefaultIfEmpty()
                where lesson.WorkspaceId == workspaceId && lesson.StartsAt < rangeEnd && lesson.EndsAt > rangeStart
                    && (owner || lesson.TeacherUserId == userId)
                orderby lesson.StartsAt, lesson.Id
                select new { lesson.Id, lesson.SeriesId, TimeZoneId = lessonSeries == null ? null : lessonSeries.TimeZoneId,
                    lesson.CourseId, CourseName = course.Name, course.Subject, workspace.Name,
                    lesson.GroupId, GroupName = courseGroup == null ? null : courseGroup.Name,
                    lesson.RoomId, RoomName = courseRoom == null ? null : courseRoom.Name,
                    BranchName = courseBranch == null ? null : courseBranch.Name, lesson.TeacherUserId,
                    lesson.StartsAt, lesson.EndsAt, lesson.Status })
                .Skip(pageSkip).Take(pageSize).ToListAsync(ct);
            var lessonIds = lessons.Select(lesson => lesson.Id).ToList();
            var participants = await (from participant in db.LessonParticipants.AsNoTracking()
                join student in db.Students.AsNoTracking() on participant.StudentId equals student.Id
                join attendanceJoin in db.AttendanceRecords.AsNoTracking()
                    on new { participant.LessonSessionId, participant.StudentId }
                    equals new { attendanceJoin.LessonSessionId, attendanceJoin.StudentId } into attendanceJoins
                from attendance in attendanceJoins.DefaultIfEmpty()
                where participant.WorkspaceId == workspaceId && lessonIds.Contains(participant.LessonSessionId)
                orderby student.DisplayName
                select new { participant.LessonSessionId, studentId = student.Id, student.DisplayName,
                    attendanceStatus = attendance == null ? null : attendance.Status.ToString(), participant.IsActive }).ToListAsync(ct);
            var participantsByLesson = participants.GroupBy(participant => participant.LessonSessionId)
                .ToDictionary(grouping => grouping.Key, grouping => grouping
                    .Select(participant => new LessonStudentView(participant.studentId, participant.DisplayName, participant.attendanceStatus, participant.IsActive)).ToArray());
            return Results.Ok(lessons.Select(lesson => new
            {
                lesson.Id, lesson.SeriesId, lesson.TimeZoneId, lesson.CourseId, lesson.CourseName, lesson.Subject, workspaceName = lesson.Name,
                lesson.GroupId, lesson.GroupName, lesson.TeacherUserId, lesson.StartsAt, lesson.EndsAt,
                lesson.RoomId, lesson.RoomName, lesson.BranchName,
                status = lesson.Status.ToString(),
                students = participantsByLesson.GetValueOrDefault(lesson.Id) ?? Array.Empty<LessonStudentView>()
            }));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/makeup-credits", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var isOwner = await IsOwner(db, workspaceId, userId, ct);
            var now = DateTimeOffset.UtcNow;
            var rows = await (from credit in db.MakeupCredits.AsNoTracking()
                join student in db.Students.AsNoTracking() on credit.StudentId equals student.Id
                join sourceLesson in db.LessonSessions.AsNoTracking() on credit.SourceLessonId equals sourceLesson.Id
                where credit.WorkspaceId == workspaceId && (isOwner || sourceLesson.TeacherUserId == userId)
                orderby credit.ExpiresAt, student.DisplayName
                select new { credit.Id, credit.StudentId, student.DisplayName, credit.SourceLessonId,
                    sourceLesson.StartsAt, sourceLesson.EndsAt, sourceCourseId = sourceLesson.CourseId,
                    sourceTeacherUserId = sourceLesson.TeacherUserId, credit.MakeupLessonId, credit.Reason,
                    credit.Status, credit.GrantedAt, credit.ExpiresAt })
                .Take(200).ToListAsync(ct);
            return Results.Ok(rows.Select(row => new { row.Id, row.StudentId, studentName = row.DisplayName,
                row.SourceLessonId, sourceLessonStartsAt = row.StartsAt, sourceCourseId = row.sourceCourseId,
                sourceTeacherUserId = row.sourceTeacherUserId,
                durationMinutes = Math.Max(1, (int)(row.EndsAt - row.StartsAt).TotalMinutes),
                row.MakeupLessonId, reason = row.Reason.ToString(),
                status = ((row.Status is MakeupCreditStatus.PendingTeacherApproval or MakeupCreditStatus.Available)
                    && row.ExpiresAt <= now ? MakeupCreditStatus.Expired : row.Status).ToString(),
                row.GrantedAt, row.ExpiresAt }));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/makeup-credits/{creditId:guid}/approve", async (Guid workspaceId, Guid creditId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var credit = await db.MakeupCredits.SingleOrDefaultAsync(item => item.Id == creditId && item.WorkspaceId == workspaceId, ct);
                if (credit is null) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                var sourceTeacherId = await db.LessonSessions.Where(lesson => lesson.Id == credit.SourceLessonId
                    && lesson.WorkspaceId == workspaceId).Select(lesson => (Guid?)lesson.TeacherUserId).SingleOrDefaultAsync(ct);
                if (!isOwner && (sourceTeacherId != userId || !await IsActiveTeacher(db, workspaceId, userId, ct)))
                    return Results.NotFound();
                var now = DateTimeOffset.UtcNow;
                if (credit.ExpiresAt <= now) return Results.Conflict(new { code = "MakeupCredit.Expired" });
                if (credit.Status != MakeupCreditStatus.PendingTeacherApproval)
                    return Results.Conflict(new { code = "MakeupCredit.NotPending" });
                credit.Approve(now);
                AddAudit(db, workspaceId, userId, credit.Id, "MakeupCredit.Approved");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new { credit.Id, status = credit.Status.ToString(), credit.ExpiresAt });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "MakeupCredit.ConcurrentUpdate" });
            }
        });

        api.MapGet("/workspaces/{workspaceId:guid}/teacher-time-off", async (Guid workspaceId, DateTimeOffset? from, DateTimeOffset? to,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            var rangeStart = (from ?? now.AddDays(-7)).ToUniversalTime();
            var rangeEnd = (to ?? now.AddDays(90)).ToUniversalTime();
            if (rangeEnd <= rangeStart || rangeEnd - rangeStart > TimeSpan.FromDays(180))
                return Results.BadRequest(new { code = "Availability.InvalidRange" });

            var isOwner = await IsOwner(db, workspaceId, userId, ct);
            var blocks = db.TeacherTimeOffs.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.StartsAt < rangeEnd && item.EndsAt > rangeStart);
            if (!isOwner) blocks = blocks.Where(item => item.TeacherUserId == userId);
            return Results.Ok(await blocks.OrderBy(item => item.StartsAt).Take(500)
                .Select(item => new { item.Id, item.TeacherUserId, item.StartsAt, item.EndsAt, item.Reason, item.CreatedAt })
                .ToListAsync(ct));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/teacher-time-off", async (Guid workspaceId, CreateTeacherTimeOffRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            var startsAt = request.StartsAt.ToUniversalTime();
            var endsAt = request.EndsAt.ToUniversalTime();
            if (request.Id == Guid.Empty || request.TeacherUserId == Guid.Empty || startsAt <= now || endsAt <= startsAt
                || endsAt - startsAt > TimeSpan.FromDays(90) || request.Reason?.Trim().Length > 300)
                return Results.BadRequest(new { code = "Availability.InvalidTime" });

            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && request.TeacherUserId != userId) return Results.NotFound();
                var ownerUserId = await db.Workspaces.Where(workspace => workspace.Id == workspaceId)
                    .Select(workspace => workspace.OwnerUserId).SingleAsync(ct);
                if (request.TeacherUserId != ownerUserId
                    && !await IsActiveTeacher(db, workspaceId, request.TeacherUserId, ct))
                    return Results.BadRequest(new { code = "Availability.TeacherNotInWorkspace" });

                if (await db.LessonSessions.AnyAsync(lesson => lesson.WorkspaceId == workspaceId
                        && lesson.TeacherUserId == request.TeacherUserId && lesson.Status == LessonSessionStatus.Scheduled
                        && lesson.StartsAt < endsAt && startsAt < lesson.EndsAt, ct))
                    return Results.Conflict(new { code = "Availability.LessonConflict" });
                if (await db.TeacherTimeOffs.AnyAsync(item => item.WorkspaceId == workspaceId
                        && item.TeacherUserId == request.TeacherUserId && item.StartsAt < endsAt && startsAt < item.EndsAt, ct))
                    return Results.Conflict(new { code = "Availability.BlockConflict" });

                var block = TeacherTimeOff.Create(request.Id, workspaceId, request.TeacherUserId, startsAt, endsAt,
                    request.Reason, userId, now);
                db.TeacherTimeOffs.Add(block);
                AddAudit(db, workspaceId, userId, block.Id, "TeacherTimeOff.Created");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/teacher-time-off/{block.Id}",
                    new { block.Id, block.TeacherUserId, block.StartsAt, block.EndsAt, block.Reason, block.CreatedAt });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Availability.ConcurrentConflict" });
            }
        });

        api.MapDelete("/workspaces/{workspaceId:guid}/teacher-time-off/{blockId:guid}", async (Guid workspaceId, Guid blockId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var block = await db.TeacherTimeOffs.SingleOrDefaultAsync(item => item.Id == blockId && item.WorkspaceId == workspaceId, ct);
            if (block is null) return Results.NotFound();
            if (block.TeacherUserId != userId && !await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            db.TeacherTimeOffs.Remove(block);
            AddAudit(db, workspaceId, userId, block.Id, "TeacherTimeOff.Deleted");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapGet("/workspaces/{workspaceId:guid}/teacher-availability", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var isOwner = await IsOwner(db, workspaceId, userId, ct);
            var windows = db.TeacherAvailabilityWindows.AsNoTracking().Where(item => item.WorkspaceId == workspaceId);
            if (!isOwner) windows = windows.Where(item => item.TeacherUserId == userId);
            var result = await windows.OrderBy(item => item.TeacherUserId).ThenBy(item => item.Weekday).ThenBy(item => item.StartTime)
                .Take(500).ToListAsync(ct);
            return Results.Ok(result.Select(item => new
            {
                item.Id, item.TeacherUserId, weekday = item.Weekday,
                startTime = item.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                endTime = item.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture), item.TimeZoneId
            }));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/teacher-availability", async (Guid workspaceId, CreateTeacherAvailabilityWindowRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || request.TeacherUserId == Guid.Empty || request.Weekday is < 0 or > 6
                || string.IsNullOrWhiteSpace(request.TimeZoneId)
                || !TimeOnly.TryParseExact(request.StartTime, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime)
                || !TimeOnly.TryParseExact(request.EndTime, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime)
                || endTime <= startTime)
                return Results.BadRequest(new { code = "Availability.InvalidWindow" });

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                return Results.BadRequest(new { code = "Availability.InvalidTimeZone" });
            }
            catch (InvalidTimeZoneException)
            {
                return Results.BadRequest(new { code = "Availability.InvalidTimeZone" });
            }

            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && request.TeacherUserId != userId) return Results.NotFound();
                var ownerUserId = await db.Workspaces.Where(workspace => workspace.Id == workspaceId)
                    .Select(workspace => workspace.OwnerUserId).SingleAsync(ct);
                if (request.TeacherUserId != ownerUserId
                    && !await IsActiveTeacher(db, workspaceId, request.TeacherUserId, ct))
                    return Results.BadRequest(new { code = "Availability.TeacherNotInWorkspace" });
                if (await db.TeacherAvailabilityWindows.AnyAsync(item => item.WorkspaceId == workspaceId
                        && item.TeacherUserId == request.TeacherUserId && item.Weekday == request.Weekday
                        && item.StartTime == startTime && item.EndTime == endTime && item.TimeZoneId == timeZone.Id, ct))
                    return Results.Conflict(new { code = "Availability.DuplicateWindow" });

                var window = TeacherAvailabilityWindow.Create(request.Id, workspaceId, request.TeacherUserId, request.Weekday,
                    startTime, endTime, timeZone.Id, userId, DateTimeOffset.UtcNow);
                db.TeacherAvailabilityWindows.Add(window);
                AddAudit(db, workspaceId, userId, window.Id, "TeacherAvailability.Created");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/teacher-availability/{window.Id}",
                    new { window.Id, window.TeacherUserId, weekday = window.Weekday, window.StartTime, window.EndTime, window.TimeZoneId });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Availability.ConcurrentConflict" });
            }
        });

        api.MapDelete("/workspaces/{workspaceId:guid}/teacher-availability/{windowId:guid}", async (Guid workspaceId, Guid windowId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var window = await db.TeacherAvailabilityWindows.SingleOrDefaultAsync(item => item.Id == windowId
                && item.WorkspaceId == workspaceId, ct);
            if (window is null) return Results.NotFound();
            if (window.TeacherUserId != userId && !await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            db.TeacherAvailabilityWindows.Remove(window);
            AddAudit(db, workspaceId, userId, window.Id, "TeacherAvailability.Deleted");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapGet("/workspaces/{workspaceId:guid}/lesson-series", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            var isOwner = await IsOwner(db, workspaceId, userId, ct);
            var now = DateTimeOffset.UtcNow;
            var series = await (from item in db.LessonSeries.AsNoTracking()
                join course in db.Courses.AsNoTracking() on item.CourseId equals course.Id
                join groupJoin in db.Groups.AsNoTracking() on item.GroupId equals (Guid?)groupJoin.Id into groupJoins
                from courseGroup in groupJoins.DefaultIfEmpty()
                join roomJoin in db.Rooms.AsNoTracking() on item.RoomId equals (Guid?)roomJoin.Id into roomJoins
                from courseRoom in roomJoins.DefaultIfEmpty()
                join branchJoin in db.Branches.AsNoTracking() on (courseRoom == null ? null : (Guid?)courseRoom.BranchId) equals (Guid?)branchJoin.Id into branchJoins
                from courseBranch in branchJoins.DefaultIfEmpty()
                where item.WorkspaceId == workspaceId && (isOwner || item.TeacherUserId == userId)
                    && db.LessonSessions.Any(lesson => lesson.WorkspaceId == workspaceId && lesson.SeriesId == item.Id
                        && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now)
                orderby item.StartsOn, item.StartTime
                select new { item.Id, item.CourseId, courseName = course.Name, item.GroupId,
                    groupName = courseGroup == null ? null : courseGroup.Name,
                    item.RoomId, roomName = courseRoom == null ? null : courseRoom.Name,
                    branchName = courseBranch == null ? null : courseBranch.Name, item.TeacherUserId, item.TimeZoneId,
                    item.StartsOn, item.EndsOn, item.StartTime, item.DurationMinutes, item.DaysOfWeekMask }).Take(200).ToListAsync(ct);
            var seriesIds = series.Select(item => item.Id).ToList();
            var occurrences = await db.LessonSessions.AsNoTracking().Where(lesson => lesson.WorkspaceId == workspaceId
                    && lesson.SeriesId != null && seriesIds.Contains(lesson.SeriesId.Value)
                    && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now)
                .GroupBy(lesson => lesson.SeriesId!.Value)
                .Select(grouping => new { SeriesId = grouping.Key, NextStartsAt = grouping.Min(lesson => lesson.StartsAt), RemainingSessions = grouping.Count() })
                .ToListAsync(ct);
            var occurrenceBySeries = occurrences.ToDictionary(item => item.SeriesId);
            return Results.Ok(series.Where(item => occurrenceBySeries.ContainsKey(item.Id)).Select(item => new
            {
                item.Id, item.CourseId, item.courseName, item.GroupId, item.groupName, item.TeacherUserId,
                item.RoomId, item.roomName, item.branchName,
                item.TimeZoneId, item.StartsOn, item.EndsOn, item.StartTime, item.DurationMinutes, item.DaysOfWeekMask,
                nextStartsAt = occurrenceBySeries[item.Id].NextStartsAt,
                remainingSessions = occurrenceBySeries[item.Id].RemainingSessions
            }));
        });

        api.MapPut("/workspaces/{workspaceId:guid}/lesson-series/{seriesId:guid}/schedule", async (
            Guid workspaceId, Guid seriesId, UpdateLessonSeriesScheduleRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.RoomId == Guid.Empty
                || !DateOnly.TryParseExact(request.EndsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endsOn)
                || !TimeOnly.TryParseExact(request.StartTime, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime)
                || request.DurationMinutes is < 1 or > 480 || request.DaysOfWeekMask is < 1 or > 127)
                return Results.BadRequest(new { code = "LessonSeries.InvalidSchedule" });

            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
                var series = await db.LessonSeries.SingleOrDefaultAsync(item => item.Id == seriesId
                    && item.WorkspaceId == workspaceId, ct);
                if (series is null) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && series.TeacherUserId != userId) return Results.NotFound();
                if (endsOn < series.StartsOn || endsOn.DayNumber - series.StartsOn.DayNumber > 180)
                    return Results.BadRequest(new { code = "LessonSeries.InvalidSchedule" });
                var ownerUserId = await db.Workspaces.Where(item => item.Id == workspaceId)
                    .Select(item => item.OwnerUserId).SingleAsync(ct);
                if (series.TeacherUserId != ownerUserId
                    && !await IsActiveTeacher(db, workspaceId, series.TeacherUserId, ct))
                    return Results.BadRequest(new { code = "Lesson.TeacherNotInWorkspace" });

                var now = DateTimeOffset.UtcNow;
                var futureSessions = await db.LessonSessions.Where(lesson => lesson.WorkspaceId == workspaceId
                    && lesson.SeriesId == seriesId && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now)
                    .OrderBy(lesson => lesson.StartsAt).ToListAsync(ct);
                if (futureSessions.Count == 0)
                    return Results.Conflict(new { code = "LessonSeries.NoFutureSessions" });
                var futureSessionIds = futureSessions.Select(lesson => lesson.Id).ToList();
                if (await db.AttendanceRecords.AnyAsync(attendance => attendance.WorkspaceId == workspaceId
                        && futureSessionIds.Contains(attendance.LessonSessionId), ct))
                    return Results.Conflict(new { code = "LessonSeries.AttendanceStarted" });

                var participantStudentIds = await db.LessonParticipants.AsNoTracking()
                    .Where(participant => participant.WorkspaceId == workspaceId
                        && participant.IsActive
                        && participant.LessonSessionId == futureSessions[0].Id)
                    .Select(participant => participant.StudentId).ToListAsync(ct);
                if (participantStudentIds.Count == 0)
                    return Results.Conflict(new { code = "LessonSeries.NoParticipants" });
                var studentUserIds = await GetLinkedStudentUserIds(db, workspaceId, participantStudentIds, ct);

                CourseRoom? room = null;
                if (request.RoomId is Guid roomId)
                {
                    room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId && item.WorkspaceId == workspaceId, ct);
                    if (room is null) return Results.NotFound();
                    if (room.Capacity is int capacity && participantStudentIds.Count > capacity)
                        return Results.Conflict(new { code = "Room.CapacityExceeded" });
                }

                LessonSeries updatedSchedule;
                IReadOnlyList<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)> occurrences;
                TimeZoneInfo timeZone;
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(series.TimeZoneId);
                    var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
                    var scheduleStartsOn = series.StartsOn < localToday ? localToday : series.StartsOn;
                    if (scheduleStartsOn > endsOn)
                        return Results.BadRequest(new { code = "LessonSeries.NoFutureOccurrences" });
                    updatedSchedule = LessonSeries.Create(series.Id, series.WorkspaceId, series.CourseId,
                        series.GroupId, series.StudentId, series.TeacherUserId, series.TimeZoneId,
                        scheduleStartsOn, endsOn, startTime, request.DurationMinutes, request.DaysOfWeekMask,
                        now, request.RoomId);
                    occurrences = updatedSchedule.Expand(timeZone).Where(item => item.StartsAt > now).ToArray();
                }
                catch (TimeZoneNotFoundException)
                {
                    return Results.BadRequest(new { code = "LessonSeries.InvalidTimeZone" });
                }
                catch (InvalidTimeZoneException)
                {
                    return Results.BadRequest(new { code = "LessonSeries.InvalidTimeZone" });
                }
                catch (InvalidOperationException)
                {
                    return Results.BadRequest(new { code = "LessonSeries.InvalidLocalTime" });
                }
                catch (ArgumentException)
                {
                    return Results.BadRequest(new { code = "LessonSeries.InvalidSchedule" });
                }
                if (occurrences.Count == 0)
                    return Results.BadRequest(new { code = "LessonSeries.NoFutureOccurrences" });

                var earliestStart = occurrences.Min(item => item.StartsAt);
                var latestEnd = occurrences.Max(item => item.EndsAt);
                var teacherSessions = await db.LessonSessions.AsNoTracking()
                    .Where(existing => existing.WorkspaceId == workspaceId && existing.Status == LessonSessionStatus.Scheduled
                        && existing.TeacherUserId == series.TeacherUserId && !futureSessionIds.Contains(existing.Id)
                        && existing.StartsAt < latestEnd && existing.EndsAt > earliestStart)
                    .Select(existing => new { existing.StartsAt, existing.EndsAt }).ToListAsync(ct);
                if (occurrences.Any(occurrence => teacherSessions.Any(existing => existing.StartsAt < occurrence.EndsAt
                    && occurrence.StartsAt < existing.EndsAt)))
                    return Results.Conflict(new { code = "Lesson.TeacherConflict" });

                var teacherTimeOff = await db.TeacherTimeOffs.AsNoTracking()
                    .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == series.TeacherUserId
                        && item.StartsAt < latestEnd && item.EndsAt > earliestStart)
                    .Select(item => new { item.StartsAt, item.EndsAt }).ToListAsync(ct);
                if (occurrences.Any(occurrence => teacherTimeOff.Any(item => item.StartsAt < occurrence.EndsAt
                    && occurrence.StartsAt < item.EndsAt)))
                    return Results.Conflict(new { code = "Availability.TimeOffConflict" });

                var availabilityWindows = await db.TeacherAvailabilityWindows.AsNoTracking()
                    .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == series.TeacherUserId)
                    .ToListAsync(ct);
                if (occurrences.Any(occurrence => !FitsTeacherAvailability(occurrence, availabilityWindows)))
                    return Results.Conflict(new { code = "Availability.OutsideWorkingHours" });

                var studentSessions = await (from participant in db.LessonParticipants.AsNoTracking()
                    join existing in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals existing.Id
                    where participant.WorkspaceId == workspaceId && participantStudentIds.Contains(participant.StudentId)
                        && participant.IsActive
                        && existing.WorkspaceId == workspaceId && existing.Status == LessonSessionStatus.Scheduled
                        && !futureSessionIds.Contains(existing.Id)
                        && existing.StartsAt < latestEnd && existing.EndsAt > earliestStart
                    select new { existing.StartsAt, existing.EndsAt }).ToListAsync(ct);
                if (occurrences.Any(occurrence => studentSessions.Any(existing => existing.StartsAt < occurrence.EndsAt
                    && occurrence.StartsAt < existing.EndsAt)))
                    return Results.Conflict(new { code = "Lesson.StudentConflict" });

                if (room is not null)
                {
                    var roomSessions = await db.LessonSessions.AsNoTracking()
                        .Where(existing => existing.WorkspaceId == workspaceId && existing.RoomId == room.Id
                            && existing.Status == LessonSessionStatus.Scheduled && !futureSessionIds.Contains(existing.Id)
                            && existing.StartsAt < latestEnd && existing.EndsAt > earliestStart)
                        .Select(existing => new { existing.StartsAt, existing.EndsAt }).ToListAsync(ct);
                    if (occurrences.Any(occurrence => roomSessions.Any(existing => existing.StartsAt < occurrence.EndsAt
                        && occurrence.StartsAt < existing.EndsAt)))
                        return Results.Conflict(new { code = "Room.Conflict" });
                }

                series.UpdateSchedule(endsOn, startTime, request.DurationMinutes, request.DaysOfWeekMask, request.RoomId);
                var existingByStart = futureSessions.ToDictionary(lesson => lesson.StartsAt);
                var occurrenceStarts = occurrences.Select(item => item.StartsAt).ToHashSet();
                var sessionsUpdated = 0;
                var sessionsCancelled = 0;
                foreach (var lesson in futureSessions)
                {
                    if (!occurrenceStarts.Contains(lesson.StartsAt))
                    {
                        lesson.Cancel();
                        await CourseManagementReminderScheduling.CancelEntityAsync(db, workspaceId, lesson.Id, ct);
                        if (studentUserIds.Length > 0)
                            await publishEndpoint.Publish(new CourseManagementLessonCancelledV1(lesson.Id, workspaceId,
                                studentUserIds, lesson.StartsAt), ct);
                        AddAudit(db, workspaceId, userId, lesson.Id, "LessonSession.SeriesSlotCancelled");
                        sessionsCancelled++;
                    }
                }

                var sessionsCreated = 0;
                foreach (var occurrence in occurrences)
                {
                    if (existingByStart.TryGetValue(occurrence.StartsAt, out var existing))
                    {
                        if (existing.EndsAt != occurrence.EndsAt || existing.RoomId != request.RoomId)
                        {
                            existing.Reschedule(occurrence.StartsAt, occurrence.EndsAt, request.RoomId);
                            await CourseManagementReminderScheduling.ScheduleLessonAsync(db, workspaceId, existing.Id,
                                existing.StartsAt, studentUserIds, now, ct);
                            if (studentUserIds.Length > 0)
                                await publishEndpoint.Publish(new CourseManagementLessonRescheduledV1(existing.Id, workspaceId,
                                    studentUserIds, existing.StartsAt), ct);
                            sessionsUpdated++;
                        }
                        continue;
                    }

                    var lesson = LessonSession.Create(Guid.NewGuid(), workspaceId, series.CourseId, series.GroupId,
                        series.TeacherUserId, occurrence.StartsAt, occurrence.EndsAt, now, series.Id, request.RoomId);
                    db.LessonSessions.Add(lesson);
                    db.LessonParticipants.AddRange(participantStudentIds.Select(studentId => LessonParticipant.Create(
                        Guid.NewGuid(), workspaceId, lesson.Id, studentId)));
                    await CourseManagementReminderScheduling.ScheduleLessonAsync(db, workspaceId, lesson.Id,
                        lesson.StartsAt, studentUserIds, now, ct);
                    if (studentUserIds.Length > 0)
                        await publishEndpoint.Publish(new CourseManagementLessonScheduledV1(lesson.Id, workspaceId,
                            studentUserIds, lesson.StartsAt), ct);
                    sessionsCreated++;
                }

                var unmatchedCancelledSessions = Math.Max(0, sessionsCancelled - sessionsCreated);
                foreach (var lesson in futureSessions.Where(item => item.Status == LessonSessionStatus.Cancelled)
                    .OrderByDescending(item => item.StartsAt).Take(unmatchedCancelledSessions))
                    await GrantStaffCancellationCredits(db, workspaceId, lesson.Id, userId, now, ct);

                AddAudit(db, workspaceId, userId, series.Id, "LessonSeries.ScheduleUpdated");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new { seriesId, sessionsCreated, sessionsUpdated, sessionsCancelled });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentConflict" });
            }
        });

        api.MapPost("/workspaces/{workspaceId:guid}/lesson-series/{seriesId:guid}/cancel-future", async (Guid workspaceId, Guid seriesId,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
                var series = await db.LessonSeries.SingleOrDefaultAsync(item => item.Id == seriesId && item.WorkspaceId == workspaceId, ct);
                if (series is null) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && series.TeacherUserId != userId) return Results.NotFound();

                var now = DateTimeOffset.UtcNow;
                var futureSessions = await db.LessonSessions.Where(lesson => lesson.WorkspaceId == workspaceId
                    && lesson.SeriesId == seriesId && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now).ToListAsync(ct);
                if (futureSessions.Count == 0)
                {
                    await transaction.CommitAsync(ct);
                    return Results.Ok(new { seriesId, sessionsCancelled = 0 });
                }
                var lessonIds = futureSessions.Select(lesson => lesson.Id).ToList();
                var participantStudentIds = await db.LessonParticipants.AsNoTracking()
                    .Where(participant => participant.WorkspaceId == workspaceId && participant.IsActive
                        && lessonIds.Contains(participant.LessonSessionId))
                    .Select(participant => participant.StudentId).Distinct().ToListAsync(ct);
                var studentUserIds = await GetLinkedStudentUserIds(db, workspaceId, participantStudentIds, ct);
                if (await db.AttendanceRecords.AnyAsync(attendance => attendance.WorkspaceId == workspaceId
                        && lessonIds.Contains(attendance.LessonSessionId), ct))
                    return Results.Conflict(new { code = "Lesson.AttendanceStarted" });

                foreach (var lesson in futureSessions)
                {
                    lesson.Cancel();
                    await CourseManagementReminderScheduling.CancelEntityAsync(db, workspaceId, lesson.Id, ct);
                    if (studentUserIds.Length > 0)
                        await publishEndpoint.Publish(new CourseManagementLessonCancelledV1(lesson.Id, workspaceId,
                            studentUserIds, lesson.StartsAt), ct);
                    var makeupCredit = await db.MakeupCredits.SingleOrDefaultAsync(credit => credit.WorkspaceId == workspaceId
                        && credit.MakeupLessonId == lesson.Id, ct);
                    if (makeupCredit is null)
                        await GrantStaffCancellationCredits(db, workspaceId, lesson.Id, userId, now, ct);
                    else
                    {
                        makeupCredit.ReleaseFromCancelledLesson(now);
                        AddAudit(db, workspaceId, userId, makeupCredit.Id, "MakeupCredit.Released");
                    }
                }
                AddAudit(db, workspaceId, userId, seriesId, "LessonSeries.FutureCancelled");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new { seriesId, sessionsCancelled = futureSessions.Count });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentConflict" });
            }
        });

        api.MapPost("/workspaces/{workspaceId:guid}/lesson-series", async (Guid workspaceId, CreateLessonSeriesRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || request.CourseId == Guid.Empty || request.TeacherUserId == Guid.Empty
                || (request.GroupId is null) == (request.StudentId is null)
                || request.GroupId == Guid.Empty || request.StudentId == Guid.Empty || request.RoomId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.TimeZoneId)
                || !DateOnly.TryParseExact(request.StartsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startsOn)
                || !DateOnly.TryParseExact(request.EndsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endsOn)
                || !TimeOnly.TryParseExact(request.StartTime, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime)
                || request.DurationMinutes is < 1 or > 480 || request.DaysOfWeekMask is < 1 or > 127
                || endsOn < startsOn || endsOn.DayNumber - startsOn.DayNumber > 180)
                return Results.BadRequest(new { code = "LessonSeries.InvalidInput" });

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                return Results.BadRequest(new { code = "LessonSeries.InvalidTimeZone" });
            }
            catch (InvalidTimeZoneException)
            {
                return Results.BadRequest(new { code = "LessonSeries.InvalidTimeZone" });
            }

            var now = DateTimeOffset.UtcNow;
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
            if (startsOn < localToday) return Results.BadRequest(new { code = "LessonSeries.InvalidDateRange" });

            LessonSeries series;
            IReadOnlyList<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)> occurrences;
            try
            {
                series = LessonSeries.Create(request.Id, workspaceId, request.CourseId, request.GroupId, request.StudentId,
                    request.TeacherUserId, request.TimeZoneId, startsOn, endsOn, startTime,
                    request.DurationMinutes, request.DaysOfWeekMask, now, request.RoomId);
                occurrences = series.Expand(timeZone).Where(occurrence => occurrence.StartsAt > now).ToArray();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { code = "LessonSeries.InvalidInput" });
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { code = "LessonSeries.InvalidLocalTime" });
            }
            if (occurrences.Count == 0) return Results.BadRequest(new { code = "LessonSeries.NoFutureOccurrences" });

            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && request.TeacherUserId != userId) return Results.NotFound();
                var ownerUserId = await db.Workspaces.Where(workspace => workspace.Id == workspaceId)
                    .Select(workspace => workspace.OwnerUserId).SingleAsync(ct);
                var assignedTeacherExists = request.TeacherUserId == ownerUserId
                    || await IsActiveTeacher(db, workspaceId, request.TeacherUserId, ct);
                if (!assignedTeacherExists) return Results.BadRequest(new { code = "Lesson.TeacherNotInWorkspace" });
                if (!await db.Courses.AnyAsync(course => course.Id == request.CourseId && course.WorkspaceId == workspaceId, ct))
                    return Results.NotFound();

                List<Guid> studentIds;
                if (request.GroupId is Guid selectedGroupId)
                {
                    var courseGroup = await db.Groups.SingleOrDefaultAsync(item => item.Id == selectedGroupId
                        && item.WorkspaceId == workspaceId && item.CourseId == request.CourseId, ct);
                    if (courseGroup is null || courseGroup.TeacherUserId != request.TeacherUserId) return Results.NotFound();
                    studentIds = await (from enrollment in db.Enrollments
                        join student in db.Students on enrollment.StudentId equals student.Id
                        where enrollment.WorkspaceId == workspaceId && enrollment.GroupId == selectedGroupId
                            && enrollment.Status == EnrollmentStatus.Active && student.WorkspaceId == workspaceId && student.IsActive
                        select student.Id).Distinct().ToListAsync(ct);
                    if (studentIds.Count == 0) return Results.BadRequest(new { code = "Lesson.GroupHasNoActiveStudents" });
                }
                else
                {
                    var studentId = request.StudentId!.Value;
                    if (!await db.Students.AnyAsync(student => student.Id == studentId && student.WorkspaceId == workspaceId && student.IsActive, ct))
                        return Results.NotFound();
                    if (!isOwner && !await CanManageStudent(db, workspaceId, studentId, userId, ct)) return Results.NotFound();
                    studentIds = [studentId];
                }

                CourseRoom? room = null;
                if (request.RoomId is Guid roomId)
                {
                    room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId && item.WorkspaceId == workspaceId, ct);
                    if (room is null) return Results.NotFound();
                    if (room.Capacity is int capacity && studentIds.Count > capacity)
                        return Results.Conflict(new { code = "Room.CapacityExceeded" });
                }

                var earliestStart = occurrences.Min(occurrence => occurrence.StartsAt);
                var latestEnd = occurrences.Max(occurrence => occurrence.EndsAt);
                var teacherSessions = await db.LessonSessions.AsNoTracking()
                    .Where(existing => existing.WorkspaceId == workspaceId && existing.Status == LessonSessionStatus.Scheduled
                        && existing.TeacherUserId == request.TeacherUserId && existing.StartsAt < latestEnd && existing.EndsAt > earliestStart)
                    .Select(existing => new { existing.StartsAt, existing.EndsAt }).ToListAsync(ct);
                if (occurrences.Any(occurrence => teacherSessions.Any(existing => existing.StartsAt < occurrence.EndsAt
                    && occurrence.StartsAt < existing.EndsAt)))
                    return Results.Conflict(new { code = "Lesson.TeacherConflict" });

                var teacherTimeOff = await db.TeacherTimeOffs.AsNoTracking()
                    .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == request.TeacherUserId
                        && item.StartsAt < latestEnd && item.EndsAt > earliestStart)
                    .Select(item => new { item.StartsAt, item.EndsAt }).ToListAsync(ct);
                if (occurrences.Any(occurrence => teacherTimeOff.Any(item => item.StartsAt < occurrence.EndsAt
                    && occurrence.StartsAt < item.EndsAt)))
                    return Results.Conflict(new { code = "Availability.TimeOffConflict" });

                var availabilityWindows = await db.TeacherAvailabilityWindows.AsNoTracking()
                    .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == request.TeacherUserId)
                    .ToListAsync(ct);
                if (occurrences.Any(occurrence => !FitsTeacherAvailability(occurrence, availabilityWindows)))
                    return Results.Conflict(new { code = "Availability.OutsideWorkingHours" });

                var studentSessions = await (from participant in db.LessonParticipants.AsNoTracking()
                    join existing in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals existing.Id
                    where participant.WorkspaceId == workspaceId && participant.IsActive && studentIds.Contains(participant.StudentId)
                        && existing.WorkspaceId == workspaceId && existing.Status == LessonSessionStatus.Scheduled
                        && existing.StartsAt < latestEnd && existing.EndsAt > earliestStart
                    select new { existing.StartsAt, existing.EndsAt }).ToListAsync(ct);
                if (occurrences.Any(occurrence => studentSessions.Any(existing => existing.StartsAt < occurrence.EndsAt
                    && occurrence.StartsAt < existing.EndsAt)))
                    return Results.Conflict(new { code = "Lesson.StudentConflict" });

                if (room is not null)
                {
                    var roomSessions = await db.LessonSessions.AsNoTracking()
                        .Where(existing => existing.WorkspaceId == workspaceId && existing.RoomId == room.Id
                            && existing.Status == LessonSessionStatus.Scheduled
                            && existing.StartsAt < latestEnd && existing.EndsAt > earliestStart)
                        .Select(existing => new { existing.StartsAt, existing.EndsAt }).ToListAsync(ct);
                    if (occurrences.Any(occurrence => roomSessions.Any(existing => existing.StartsAt < occurrence.EndsAt
                        && occurrence.StartsAt < existing.EndsAt)))
                        return Results.Conflict(new { code = "Room.Conflict" });
                }

                db.LessonSeries.Add(series);
                var sessions = occurrences.Select(occurrence => LessonSession.Create(Guid.NewGuid(), workspaceId,
                    request.CourseId, request.GroupId, request.TeacherUserId, occurrence.StartsAt, occurrence.EndsAt, now, series.Id, request.RoomId)).ToArray();
                db.LessonSessions.AddRange(sessions);
                db.LessonParticipants.AddRange(sessions.SelectMany(session => studentIds.Select(studentId =>
                    LessonParticipant.Create(Guid.NewGuid(), workspaceId, session.Id, studentId))));
                var studentUserIds = await GetLinkedStudentUserIds(db, workspaceId, studentIds, ct);
                if (studentUserIds.Length > 0)
                    foreach (var session in sessions)
                    {
                        await CourseManagementReminderScheduling.ScheduleLessonAsync(db, workspaceId, session.Id,
                            session.StartsAt, studentUserIds, now, ct);
                        await publishEndpoint.Publish(new CourseManagementLessonScheduledV1(session.Id, workspaceId,
                            studentUserIds, session.StartsAt), ct);
                    }
                AddAudit(db, workspaceId, userId, series.Id, "LessonSeries.Created");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new { seriesId = series.Id, sessionsCreated = sessions.Length });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentConflict" });
            }
        });

        api.MapPost("/workspaces/{workspaceId:guid}/lessons", async (Guid workspaceId, CreateLessonSessionRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await CanManageWorkspace(db, workspaceId, userId, ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || request.CourseId == Guid.Empty || request.TeacherUserId == Guid.Empty
                || (request.GroupId is null) == (request.StudentId is null)
                || request.GroupId == Guid.Empty || request.StudentId == Guid.Empty || request.RoomId == Guid.Empty
                || request.MakeupCreditId == Guid.Empty)
                return Results.BadRequest(new { code = "Lesson.InvalidInput" });

            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && request.TeacherUserId != userId) return Results.NotFound();
                var ownerUserId = await db.Workspaces.Where(workspace => workspace.Id == workspaceId)
                    .Select(workspace => workspace.OwnerUserId).SingleAsync(ct);
                var assignedTeacherExists = request.TeacherUserId == ownerUserId
                    || await IsActiveTeacher(db, workspaceId, request.TeacherUserId, ct);
                if (!assignedTeacherExists) return Results.BadRequest(new { code = "Lesson.TeacherNotInWorkspace" });

                if (!await db.Courses.AnyAsync(course => course.Id == request.CourseId && course.WorkspaceId == workspaceId, ct))
                    return Results.NotFound();

                var groupId = request.GroupId;
                List<Guid> studentIds;
                if (groupId is Guid selectedGroupId)
                {
                    var courseGroup = await db.Groups.SingleOrDefaultAsync(item => item.Id == selectedGroupId
                        && item.WorkspaceId == workspaceId && item.CourseId == request.CourseId, ct);
                    if (courseGroup is null || courseGroup.TeacherUserId != request.TeacherUserId) return Results.NotFound();
                    studentIds = await (from enrollment in db.Enrollments
                        join student in db.Students on enrollment.StudentId equals student.Id
                        where enrollment.WorkspaceId == workspaceId && enrollment.GroupId == selectedGroupId
                            && enrollment.Status == EnrollmentStatus.Active && student.WorkspaceId == workspaceId && student.IsActive
                        select student.Id).Distinct().ToListAsync(ct);
                    if (studentIds.Count == 0) return Results.BadRequest(new { code = "Lesson.GroupHasNoActiveStudents" });
                }
                else
                {
                    var studentId = request.StudentId!.Value;
                    if (!await db.Students.AnyAsync(student => student.Id == studentId && student.WorkspaceId == workspaceId && student.IsActive, ct))
                        return Results.NotFound();
                    if (!isOwner && !await CanManageStudent(db, workspaceId, studentId, userId, ct)) return Results.NotFound();
                    studentIds = [studentId];
                }

                CourseRoom? room = null;
                if (request.RoomId is Guid roomId)
                {
                    room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId && item.WorkspaceId == workspaceId, ct);
                    if (room is null) return Results.NotFound();
                    if (room.Capacity is int capacity && studentIds.Count > capacity)
                        return Results.Conflict(new { code = "Room.CapacityExceeded" });
                }

                var startsAt = request.StartsAt.ToUniversalTime();
                var endsAt = request.EndsAt.ToUniversalTime();
                var now = DateTimeOffset.UtcNow;
                if (startsAt <= now || endsAt <= startsAt || endsAt - startsAt > TimeSpan.FromHours(8))
                    return Results.BadRequest(new { code = "Lesson.InvalidTime" });

                MakeupCredit? makeupCredit = null;
                if (request.MakeupCreditId is Guid makeupCreditId)
                {
                    if (groupId is not null || request.StudentId is null)
                        return Results.BadRequest(new { code = "MakeupCredit.InvalidLesson" });
                    makeupCredit = await db.MakeupCredits.SingleOrDefaultAsync(item => item.Id == makeupCreditId
                        && item.WorkspaceId == workspaceId, ct);
                    if (makeupCredit is null || makeupCredit.StudentId != request.StudentId.Value)
                        return Results.NotFound();
                    if (makeupCredit.Status != MakeupCreditStatus.Available || makeupCredit.ExpiresAt <= now)
                        return Results.Conflict(new { code = "MakeupCredit.NotAvailable" });
                    var sourceLesson = await db.LessonSessions.SingleOrDefaultAsync(item => item.Id == makeupCredit.SourceLessonId
                        && item.WorkspaceId == workspaceId, ct);
                    if (sourceLesson is null || sourceLesson.CourseId != request.CourseId
                        || sourceLesson.TeacherUserId != request.TeacherUserId)
                        return Results.BadRequest(new { code = "MakeupCredit.LessonMismatch" });
                    if (endsAt > makeupCredit.ExpiresAt)
                        return Results.Conflict(new { code = "MakeupCredit.Expired" });
                }

                if (await db.TeacherTimeOffs.AnyAsync(item => item.WorkspaceId == workspaceId
                        && item.TeacherUserId == request.TeacherUserId && item.StartsAt < endsAt && startsAt < item.EndsAt, ct))
                    return Results.Conflict(new { code = "Availability.TimeOffConflict" });

                var availabilityWindows = await db.TeacherAvailabilityWindows.AsNoTracking()
                    .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == request.TeacherUserId)
                    .ToListAsync(ct);
                if (!FitsTeacherAvailability((startsAt, endsAt), availabilityWindows))
                    return Results.Conflict(new { code = "Availability.OutsideWorkingHours" });

                if (await db.LessonSessions.AnyAsync(existing => existing.WorkspaceId == workspaceId
                        && existing.Status == LessonSessionStatus.Scheduled
                        && existing.TeacherUserId == request.TeacherUserId
                        && existing.StartsAt < endsAt && startsAt < existing.EndsAt, ct))
                    return Results.Conflict(new { code = "Lesson.TeacherConflict" });

                var hasStudentConflict = await (from participant in db.LessonParticipants
                    join existing in db.LessonSessions on participant.LessonSessionId equals existing.Id
                    where participant.WorkspaceId == workspaceId && participant.IsActive && existing.WorkspaceId == workspaceId
                        && existing.Status == LessonSessionStatus.Scheduled && existing.StartsAt < endsAt && startsAt < existing.EndsAt
                        && studentIds.Contains(participant.StudentId)
                    select participant.Id).AnyAsync(ct);
                if (hasStudentConflict) return Results.Conflict(new { code = "Lesson.StudentConflict" });

                if (room is not null && await db.LessonSessions.AnyAsync(existing => existing.WorkspaceId == workspaceId
                        && existing.RoomId == room.Id && existing.Status == LessonSessionStatus.Scheduled
                        && existing.StartsAt < endsAt && startsAt < existing.EndsAt, ct))
                    return Results.Conflict(new { code = "Room.Conflict" });

                var lesson = LessonSession.Create(request.Id, workspaceId, request.CourseId, groupId,
                    request.TeacherUserId, startsAt, endsAt, now, roomId: request.RoomId);
                db.LessonSessions.Add(lesson);
                db.LessonParticipants.AddRange(studentIds.Select(studentId => LessonParticipant.Create(
                    Guid.NewGuid(), workspaceId, lesson.Id, studentId)));
                var studentUserIds = await GetLinkedStudentUserIds(db, workspaceId, studentIds, ct);
                await CourseManagementReminderScheduling.ScheduleLessonAsync(db, workspaceId, lesson.Id,
                    lesson.StartsAt, studentUserIds, now, ct);
                if (studentUserIds.Length > 0)
                    await publishEndpoint.Publish(new CourseManagementLessonScheduledV1(lesson.Id, workspaceId,
                        studentUserIds, lesson.StartsAt), ct);
                AddAudit(db, workspaceId, userId, lesson.Id, "LessonSession.Created");
                if (makeupCredit is not null)
                {
                    makeupCredit.Schedule(lesson.Id, endsAt, now);
                    AddAudit(db, workspaceId, userId, makeupCredit.Id, "MakeupCredit.Scheduled");
                }
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/lessons/{lesson.Id}", new { lesson.Id });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentConflict" });
            }
        });

        api.MapPut("/workspaces/{workspaceId:guid}/lessons/{lessonId:guid}/schedule", async (Guid workspaceId, Guid lessonId,
            RescheduleLessonRequest request, ClaimsPrincipal user, CourseManagementDbContext db,
            IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (request.RoomId == Guid.Empty) return Results.BadRequest(new { code = "Lesson.InvalidInput" });
            var startsAt = request.StartsAt.ToUniversalTime();
            var endsAt = request.EndsAt.ToUniversalTime();
            var now = DateTimeOffset.UtcNow;
            if (startsAt <= now || endsAt <= startsAt || endsAt - startsAt > TimeSpan.FromHours(8))
                return Results.BadRequest(new { code = "Lesson.InvalidTime" });

            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var lesson = await db.LessonSessions.SingleOrDefaultAsync(item => item.Id == lessonId && item.WorkspaceId == workspaceId, ct);
                if (lesson is null) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && (lesson.TeacherUserId != userId || !await IsActiveTeacher(db, workspaceId, userId, ct)))
                    return Results.NotFound();
                if (lesson.Status != LessonSessionStatus.Scheduled) return Results.Conflict(new { code = "Lesson.NotScheduled" });
                if (lesson.SeriesId is not null) return Results.Conflict(new { code = "Lesson.SeriesOccurrenceMustUseSeriesEditor" });
                if (lesson.StartsAt <= now || await db.AttendanceRecords.AnyAsync(attendance => attendance.WorkspaceId == workspaceId
                        && attendance.LessonSessionId == lessonId, ct))
                    return Results.Conflict(new { code = "Lesson.AttendanceStarted" });
                var makeupCredit = await db.MakeupCredits.SingleOrDefaultAsync(credit => credit.WorkspaceId == workspaceId
                    && credit.MakeupLessonId == lessonId, ct);
                if (makeupCredit is not null && endsAt > makeupCredit.ExpiresAt)
                    return Results.Conflict(new { code = "MakeupCredit.Expired" });

                var studentIds = await db.LessonParticipants.AsNoTracking().Where(participant => participant.WorkspaceId == workspaceId
                        && participant.LessonSessionId == lessonId && participant.IsActive)
                    .Select(participant => participant.StudentId).ToListAsync(ct);
                if (studentIds.Count == 0) return Results.Conflict(new { code = "Lesson.NoParticipants" });
                var studentUserIds = await GetLinkedStudentUserIds(db, workspaceId, studentIds, ct);

                CourseRoom? room = null;
                if (request.RoomId is Guid roomId)
                {
                    room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId && item.WorkspaceId == workspaceId, ct);
                    if (room is null) return Results.NotFound();
                    if (room.Capacity is int capacity && studentIds.Count > capacity)
                        return Results.Conflict(new { code = "Room.CapacityExceeded" });
                }

                if (await db.TeacherTimeOffs.AnyAsync(item => item.WorkspaceId == workspaceId
                        && item.TeacherUserId == lesson.TeacherUserId && item.StartsAt < endsAt && startsAt < item.EndsAt, ct))
                    return Results.Conflict(new { code = "Availability.TimeOffConflict" });
                var availabilityWindows = await db.TeacherAvailabilityWindows.AsNoTracking()
                    .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == lesson.TeacherUserId)
                    .ToListAsync(ct);
                if (!FitsTeacherAvailability((startsAt, endsAt), availabilityWindows))
                    return Results.Conflict(new { code = "Availability.OutsideWorkingHours" });

                if (await db.LessonSessions.AnyAsync(existing => existing.Id != lessonId && existing.WorkspaceId == workspaceId
                        && existing.Status == LessonSessionStatus.Scheduled && existing.TeacherUserId == lesson.TeacherUserId
                        && existing.StartsAt < endsAt && startsAt < existing.EndsAt, ct))
                    return Results.Conflict(new { code = "Lesson.TeacherConflict" });
                if (await (from participant in db.LessonParticipants.AsNoTracking()
                        join existing in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals existing.Id
                        where participant.WorkspaceId == workspaceId && participant.IsActive && participant.LessonSessionId != lessonId
                            && studentIds.Contains(participant.StudentId) && existing.WorkspaceId == workspaceId
                            && existing.Status == LessonSessionStatus.Scheduled && existing.StartsAt < endsAt && startsAt < existing.EndsAt
                        select participant.Id).AnyAsync(ct))
                    return Results.Conflict(new { code = "Lesson.StudentConflict" });
                if (room is not null && await db.LessonSessions.AnyAsync(existing => existing.Id != lessonId
                        && existing.WorkspaceId == workspaceId && existing.RoomId == room.Id
                        && existing.Status == LessonSessionStatus.Scheduled && existing.StartsAt < endsAt && startsAt < existing.EndsAt, ct))
                    return Results.Conflict(new { code = "Room.Conflict" });

                lesson.Reschedule(startsAt, endsAt, request.RoomId);
                await CourseManagementReminderScheduling.ScheduleLessonAsync(db, workspaceId, lesson.Id,
                    lesson.StartsAt, studentUserIds, now, ct);
                if (studentUserIds.Length > 0)
                    await publishEndpoint.Publish(new CourseManagementLessonRescheduledV1(lesson.Id, workspaceId,
                        studentUserIds, lesson.StartsAt), ct);
                AddAudit(db, workspaceId, userId, lesson.Id, "LessonSession.Rescheduled");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new { lesson.Id, lesson.StartsAt, lesson.EndsAt, lesson.RoomId });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentConflict" });
            }
        });

        api.MapPost("/workspaces/{workspaceId:guid}/lessons/{lessonId:guid}/cancel", async (Guid workspaceId, Guid lessonId,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            var userId = UserId(user);
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var lesson = await db.LessonSessions.SingleOrDefaultAsync(item => item.Id == lessonId && item.WorkspaceId == workspaceId, ct);
                if (lesson is null) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && (lesson.TeacherUserId != userId || !await IsActiveTeacher(db, workspaceId, userId, ct)))
                    return Results.NotFound();
                var now = DateTimeOffset.UtcNow;
                if (lesson.Status != LessonSessionStatus.Scheduled) return Results.Conflict(new { code = "Lesson.NotScheduled" });
                if (lesson.StartsAt <= now || await db.AttendanceRecords.AnyAsync(attendance => attendance.WorkspaceId == workspaceId
                        && attendance.LessonSessionId == lessonId, ct))
                    return Results.Conflict(new { code = "Lesson.AttendanceStarted" });

                var studentIds = await db.LessonParticipants.AsNoTracking().Where(participant => participant.WorkspaceId == workspaceId
                        && participant.LessonSessionId == lessonId && participant.IsActive)
                    .Select(participant => participant.StudentId).ToListAsync(ct);
                var studentUserIds = await GetLinkedStudentUserIds(db, workspaceId, studentIds, ct);
                lesson.Cancel();
                await CourseManagementReminderScheduling.CancelEntityAsync(db, workspaceId, lesson.Id, ct);
                var makeupCredit = await db.MakeupCredits.SingleOrDefaultAsync(credit => credit.WorkspaceId == workspaceId
                    && credit.MakeupLessonId == lessonId, ct);
                if (makeupCredit is null)
                    await GrantStaffCancellationCredits(db, workspaceId, lesson.Id, userId, now, ct);
                else
                {
                    makeupCredit.ReleaseFromCancelledLesson(now);
                    AddAudit(db, workspaceId, userId, makeupCredit.Id, "MakeupCredit.Released");
                }
                if (studentUserIds.Length > 0)
                    await publishEndpoint.Publish(new CourseManagementLessonCancelledV1(lesson.Id, workspaceId,
                        studentUserIds, lesson.StartsAt), ct);
                AddAudit(db, workspaceId, userId, lesson.Id, "LessonSession.Cancelled");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.NoContent();
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentConflict" });
            }
        });

        api.MapPut("/workspaces/{workspaceId:guid}/lessons/{lessonId:guid}/students/{studentId:guid}/attendance",
            async (Guid workspaceId, Guid lessonId, Guid studentId, UpdateLessonAttendanceRequest request,
                ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!Enum.IsDefined(typeof(LessonAttendanceStatus), request.Status))
                return Results.BadRequest(new { code = "Attendance.InvalidStatus" });

            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var lesson = await db.LessonSessions.SingleOrDefaultAsync(item => item.Id == lessonId && item.WorkspaceId == workspaceId, ct);
                if (lesson is null) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && (lesson.TeacherUserId != userId || !await IsActiveTeacher(db, workspaceId, userId, ct)))
                    return Results.NotFound();
                if (lesson.Status != LessonSessionStatus.Scheduled)
                    return Results.Conflict(new { code = "Attendance.LessonClosed" });
                var now = DateTimeOffset.UtcNow;
                if (lesson.StartsAt > now) return Results.Conflict(new { code = "Attendance.LessonNotStarted" });
                if (!await db.LessonParticipants.AnyAsync(participant => participant.WorkspaceId == workspaceId
                    && participant.LessonSessionId == lessonId && participant.StudentId == studentId && participant.IsActive, ct))
                    return Results.NotFound();

                var status = (LessonAttendanceStatus)request.Status;
                var attendance = await db.AttendanceRecords.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId
                    && item.LessonSessionId == lessonId && item.StudentId == studentId, ct);
                if (attendance is null)
                {
                    attendance = AttendanceRecord.Create(Guid.NewGuid(), workspaceId, lessonId, studentId, status, userId, now);
                    db.AttendanceRecords.Add(attendance);
                    AddAudit(db, workspaceId, userId, attendance.Id, "Attendance.Marked");
                }
                else if (attendance.Status != status)
                {
                    attendance.UpdateStatus(status, userId, now);
                    AddAudit(db, workspaceId, userId, attendance.Id, "Attendance.Updated");
                }
                else
                {
                    await transaction.CommitAsync(ct);
                    return Results.Ok(new { lessonId, studentId, status = attendance.Status.ToString(), attendance.MarkedAt });
                }

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new { lessonId, studentId, status = attendance.Status.ToString(), attendance.MarkedAt });
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Attendance.ConcurrentUpdate" });
            }
        });

        api.MapPost("/workspaces/{workspaceId:guid}/lessons/{lessonId:guid}/complete",
            async (Guid workspaceId, Guid lessonId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var lesson = await db.LessonSessions.SingleOrDefaultAsync(item => item.Id == lessonId && item.WorkspaceId == workspaceId, ct);
                if (lesson is null) return Results.NotFound();
                var isOwner = await IsOwner(db, workspaceId, userId, ct);
                if (!isOwner && (lesson.TeacherUserId != userId || !await IsActiveTeacher(db, workspaceId, userId, ct)))
                    return Results.NotFound();
                if (lesson.Status != LessonSessionStatus.Scheduled)
                    return Results.Conflict(new { code = "Lesson.NotScheduled" });
                if (lesson.EndsAt > DateTimeOffset.UtcNow)
                    return Results.Conflict(new { code = "Lesson.NotEnded" });

                var participantCount = await db.LessonParticipants.CountAsync(participant => participant.WorkspaceId == workspaceId
                    && participant.LessonSessionId == lessonId && participant.IsActive, ct);
                var hasUnmarkedParticipant = await db.LessonParticipants.AnyAsync(participant => participant.WorkspaceId == workspaceId
                    && participant.LessonSessionId == lessonId && participant.IsActive
                    && !db.AttendanceRecords.Any(attendance => attendance.WorkspaceId == workspaceId
                        && attendance.LessonSessionId == lessonId && attendance.StudentId == participant.StudentId), ct);
                if (participantCount == 0 || hasUnmarkedParticipant)
                    return Results.Conflict(new { code = "Lesson.AttendanceIncomplete" });

                var completedAt = DateTimeOffset.UtcNow;
                await CourseManagementReminderScheduling.CancelEntityAsync(db, workspaceId, lesson.Id, ct);
                var activeStudentIds = await db.LessonParticipants.Where(participant => participant.WorkspaceId == workspaceId
                    && participant.LessonSessionId == lessonId && participant.IsActive).Select(participant => participant.StudentId).ToListAsync(ct);
                var feeAgreements = await db.FeeAgreements.Where(agreement => agreement.WorkspaceId == workspaceId
                    && agreement.CourseId == lesson.CourseId && agreement.IsActive && activeStudentIds.Contains(agreement.StudentId)
                    && (agreement.Model == FeeModel.PerLesson || agreement.Model == FeeModel.LessonPackage)).ToListAsync(ct);
                var studentUserIdsByStudent = await db.Students.AsNoTracking().Where(student => student.WorkspaceId == workspaceId
                    && activeStudentIds.Contains(student.Id) && student.UserId != null)
                    .ToDictionaryAsync(student => student.Id, student => student.UserId!.Value, ct);
                foreach (var agreement in feeAgreements)
                {
                    if (agreement.Model == FeeModel.PerLesson)
                    {
                        var amount = agreement.BillingUnit == LessonBillingUnit.Hour
                            ? decimal.Round(agreement.Amount * (decimal)(lesson.EndsAt - lesson.StartsAt).TotalMinutes / 60m, 2, MidpointRounding.AwayFromZero)
                            : agreement.Amount;
                        var installment = Installment.CreateLessonCharge(Guid.NewGuid(), agreement, lessonId, amount,
                            DateOnly.FromDateTime(lesson.EndsAt.UtcDateTime), completedAt);
                        db.Installments.Add(installment);
                        studentUserIdsByStudent.TryGetValue(agreement.StudentId, out var studentUserId);
                        await CourseManagementReminderScheduling.ScheduleInstallmentAsync(db, installment,
                            studentUserId == Guid.Empty ? null : studentUserId, completedAt, ct);
                        AddAudit(db, workspaceId, userId, agreement.Id, "Finance.LessonCharged");
                    }
                    else
                    {
                        if (agreement.PackageExpiresAt is { } packageExpiry && lesson.EndsAt > packageExpiry)
                            return Results.Conflict(new { code = "Finance.PackageExpired" });
                        var availableSessions = await db.PackageMovements.Where(movement => movement.FeeAgreementId == agreement.Id)
                            .SumAsync(movement => movement.Quantity, ct);
                        if (availableSessions < 1)
                            return Results.Conflict(new { code = "Finance.PackageInsufficient" });
                        db.PackageMovements.Add(PackageMovement.Consume(Guid.NewGuid(), agreement, lessonId, completedAt));
                        AddAudit(db, workspaceId, userId, agreement.Id, "Finance.PackageConsumed");
                    }
                }

                lesson.Complete();
                AddAudit(db, workspaceId, userId, lesson.Id, "LessonSession.Completed");
                var makeupCredit = await db.MakeupCredits.SingleOrDefaultAsync(credit => credit.WorkspaceId == workspaceId
                    && credit.MakeupLessonId == lessonId, ct);
                if (makeupCredit is not null)
                {
                    makeupCredit.Consume();
                    AddAudit(db, workspaceId, userId, makeupCredit.Id, "MakeupCredit.Consumed");
                }
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.NoContent();
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentUpdate" });
            }
        });

        api.MapGet("/me/makeup-credits", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            var now = DateTimeOffset.UtcNow;
            var credits = await (from credit in db.MakeupCredits.AsNoTracking()
                join student in db.Students.AsNoTracking() on credit.StudentId equals student.Id
                join membership in db.Memberships.AsNoTracking() on student.WorkspaceId equals membership.WorkspaceId
                where student.UserId == userId && student.IsActive && membership.UserId == userId
                    && membership.IsActive && membership.Role == WorkspaceRole.Student
                orderby credit.ExpiresAt
                select new { credit.Id, credit.WorkspaceId, credit.StudentId, credit.SourceLessonId, credit.MakeupLessonId,
                    credit.Reason, credit.Status, credit.GrantedAt, credit.ExpiresAt })
                .Take(100).ToListAsync(ct);
            return Results.Ok(credits.Select(credit => new { credit.Id, credit.WorkspaceId, credit.StudentId,
                credit.SourceLessonId, credit.MakeupLessonId, reason = credit.Reason.ToString(),
                status = ((credit.Status is MakeupCreditStatus.PendingTeacherApproval or MakeupCreditStatus.Available)
                    && credit.ExpiresAt <= now ? MakeupCreditStatus.Expired : credit.Status).ToString(),
                credit.GrantedAt, credit.ExpiresAt }));
        });

        api.MapPost("/me/lessons/{lessonId:guid}/cancel-participation", async (Guid lessonId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var lesson = await db.LessonSessions.SingleOrDefaultAsync(item => item.Id == lessonId, ct);
                if (lesson is null) return Results.NotFound();
                var student = await db.Students.SingleOrDefaultAsync(item => item.WorkspaceId == lesson.WorkspaceId
                    && item.UserId == userId && item.IsActive, ct);
                var isStudentMember = student is not null && await db.Memberships.AnyAsync(member => member.WorkspaceId == lesson.WorkspaceId
                    && member.UserId == userId && member.IsActive && member.Role == WorkspaceRole.Student, ct);
                if (student is null || !isStudentMember) return Results.NotFound();
                var participant = await db.LessonParticipants.SingleOrDefaultAsync(item => item.WorkspaceId == lesson.WorkspaceId
                    && item.LessonSessionId == lessonId && item.StudentId == student.Id, ct);
                if (participant is null) return Results.NotFound();
                var now = DateTimeOffset.UtcNow;
                if (!participant.IsActive)
                {
                    var previousCredit = await db.MakeupCredits.SingleOrDefaultAsync(credit => credit.WorkspaceId == lesson.WorkspaceId
                        && credit.SourceLessonId == lessonId && credit.StudentId == student.Id, ct);
                    var linkedCredit = previousCredit ?? await db.MakeupCredits.SingleOrDefaultAsync(credit => credit.WorkspaceId == lesson.WorkspaceId
                        && credit.MakeupLessonId == lessonId && credit.StudentId == student.Id, ct);
                    await transaction.CommitAsync(ct);
                    return Results.Ok(new StudentLessonCancellationResponse(lesson.Id,
                        linkedCredit?.Reason == MakeupCreditReason.StudentEarlyCancellation,
                        linkedCredit?.Id, linkedCredit?.EffectiveStatus(now).ToString()));
                }
                if (lesson.Status != LessonSessionStatus.Scheduled)
                    return Results.Conflict(new { code = "Lesson.NotScheduled" });
                if (lesson.StartsAt <= now || await db.AttendanceRecords.AnyAsync(attendance => attendance.WorkspaceId == lesson.WorkspaceId
                        && attendance.LessonSessionId == lessonId && attendance.StudentId == student.Id, ct))
                    return Results.Conflict(new { code = "Lesson.AttendanceStarted" });

                var isEarlyCancellation = lesson.StartsAt - now >= TimeSpan.FromHours(24);
                var makeupCredit = await db.MakeupCredits.SingleOrDefaultAsync(credit => credit.WorkspaceId == lesson.WorkspaceId
                    && credit.MakeupLessonId == lessonId && credit.StudentId == student.Id, ct);
                participant.Cancel(now);
                await CourseManagementReminderScheduling.CancelEntityAsync(db, lesson.WorkspaceId, lessonId, ct,
                    userId);
                if (makeupCredit is not null)
                    makeupCredit.StudentCancelledMakeup(isEarlyCancellation, now);
                else if (isEarlyCancellation)
                {
                    makeupCredit = MakeupCredit.Create(Guid.NewGuid(), lesson.WorkspaceId, student.Id, lessonId,
                        userId, MakeupCreditReason.StudentEarlyCancellation, now);
                    db.MakeupCredits.Add(makeupCredit);
                }

                var activeParticipantsRemain = await db.LessonParticipants.AnyAsync(item => item.WorkspaceId == lesson.WorkspaceId
                    && item.LessonSessionId == lessonId && item.Id != participant.Id && item.IsActive, ct);
                if (!activeParticipantsRemain)
                {
                    lesson.Cancel();
                    await CourseManagementReminderScheduling.CancelEntityAsync(db, lesson.WorkspaceId, lessonId, ct);
                }
                AddAudit(db, lesson.WorkspaceId, userId, lesson.Id, "LessonParticipation.Cancelled");
                if (makeupCredit is not null)
                    AddAudit(db, lesson.WorkspaceId, userId, makeupCredit.Id,
                        isEarlyCancellation ? "MakeupCredit.Requested" : "MakeupCredit.Consumed");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new StudentLessonCancellationResponse(lesson.Id, isEarlyCancellation,
                    makeupCredit?.Id, makeupCredit?.EffectiveStatus(now).ToString()));
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentConflict" });
            }
        });

        api.MapGet("/me/lessons", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            var now = DateTimeOffset.UtcNow;
            var lessons = await (from participant in db.LessonParticipants.AsNoTracking()
                join student in db.Students.AsNoTracking() on participant.StudentId equals student.Id
                join membership in db.Memberships.AsNoTracking() on new { student.WorkspaceId, UserId = student.UserId!.Value }
                    equals new { membership.WorkspaceId, membership.UserId }
                join lesson in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals lesson.Id
                join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                join workspace in db.Workspaces.AsNoTracking() on lesson.WorkspaceId equals workspace.Id
                join groupJoin in db.Groups.AsNoTracking() on lesson.GroupId equals groupJoin.Id into groupJoins
                from courseGroup in groupJoins.DefaultIfEmpty()
                join teacherJoin in db.UserProfiles.AsNoTracking() on lesson.TeacherUserId equals teacherJoin.UserId into teacherJoins
                from teacher in teacherJoins.DefaultIfEmpty()
                where participant.IsActive && student.UserId == userId && student.IsActive && membership.IsActive && membership.Role == WorkspaceRole.Student
                    && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt >= now
                orderby lesson.StartsAt
                select new { lesson.Id, workspaceName = workspace.Name, courseName = course.Name, course.Subject,
                    groupName = courseGroup == null ? null : courseGroup.Name,
                    teacherName = teacher == null ? null : teacher.DisplayName, lesson.StartsAt, lesson.EndsAt })
                .Take(100).ToListAsync(ct);
            return Results.Ok(lessons);
        });

        api.MapGet("/me/attendance", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            var attendance = await (from record in db.AttendanceRecords.AsNoTracking()
                join student in db.Students.AsNoTracking() on record.StudentId equals student.Id
                join membership in db.Memberships.AsNoTracking() on new { student.WorkspaceId, UserId = student.UserId!.Value }
                    equals new { membership.WorkspaceId, membership.UserId }
                join lesson in db.LessonSessions.AsNoTracking() on record.LessonSessionId equals lesson.Id
                join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                join workspace in db.Workspaces.AsNoTracking() on record.WorkspaceId equals workspace.Id
                where student.UserId == userId && student.IsActive && membership.IsActive
                    && membership.Role == WorkspaceRole.Student && record.WorkspaceId == student.WorkspaceId
                    && lesson.WorkspaceId == record.WorkspaceId && course.WorkspaceId == record.WorkspaceId
                    && lesson.Status == LessonSessionStatus.Completed
                orderby lesson.StartsAt descending
                select new { recordId = record.Id, lessonId = lesson.Id, workspaceName = workspace.Name,
                    courseName = course.Name, course.Subject, lesson.StartsAt, lesson.EndsAt, record.Status, record.MarkedAt })
                .Take(100).ToListAsync(ct);
            return Results.Ok(attendance.Select(item => new StudentAttendanceView(item.recordId, item.lessonId,
                item.workspaceName, item.courseName, item.Subject, item.StartsAt, item.EndsAt, item.Status.ToString(), item.MarkedAt)));
        });

        api.MapGet("/me/courses", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
            Results.Ok(await (from student in db.Students.AsNoTracking()
                join membership in db.Memberships.AsNoTracking() on new { student.WorkspaceId, UserId = student.UserId!.Value }
                    equals new { membership.WorkspaceId, membership.UserId }
                join enrollment in db.Enrollments.AsNoTracking() on student.Id equals enrollment.StudentId
                join courseGroup in db.Groups.AsNoTracking() on enrollment.GroupId equals courseGroup.Id
                join course in db.Courses.AsNoTracking() on courseGroup.CourseId equals course.Id
                where student.UserId == UserId(user) && student.IsActive && membership.IsActive && membership.Role == WorkspaceRole.Student
                    && enrollment.Status == EnrollmentStatus.Active
                orderby course.Name, courseGroup.Name
                select new { course.Id, course.Name, course.Subject, groupName = courseGroup.Name })
                .Take(100).ToListAsync(ct)));
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static Task<bool> IsOwner(CourseManagementDbContext db, Guid workspaceId, Guid userId, CancellationToken ct) =>
        db.Workspaces.AnyAsync(workspace => workspace.Id == workspaceId && workspace.OwnerUserId == userId, ct);
    private static async Task<bool> CanManageWorkspace(CourseManagementDbContext db, Guid workspaceId, Guid userId, CancellationToken ct) =>
        await IsOwner(db, workspaceId, userId, ct)
        || await db.Memberships.AnyAsync(member => member.WorkspaceId == workspaceId && member.UserId == userId
            && member.IsActive && member.Role == WorkspaceRole.Teacher, ct);
    private static Task<bool> IsActiveTeacher(CourseManagementDbContext db, Guid workspaceId, Guid userId, CancellationToken ct) =>
        db.Memberships.AnyAsync(member => member.WorkspaceId == workspaceId && member.UserId == userId
            && member.IsActive && member.Role == WorkspaceRole.Teacher, ct);
    private static bool IsSerializationFailure(Exception exception) => exception is PostgresException
        { SqlState: PostgresErrorCodes.SerializationFailure }
        || exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } };
    private static bool FitsTeacherAvailability((DateTimeOffset StartsAt, DateTimeOffset EndsAt) occurrence,
        IReadOnlyList<TeacherAvailabilityWindow> windows)
    {
        if (windows.Count == 0) return true;
        foreach (var window in windows)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(window.TimeZoneId);
            var localStart = TimeZoneInfo.ConvertTime(occurrence.StartsAt, timeZone);
            var localEnd = TimeZoneInfo.ConvertTime(occurrence.EndsAt, timeZone);
            if (DateOnly.FromDateTime(localStart.DateTime) == DateOnly.FromDateTime(localEnd.DateTime)
                && ((int)localStart.DayOfWeek + 6) % 7 == window.Weekday
                && TimeOnly.FromDateTime(localStart.DateTime) >= window.StartTime
                && TimeOnly.FromDateTime(localEnd.DateTime) <= window.EndTime)
                return true;
        }
        return false;
    }
    private static async Task GrantStaffCancellationCredits(CourseManagementDbContext db, Guid workspaceId,
        Guid lessonId, Guid actorId, DateTimeOffset now, CancellationToken ct)
    {
        var studentIds = await (from participant in db.LessonParticipants.AsNoTracking()
            join student in db.Students.AsNoTracking() on participant.StudentId equals student.Id
            where participant.WorkspaceId == workspaceId && participant.LessonSessionId == lessonId
                && participant.IsActive && student.WorkspaceId == workspaceId && student.IsActive
            select student.Id).Distinct().ToListAsync(ct);
        if (studentIds.Count == 0) return;
        var existingStudentIds = await db.MakeupCredits.Where(credit => credit.WorkspaceId == workspaceId
                && credit.SourceLessonId == lessonId && studentIds.Contains(credit.StudentId))
            .Select(credit => credit.StudentId).ToListAsync(ct);
        foreach (var studentId in studentIds.Except(existingStudentIds))
        {
            var credit = MakeupCredit.Create(Guid.NewGuid(), workspaceId, studentId, lessonId,
                actorId, MakeupCreditReason.StaffCancellation, now);
            db.MakeupCredits.Add(credit);
            AddAudit(db, workspaceId, actorId, credit.Id, "MakeupCredit.Granted");
        }
    }
    private static async Task<bool> CanManageStudent(CourseManagementDbContext db, Guid workspaceId, Guid studentId, Guid userId, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(student => student.Id == studentId && student.WorkspaceId == workspaceId, ct)) return false;
        return await IsOwner(db, workspaceId, userId, ct)
        || await db.Enrollments.AnyAsync(enrollment => enrollment.WorkspaceId == workspaceId && enrollment.StudentId == studentId
            && enrollment.Status == EnrollmentStatus.Active
            && db.Groups.Any(group => group.Id == enrollment.GroupId && group.WorkspaceId == workspaceId && group.TeacherUserId == userId)
            && db.Memberships.Any(member => member.WorkspaceId == workspaceId && member.UserId == userId
                && member.IsActive && member.Role == WorkspaceRole.Teacher), ct);
    }
    private static string? NormalizeLegacyStudentImport(LegacyStudentImportRequest request, out NormalizedLegacyStudentImport? import)
    {
        import = null;
        var sourceSystem = request.SourceSystem?.Trim().ToLowerInvariant();
        if (sourceSystem is not ("coaching" or "speed-reading")) return "LegacyStudentImport.UnsupportedSource";

        var selectedFields = request.SelectedFields?.Select(field => field?.Trim() ?? string.Empty).ToArray();
        if (selectedFields is null || selectedFields.Length is < 1 or > 3
            || selectedFields.Any(field => field is not ("displayName" or "email" or "isActive"))
            || selectedFields.Distinct(StringComparer.Ordinal).Count() != selectedFields.Length
            || !selectedFields.Contains("displayName", StringComparer.Ordinal))
            return "LegacyStudentImport.InvalidFields";

        if (request.Rows is null || request.Rows.Count is < 1 or > 500) return "LegacyStudentImport.InvalidRowCount";
        var includeEmail = selectedFields.Contains("email", StringComparer.Ordinal);
        var includeIsActive = selectedFields.Contains("isActive", StringComparer.Ordinal);
        var normalizedRows = request.Rows.Select(row =>
        {
            var displayName = row.DisplayName?.Trim() ?? string.Empty;
            var email = includeEmail && !string.IsNullOrWhiteSpace(row.Email) ? row.Email.Trim().ToLowerInvariant() : null;
            string? error = row.SourceRecordId == Guid.Empty ? "Kaynak kayıt kimliği eksik."
                : displayName.Length is < 1 or > 200 ? "Ad soyad zorunludur ve 200 karakteri aşamaz."
                : email?.Length > 320 ? "E-posta 320 karakteri aşamaz."
                : null;
            return new NormalizedLegacyStudentImportRow(row.SourceRecordId, displayName, email,
                includeIsActive ? row.IsActive : true, error);
        }).ToArray();

        import = new NormalizedLegacyStudentImport(sourceSystem,
            selectedFields.OrderBy(field => field, StringComparer.Ordinal).ToArray(), normalizedRows);
        return null;
    }

    private static async Task<LegacyStudentImportPreviewResponse> BuildLegacyStudentImportPreview(Guid workspaceId,
        NormalizedLegacyStudentImport import, CourseManagementDbContext db, CancellationToken ct)
    {
        var previewContent = JsonSerializer.Serialize(new
        {
            workspaceId,
            import.SourceSystem,
            import.SelectedFields,
            Rows = import.Rows.Select(row => new { row.SourceRecordId, row.DisplayName, row.Email, row.IsActive })
        });
        var previewId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previewContent))).ToLowerInvariant();
        var sourceRecordIds = import.Rows.Where(row => row.SourceRecordId != Guid.Empty)
            .Select(row => row.SourceRecordId).Distinct().ToList();
        var existingMappings = sourceRecordIds.Count == 0
            ? new Dictionary<Guid, LegacyStudentImportMapping>()
            : await db.LegacyStudentImports.AsNoTracking()
                .Where(mapping => mapping.WorkspaceId == workspaceId && mapping.SourceSystem == import.SourceSystem
                    && sourceRecordIds.Contains(mapping.SourceRecordId))
                .ToDictionaryAsync(mapping => mapping.SourceRecordId, ct);
        var importedEmails = import.Rows.Where(row => row.Error is null && row.Email is not null)
            .Select(row => row.Email!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingEmails = importedEmails.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await db.Students.AsNoTracking().Where(student => student.WorkspaceId == workspaceId
                    && student.Email != null && importedEmails.Contains(student.Email))
                .Select(student => student.Email!).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seenSourceIds = new HashSet<Guid>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<LegacyStudentImportPreviewRow>(import.Rows.Count);
        for (var index = 0; index < import.Rows.Count; index++)
        {
            var row = import.Rows[index];
            if (row.Error is not null)
            {
                rows.Add(new LegacyStudentImportPreviewRow(index + 1, "Invalid", row.DisplayName, row.Email, row.IsActive, row.Error));
                continue;
            }
            if (!seenSourceIds.Add(row.SourceRecordId))
            {
                rows.Add(new LegacyStudentImportPreviewRow(index + 1, "Duplicate", row.DisplayName, row.Email, row.IsActive,
                    "Kaynak kayıt aynı aktarımda birden fazla kez seçilmiş."));
                continue;
            }
            if (existingMappings.ContainsKey(row.SourceRecordId))
            {
                rows.Add(new LegacyStudentImportPreviewRow(index + 1, "Imported", row.DisplayName, row.Email, row.IsActive, null));
                continue;
            }
            if (row.Email is not null && (existingEmails.Contains(row.Email) || !seenEmails.Add(row.Email)))
            {
                rows.Add(new LegacyStudentImportPreviewRow(index + 1, "Conflict", row.DisplayName, row.Email, row.IsActive,
                    "E-posta çalışma alanındaki başka bir kayıtla çakışıyor; otomatik eşleştirme yapılmadı."));
                continue;
            }
            rows.Add(new LegacyStudentImportPreviewRow(index + 1, "Ready", row.DisplayName, row.Email, row.IsActive, null));
        }

        return new LegacyStudentImportPreviewResponse(previewId, import.SourceSystem, import.SelectedFields,
            rows.Count(row => row.Status == "Ready"), rows.Count(row => row.Status == "Imported"),
            rows.Count(row => row.Status == "Invalid"), rows.Count(row => row.Status == "Conflict"),
            rows.Count(row => row.Status == "Duplicate"), rows);
    }

    private static async Task<StudentCsvImportPreviewResponse> BuildStudentCsvPreview(Guid workspaceId, string csv,
        StudentCsvImportDocument document, CourseManagementDbContext db, CancellationToken ct)
    {
        var previewId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{workspaceId:N}\n{csv}"))).ToLowerInvariant();
        var validRows = document.Rows.Where(row => row.IsValid).ToArray();
        var rowIds = validRows.Select(row => ImportedStudentId(previewId, row.RowNumber)).ToList();
        var existingImports = rowIds.Count == 0 ? new Dictionary<Guid, StudentRecord>()
            : await db.Students.AsNoTracking().Where(student => rowIds.Contains(student.Id)).ToDictionaryAsync(student => student.Id, ct);
        var emails = validRows.Where(row => row.Email is not null).Select(row => row.Email!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingEmails = emails.Count == 0 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await db.Students.AsNoTracking().Where(student => student.WorkspaceId == workspaceId && student.Email != null
                    && emails.Contains(student.Email))
                .Select(student => student.Email!).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<StudentCsvImportPreviewRow>(document.Rows.Count);

        foreach (var row in document.Rows)
        {
            if (!row.IsValid)
            {
                rows.Add(new StudentCsvImportPreviewRow(row.RowNumber, "Invalid", row.DisplayName, row.Email, row.PhoneNumber, row.Error));
                continue;
            }

            var studentId = ImportedStudentId(previewId, row.RowNumber);
            if (existingImports.TryGetValue(studentId, out var imported))
            {
                var matches = imported.WorkspaceId == workspaceId
                    && imported.DisplayName == row.DisplayName
                    && string.Equals(imported.Email, row.Email, StringComparison.OrdinalIgnoreCase)
                    && imported.PhoneNumber == row.PhoneNumber;
                rows.Add(new StudentCsvImportPreviewRow(row.RowNumber, matches ? "Imported" : "Duplicate", row.DisplayName,
                    row.Email, row.PhoneNumber, matches ? null : "İçe aktarım kimliği mevcut başka bir kayıtla çakışıyor."));
                continue;
            }

            if (row.Email is not null && existingEmails.Contains(row.Email))
            {
                rows.Add(new StudentCsvImportPreviewRow(row.RowNumber, "Duplicate", row.DisplayName, row.Email, row.PhoneNumber,
                    "Bu e-posta çalışma alanında zaten kayıtlı."));
                continue;
            }

            var duplicateKey = row.Email is not null ? $"email:{row.Email}"
                : $"student:{row.DisplayName.ToUpperInvariant()}\u001f{row.PhoneNumber?.ToUpperInvariant()}";
            if (seenRows.TryGetValue(duplicateKey, out var duplicateOf))
            {
                rows.Add(new StudentCsvImportPreviewRow(row.RowNumber, "Duplicate", row.DisplayName, row.Email, row.PhoneNumber,
                    $"Bu satır {duplicateOf}. satırla yineleniyor."));
                continue;
            }

            seenRows.Add(duplicateKey, row.RowNumber);
            rows.Add(new StudentCsvImportPreviewRow(row.RowNumber, "Ready", row.DisplayName, row.Email, row.PhoneNumber, null));
        }

        return new StudentCsvImportPreviewResponse(previewId,
            rows.Count(row => row.Status == "Ready"), rows.Count(row => row.Status == "Imported"),
            rows.Count(row => row.Status == "Invalid"), rows.Count(row => row.Status == "Duplicate"), rows);
    }
    private static Guid ImportedStudentId(string previewId, int rowNumber) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{previewId}\n{rowNumber}")).AsSpan(0, 16));
    private static Task<Guid[]> GetLinkedStudentUserIds(CourseManagementDbContext db, Guid workspaceId,
        IReadOnlyCollection<Guid> studentIds, CancellationToken ct) => db.Students.AsNoTracking()
        .Where(student => student.WorkspaceId == workspaceId && student.IsActive && student.UserId != null
            && studentIds.Contains(student.Id))
        .Select(student => student.UserId!.Value).Distinct().ToArrayAsync(ct);
    private static object StudentView(StudentRecord student) => new { student.Id, student.UserId, student.DisplayName, student.Email, student.PhoneNumber, student.IsActive };
    private static void AddAudit(CourseManagementDbContext db, Guid workspaceId, Guid actorId, Guid targetId, string action) =>
        db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, actorId, targetId, action));

    private sealed record NormalizedLegacyStudentImport(string SourceSystem, IReadOnlyList<string> SelectedFields,
        IReadOnlyList<NormalizedLegacyStudentImportRow> Rows);
    private sealed record NormalizedLegacyStudentImportRow(Guid SourceRecordId, string DisplayName, string? Email,
        bool IsActive, string? Error);
}

public sealed record CreateStudentRequest(Guid Id, string DisplayName, string? Email, string? PhoneNumber);
public sealed record PreviewStudentCsvRequest(string? Csv);
public sealed record CommitStudentCsvRequest(string? PreviewId, string? Csv);
public sealed record StudentCsvImportPreviewRow(int RowNumber, string Status, string DisplayName, string? Email, string? PhoneNumber, string? Error);
public sealed record StudentCsvImportPreviewResponse(string PreviewId, int ReadyCount, int ImportedCount, int InvalidCount,
    int DuplicateCount, IReadOnlyList<StudentCsvImportPreviewRow> Rows);
public sealed record StudentCsvImportCommitResponse(string PreviewId, int CreatedCount, int AlreadyImportedCount);
public sealed record LegacyStudentImportRequest(string? PreviewId, string? SourceSystem, IReadOnlyList<string>? SelectedFields,
    IReadOnlyList<LegacyStudentImportInputRow>? Rows);
public sealed record LegacyStudentImportInputRow(Guid SourceRecordId, string? DisplayName, string? Email, bool IsActive);
public sealed record LegacyStudentImportPreviewRow(int RowNumber, string Status, string DisplayName, string? Email,
    bool IsActive, string? Error);
public sealed record LegacyStudentImportPreviewResponse(string PreviewId, string SourceSystem, IReadOnlyList<string> SelectedFields,
    int ReadyCount, int ImportedCount, int InvalidCount, int ConflictCount, int DuplicateCount,
    IReadOnlyList<LegacyStudentImportPreviewRow> Rows);
public sealed record LegacyStudentImportCommitResponse(string PreviewId, int CreatedCount, int AlreadyImportedCount);
public sealed record UpdateStudentRequest(string DisplayName, string? Email, string? PhoneNumber, bool IsActive);
public sealed record UpdateGuardianContactRequest(string DisplayName, string? Email, string? PhoneNumber);
public sealed record LinkGuardianRequest(Guid GuardianId, string? DisplayName, string? Email, string? PhoneNumber, GuardianRelationship Relationship);
public sealed record CreateCourseRequest(Guid Id, string Name, string Subject);
public sealed record CreateGroupRequest(Guid Id, Guid CourseId, Guid TeacherUserId, string Name);
public sealed record CreateBranchRequest(Guid Id, string Name);
public sealed record CreateRoomRequest(Guid Id, string Name, int? Capacity);
public sealed record CreateLessonSessionRequest(Guid Id, Guid CourseId, Guid? GroupId, Guid? StudentId, Guid TeacherUserId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid? RoomId = null, Guid? MakeupCreditId = null);
public sealed record RescheduleLessonRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid? RoomId);
public sealed record CreateLessonSeriesRequest(Guid Id, Guid CourseId, Guid? GroupId, Guid? StudentId, Guid TeacherUserId,
    string TimeZoneId, string StartsOn, string EndsOn, string StartTime, int DurationMinutes, int DaysOfWeekMask, Guid? RoomId = null);
public sealed record UpdateLessonSeriesScheduleRequest(string EndsOn, string StartTime, int DurationMinutes,
    int DaysOfWeekMask, Guid? RoomId);
public sealed record CreateTeacherTimeOffRequest(Guid Id, Guid TeacherUserId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason);
public sealed record CreateTeacherAvailabilityWindowRequest(Guid Id, Guid TeacherUserId, int Weekday,
    string StartTime, string EndTime, string TimeZoneId);
public sealed record UpdateLessonAttendanceRequest(int Status);
public sealed record LessonStudentView(Guid StudentId, string DisplayName, string? AttendanceStatus, bool IsActive);
public sealed record StudentLessonCancellationResponse(Guid LessonId, bool EligibleForCredit, Guid? CreditId, string? CreditStatus);
public sealed record StudentAttendanceView(Guid Id, Guid LessonId, string WorkspaceName, string CourseName,
    string Subject, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, DateTimeOffset MarkedAt);
