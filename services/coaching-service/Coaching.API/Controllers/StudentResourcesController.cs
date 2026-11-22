using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

/// <summary>KocApp uyumu (22.11.2026): the student's own books and sets ("Kaynaklarım").</summary>
[ApiController, ApiVersion(1.0), Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/resources")]
public sealed class StudentResourcesController(CoachingStudentResourceService resources) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(bool includeArchived = false, CancellationToken ct = default) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.ListAsync(resources.RequireStudent(), includeArchived, ct) }));

    [HttpPost, RequestSizeLimit(65536), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Create(StudentResourceInput request, CancellationToken ct) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.CreateAsync(resources.RequireStudent(), request, ct) }));

    [HttpPut("{id:guid}"), RequestSizeLimit(65536), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Update(Guid id, StudentResourceInput request, CancellationToken ct) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.UpdateAsync(resources.RequireStudent(), id, request, ct) }));

    [HttpPut("{id:guid}/archive"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Archive(Guid id, StudentResourceArchiveInput request, CancellationToken ct) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.SetArchivedAsync(resources.RequireStudent(), id, request, ct) }));

    [HttpDelete("{id:guid}"), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Delete(Guid id, int expectedVersion, CancellationToken ct) => StudentResourceResponses.Run(async () =>
    {
        await resources.DeleteAsync(resources.RequireStudent(), id, expectedVersion, ct);
        return NoContent();
    });
}

/// <summary>KocApp uyumu (22.11.2026): a student's resources for her teacher (read and write) and her institution (read).</summary>
[ApiController, ApiVersion(1.0), Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
[Route("api/coaching/staff/students/{studentId:guid}/study/resources")]
public sealed class CoachingStaffStudentResourcesController(CoachingStudentResourceService resources) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(Guid studentId, bool includeArchived = false, CancellationToken ct = default) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.ListAsync(await resources.RequireStaffReadAsync(studentId, ct), includeArchived, ct) }));

    [HttpPost, Authorize(Roles = "Teacher"), RequestSizeLimit(65536), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Create(Guid studentId, StudentResourceInput request, CancellationToken ct) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.CreateAsync(await resources.RequireTeacherWriteAsync(studentId, ct), request, ct) }));

    [HttpPut("{id:guid}"), Authorize(Roles = "Teacher"), RequestSizeLimit(65536), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Update(Guid studentId, Guid id, StudentResourceInput request, CancellationToken ct) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.UpdateAsync(await resources.RequireTeacherWriteAsync(studentId, ct), id, request, ct) }));

    [HttpPut("{id:guid}/archive"), Authorize(Roles = "Teacher"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Archive(Guid studentId, Guid id, StudentResourceArchiveInput request, CancellationToken ct) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.SetArchivedAsync(await resources.RequireTeacherWriteAsync(studentId, ct), id, request, ct) }));

    [HttpDelete("{id:guid}"), Authorize(Roles = "Teacher"), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Delete(Guid studentId, Guid id, int expectedVersion, CancellationToken ct) => StudentResourceResponses.Run(async () =>
    {
        await resources.DeleteAsync(await resources.RequireTeacherWriteAsync(studentId, ct), id, expectedVersion, ct);
        return NoContent();
    });

    /// <summary>KocApp plan, phase 2: the same book for other students of the teacher.</summary>
    [HttpPost("{id:guid}/copy"), Authorize(Roles = "Teacher"), RequestSizeLimit(8192), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Copy(Guid studentId, Guid id, StudentResourceCopyInput request, CancellationToken ct) => StudentResourceResponses.Run(async () =>
        Ok(new { success = true, data = await resources.CopyAsync(await resources.RequireTeacherWriteAsync(studentId, ct), id, request, ct) }));
}

/// <summary>KocApp uyumu (22.11.2026): a linked parent reads the child's resources and their progress (not the archived ones).</summary>
[ApiController, ApiVersion(1.0), Authorize(Roles = "Parent")]
[Route("api/coaching/parents/me/children/{studentId:guid}/resources")]
public sealed class CoachingParentChildResourcesController(CoachingStudentResourceService resources, CoachingParentChildService parents) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(Guid studentId, CancellationToken ct = default) => StudentResourceResponses.Run(async () =>
    {
        var child = await parents.RequireAsync(studentId, ct);
        return Ok(new { success = true, data = await resources.ListAsync(child.StudentId, false, ct) });
    });
}

internal static class StudentResourceResponses
{
    public static async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return new ObjectResult(new { success = false, message = ex.Message, code = ex.Code }) { StatusCode = 403 }; }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.NotFound")
        { return new NotFoundObjectResult(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return new ConflictObjectResult(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (ConcurrencyException)
        { return new ConflictObjectResult(new { success = false, message = "Kaynak bu arada değişti. Sayfa yenilenip yeniden denenmeli." }); }
        // The resource's own checks are written for the reader ("Toplam 1 ile 10.000 arasında olmalı.").
        catch (ArgumentException ex)
        { return new BadRequestObjectResult(new { success = false, message = ex.Message }); }
    }
}
