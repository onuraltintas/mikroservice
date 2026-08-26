using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingAssignments(SpeedReadingDbContext db) : ISpeedReadingAssignments
{
    public async Task<Guid?> CreateAsync(
        Guid teacherId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ExerciseId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title))
        {
            return null;
        }

        if (!await db.Exercises.AnyAsync(
                item => item.Id == request.ExerciseId && !item.IsDeleted,
                cancellationToken))
        {
            return null;
        }

        if (request.ReadingTextId.HasValue
            && !await db.ReadingTexts.AnyAsync(
                item => item.Id == request.ReadingTextId.Value && !item.IsDeleted,
                cancellationToken))
        {
            return null;
        }

        var studentIds = SpeedReadingAssignmentRules.NormalizeStudentIds(request.StudentIds);
        if (studentIds.Count > 0)
        {
            var studentCount = await db.Users
                .CountAsync(item => studentIds.Contains(item.Id) && !item.IsDeleted, cancellationToken);
            if (studentCount != studentIds.Count)
            {
                return null;
            }
        }

        var now = DateTime.UtcNow;
        var assignment = new LegacyAssignment
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            ExerciseId = request.ExerciseId,
            ReadingTextId = request.ReadingTextId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            DueDate = SpeedReadingAssignmentRules.NormalizeDueDate(request.DueDate),
            IsActive = true,
            CreatedAt = now,
            CreatedBy = teacherId
        };
        db.Assignments.Add(assignment);

        foreach (var studentId in studentIds)
        {
            db.StudentAssignments.Add(new LegacyStudentAssignment
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignment.Id,
                StudentId = studentId,
                IsCompleted = false,
                CreatedAt = now,
                CreatedBy = teacherId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return assignment.Id;
    }

    public async Task<IReadOnlyList<StudentAssignmentSummary>> GetMyAssignmentsAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from studentAssignment in db.StudentAssignments.AsNoTracking()
            join assignment in db.Assignments.AsNoTracking()
                on studentAssignment.AssignmentId equals assignment.Id
            join exercise in db.Exercises.AsNoTracking()
                on assignment.ExerciseId equals exercise.Id into exerciseRows
            from exercise in exerciseRows.DefaultIfEmpty()
            where studentAssignment.StudentId == studentId
                && !studentAssignment.IsDeleted
                && !assignment.IsDeleted
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

        return rows;
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
            where assignment.TeacherId == teacherId && !assignment.IsDeleted
            select new { assignment, exercise, exerciseType, readingText };

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(row => row.assignment.Title.ToLower().Contains(normalizedSearch));
        }

        if (isActive.HasValue)
        {
            query = query.Where(row => row.assignment.IsActive == isActive.Value);
        }

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
                db.StudentAssignments.Count(item => item.AssignmentId == row.assignment.Id && !item.IsDeleted),
                db.StudentAssignments.Count(item => item.AssignmentId == row.assignment.Id && !item.IsDeleted && item.IsCompleted)))
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
            where assignment.Id == assignmentId
                && assignment.TeacherId == teacherId
                && !assignment.IsDeleted
            select new { assignment, exercise })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var students = await (
            from studentAssignment in db.StudentAssignments.AsNoTracking()
            join user in db.Users.AsNoTracking()
                on studentAssignment.StudentId equals user.Id
            where studentAssignment.AssignmentId == assignmentId
                && !studentAssignment.IsDeleted
                && !user.IsDeleted
            orderby user.FirstName, user.LastName
            select new AssignmentStudentSummary(
                user.Id,
                user.FirstName,
                user.LastName,
                (user.FirstName + " " + user.LastName).Trim(),
                studentAssignment.IsCompleted,
                studentAssignment.CompletionDate,
                studentAssignment.Score))
            .ToListAsync(cancellationToken);

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
                && !item.IsDeleted,
            cancellationToken);
        if (assignment is null)
        {
            return false;
        }

        assignment.IsDeleted = true;
        assignment.DeletedAt = DateTime.UtcNow;
        assignment.DeletedBy = teacherId;
        assignment.UpdatedAt = assignment.DeletedAt;
        assignment.UpdatedBy = teacherId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AssignmentStudentMutationStatus> AddStudentAsync(
        Guid teacherId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Assignments.AnyAsync(
                item => item.Id == assignmentId && item.TeacherId == teacherId && !item.IsDeleted,
                cancellationToken))
        {
            return AssignmentStudentMutationStatus.AssignmentNotFound;
        }

        if (!await db.Users.AnyAsync(item => item.Id == studentId && !item.IsDeleted, cancellationToken))
        {
            return AssignmentStudentMutationStatus.StudentNotFound;
        }

        if (await db.StudentAssignments.AnyAsync(
                item => item.AssignmentId == assignmentId
                    && item.StudentId == studentId
                    && !item.IsDeleted,
                cancellationToken))
        {
            return AssignmentStudentMutationStatus.AlreadyAssigned;
        }

        db.StudentAssignments.Add(new LegacyStudentAssignment
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = teacherId
        });
        await db.SaveChangesAsync(cancellationToken);
        return AssignmentStudentMutationStatus.Success;
    }

    public async Task<AssignmentStudentMutationStatus> RemoveStudentAsync(
        Guid teacherId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Assignments.AnyAsync(
                item => item.Id == assignmentId && item.TeacherId == teacherId && !item.IsDeleted,
                cancellationToken))
        {
            return AssignmentStudentMutationStatus.AssignmentNotFound;
        }

        var studentAssignment = await db.StudentAssignments.SingleOrDefaultAsync(
            item => item.AssignmentId == assignmentId
                && item.StudentId == studentId
                && !item.IsDeleted,
            cancellationToken);
        if (studentAssignment is null)
        {
            return AssignmentStudentMutationStatus.StudentAssignmentNotFound;
        }

        studentAssignment.IsDeleted = true;
        studentAssignment.DeletedAt = DateTime.UtcNow;
        studentAssignment.DeletedBy = teacherId;
        studentAssignment.UpdatedAt = studentAssignment.DeletedAt;
        studentAssignment.UpdatedBy = teacherId;
        await db.SaveChangesAsync(cancellationToken);
        return AssignmentStudentMutationStatus.Success;
    }

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));
}
