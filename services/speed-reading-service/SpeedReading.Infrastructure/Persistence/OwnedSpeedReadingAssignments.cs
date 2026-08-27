using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Assignments;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Assignment use cases backed only by the owned Speed Reading store.
/// Identity remains the source of truth for user existence and profile data;
/// this class stores and authorizes only stable user identifiers.
/// </summary>
internal sealed class OwnedSpeedReadingAssignments(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingUserDirectory userDirectory) : ISpeedReadingAssignments
{
    public async Task<Guid?> CreateAsync(
        Guid teacherId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (teacherId == Guid.Empty
            || request.ExerciseId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.Title))
        {
            return null;
        }

        var exerciseExists = await db.Exercises.AnyAsync(
            item => item.Id == request.ExerciseId && item.IsActive,
            cancellationToken);
        if (!exerciseExists)
            return null;

        if (request.ReadingTextId.HasValue && !await db.ReadingTexts.AnyAsync(
                item => item.Id == request.ReadingTextId.Value
                    && item.IsActive
                    && (item.ExerciseId == null || item.ExerciseId == request.ExerciseId),
                cancellationToken))
        {
            return null;
        }

        var studentIds = SpeedReadingAssignmentRules.NormalizeStudentIds(request.StudentIds);
        if (studentIds.Count > 500)
            return null;
        if (studentIds.Count > 0)
        {
            var users = await userDirectory.GetUsersAsync(studentIds, cancellationToken);
            if (users.Users.Count != studentIds.Count || users.Users.Any(item => !item.IsActive))
                return null;
        }

        var now = DateTime.UtcNow;
        var assignment = Assignment.Create(
            teacherId,
            request.ExerciseId,
            request.ReadingTextId,
            request.Title,
            request.Description,
            SpeedReadingAssignmentRules.NormalizeDueDate(request.DueDate),
            createdAt: now,
            createdBy: teacherId.ToString());
        db.Assignments.Add(assignment);

        foreach (var studentId in studentIds)
        {
            db.StudentAssignments.Add(StudentAssignment.Assign(
                assignment.Id,
                studentId,
                assignedAt: now,
                createdBy: teacherId.ToString()));
        }

        await db.SaveChangesAsync(cancellationToken);
        return assignment.Id;
    }

    public async Task<IReadOnlyList<StudentAssignmentSummary>> GetMyAssignmentsAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from studentAssignment in db.StudentAssignments.AsNoTracking()
            join assignment in db.Assignments.AsNoTracking()
                on studentAssignment.AssignmentId equals assignment.Id
            join exercise in db.Exercises.AsNoTracking()
                on assignment.ExerciseId equals exercise.Id into exerciseRows
            from exercise in exerciseRows.DefaultIfEmpty()
            where studentAssignment.StudentId == studentId
                && studentAssignment.IsActive
                && assignment.IsActive
            orderby assignment.CreatedAt descending
            select new StudentAssignmentSummary(
                studentAssignment.Id,
                assignment.Id,
                assignment.Title,
                assignment.Description,
                assignment.DueDate,
                studentAssignment.IsCompleted,
                studentAssignment.CompletionDate,
                assignment.ExerciseId,
                exercise == null ? assignment.Title : exercise.Title,
                studentAssignment.Score))
            .ToListAsync(cancellationToken);
    }

    public async Task<SpeedReadingPage<AssignmentSummary>> GetTeacherAssignmentsAsync(
        Guid teacherId,
        int pageNumber,
        int pageSize,
        string? searchTerm,
        bool? isActive,
        Guid? exerciseTypeId,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query =
            from assignment in db.Assignments.AsNoTracking()
            join exercise in db.Exercises.AsNoTracking()
                on assignment.ExerciseId equals exercise.Id into exerciseRows
            from exercise in exerciseRows.DefaultIfEmpty()
            join exerciseType in db.ExerciseTypes.AsNoTracking()
                on exercise.ExerciseTypeId equals exerciseType.Id into typeRows
            from exerciseType in typeRows.DefaultIfEmpty()
            join readingText in db.ReadingTexts.AsNoTracking()
                on assignment.ReadingTextId equals readingText.Id into readingTextRows
            from readingText in readingTextRows.DefaultIfEmpty()
            where assignment.TeacherId == teacherId
            select new { assignment, exercise, exerciseType, readingText };

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(row => row.assignment.Title.ToLower().Contains(normalizedSearch));
        }

        if (isActive.HasValue)
            query = query.Where(row => row.assignment.IsActive == isActive.Value);

        if (exerciseTypeId.HasValue)
        {
            query = query.Where(row => row.exercise != null
                && row.exercise.ExerciseTypeId == exerciseTypeId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(row => row.assignment.CreatedAt)
            .ThenByDescending(row => row.assignment.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(row => new AssignmentSummary(
                row.assignment.Id,
                row.assignment.TeacherId,
                row.assignment.ExerciseId,
                row.assignment.ReadingTextId,
                row.assignment.Title,
                row.assignment.Description,
                row.exercise == null ? null : row.exercise.Title,
                row.exerciseType == null ? null : row.exerciseType.DisplayName,
                row.readingText == null ? null : row.readingText.Title,
                row.assignment.DueDate,
                row.assignment.IsActive,
                row.assignment.CreatedAt,
                db.StudentAssignments.Count(item => item.AssignmentId == row.assignment.Id && item.IsActive),
                db.StudentAssignments.Count(item => item.AssignmentId == row.assignment.Id
                    && item.IsActive
                    && item.IsCompleted)))
            .ToListAsync(cancellationToken);

        return new SpeedReadingPage<AssignmentSummary>(items, page, size, totalCount);
    }

    public async Task<AssignmentDetails?> GetDetailsAsync(
        Guid teacherId,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var row = await (
            from assignment in db.Assignments.AsNoTracking()
            join exercise in db.Exercises.AsNoTracking()
                on assignment.ExerciseId equals exercise.Id into exerciseRows
            from exercise in exerciseRows.DefaultIfEmpty()
            where assignment.Id == assignmentId && assignment.TeacherId == teacherId
            select new { assignment, exercise })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
            return null;

        var studentRows = await db.StudentAssignments
            .AsNoTracking()
            .Where(item => item.AssignmentId == assignmentId && item.IsActive)
            .OrderBy(item => item.StudentId)
            .ToListAsync(cancellationToken);
        var users = await userDirectory.GetUsersAsync(
            studentRows.Select(item => item.StudentId).ToArray(),
            cancellationToken);
        var userById = users.Users.ToDictionary(item => item.UserId);
        var students = studentRows
            .Select(item =>
            {
                userById.TryGetValue(item.StudentId, out var user);
                var fullName = user is null
                    ? string.Empty
                    : $"{user.FirstName} {user.LastName}".Trim();
                return new AssignmentStudentSummary(
                    item.StudentId,
                    user?.FirstName ?? string.Empty,
                    user?.LastName ?? string.Empty,
                    fullName,
                    item.IsCompleted,
                    item.CompletionDate,
                    item.Score);
            })
            .ToList();

        return new AssignmentDetails(
            row.assignment.Id,
            row.assignment.TeacherId,
            row.assignment.ExerciseId,
            row.assignment.ReadingTextId,
            row.assignment.Title,
            row.assignment.Description,
            row.exercise == null ? null : row.exercise.Title,
            row.assignment.DueDate,
            row.assignment.IsActive,
            row.assignment.CreatedAt,
            students);
    }

    public async Task<bool> DeleteAsync(
        Guid teacherId,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await db.Assignments.SingleOrDefaultAsync(
            item => item.Id == assignmentId
                && item.TeacherId == teacherId
                && item.IsActive,
            cancellationToken);
        if (assignment is null)
            return false;

        assignment.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AssignmentStudentMutationStatus> AddStudentAsync(
        Guid teacherId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (studentId == Guid.Empty)
            return AssignmentStudentMutationStatus.StudentNotFound;

        var assignmentExists = await db.Assignments.AnyAsync(
            item => item.Id == assignmentId && item.TeacherId == teacherId && item.IsActive,
            cancellationToken);
        if (!assignmentExists)
            return AssignmentStudentMutationStatus.AssignmentNotFound;

        var users = await userDirectory.GetUsersAsync([studentId], cancellationToken);
        if (users.Users.Count != 1 || !users.Users[0].IsActive)
            return AssignmentStudentMutationStatus.StudentNotFound;

        var alreadyAssigned = await db.StudentAssignments.AnyAsync(
            item => item.AssignmentId == assignmentId && item.StudentId == studentId && item.IsActive,
            cancellationToken);
        if (alreadyAssigned)
            return AssignmentStudentMutationStatus.AlreadyAssigned;

        db.StudentAssignments.Add(StudentAssignment.Assign(
            assignmentId,
            studentId,
            createdBy: teacherId.ToString()));
        await db.SaveChangesAsync(cancellationToken);
        return AssignmentStudentMutationStatus.Success;
    }

    public async Task<AssignmentStudentMutationStatus> RemoveStudentAsync(
        Guid teacherId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var assignmentExists = await db.Assignments.AnyAsync(
            item => item.Id == assignmentId && item.TeacherId == teacherId && item.IsActive,
            cancellationToken);
        if (!assignmentExists)
            return AssignmentStudentMutationStatus.AssignmentNotFound;

        var studentAssignment = await db.StudentAssignments.SingleOrDefaultAsync(
            item => item.AssignmentId == assignmentId
                && item.StudentId == studentId
                && item.IsActive,
            cancellationToken);
        if (studentAssignment is null)
            return AssignmentStudentMutationStatus.StudentAssignmentNotFound;

        studentAssignment.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
        return AssignmentStudentMutationStatus.Success;
    }

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));
}
