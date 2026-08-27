using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Assignments;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingAssignmentBackfillResult(
    int AssignmentsInserted,
    int StudentAssignmentsInserted,
    int ExistingRows,
    DateTime CompletedAtUtc);

/// <summary>
/// Copies assignment ownership and student membership after the catalog has
/// been backfilled. Identity user records remain owned by Identity; only
/// their stable identifiers are kept here.
/// </summary>
public sealed class OwnedSpeedReadingAssignmentBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSpeedReadingAssignmentBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);

        var assignments = await legacy.Assignments
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var studentAssignments = await legacy.StudentAssignments
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var resultIds = await legacy.StudentExerciseResults
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);

        var exerciseIds = await owned.Exercises
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var readingTextIds = await owned.ReadingTexts
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        ValidateReferences(assignments, studentAssignments, exerciseIds, readingTextIds, resultIds);

        await using var transaction = await owned.Database.BeginTransactionAsync(cancellationToken);
        var existingRows = 0;
        var assignmentsInserted = 0;
        var studentAssignmentsInserted = 0;

        var existingAssignmentIds = await owned.Assignments
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in assignments)
        {
            if (existingAssignmentIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.Assignments.Add(Assignment.Import(
                source.Id,
                source.TeacherId,
                source.ExerciseId,
                source.ReadingTextId,
                source.Title,
                source.Description,
                NormalizeUtc(source.DueDate),
                source.IsActive,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            assignmentsInserted++;
        }

        var existingStudentAssignmentIds = await owned.StudentAssignments
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in studentAssignments)
        {
            if (existingStudentAssignmentIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.StudentAssignments.Add(StudentAssignment.Import(
                source.Id,
                source.AssignmentId,
                source.StudentId,
                source.IsCompleted,
                NormalizeUtc(source.CompletionDate),
                source.ResultId,
                source.Score,
                source.KeyPerformanceMetric,
                isActive: true,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            studentAssignmentsInserted++;
        }

        await owned.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OwnedSpeedReadingAssignmentBackfillResult(
            assignmentsInserted,
            studentAssignmentsInserted,
            existingRows,
            DateTime.UtcNow);
    }

    private static void ValidateReferences(
        IReadOnlyList<LegacyAssignment> assignments,
        IReadOnlyList<LegacyStudentAssignment> studentAssignments,
        IReadOnlySet<Guid> exerciseIds,
        IReadOnlySet<Guid> readingTextIds,
        IReadOnlySet<Guid> resultIds)
    {
        var assignmentIds = assignments.Select(item => item.Id).ToHashSet();
        foreach (var assignment in assignments)
        {
            if (assignment.TeacherId == Guid.Empty)
                throw new InvalidOperationException($"Assignment {assignment.Id} has no teacher.");
            if (!exerciseIds.Contains(assignment.ExerciseId))
                throw new InvalidOperationException(
                    $"Assignment {assignment.Id} references missing exercise {assignment.ExerciseId}.");
            if (assignment.ReadingTextId.HasValue && !readingTextIds.Contains(assignment.ReadingTextId.Value))
                throw new InvalidOperationException(
                    $"Assignment {assignment.Id} references missing reading text {assignment.ReadingTextId}.");
        }

        var duplicate = studentAssignments
            .GroupBy(item => (item.AssignmentId, item.StudentId))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Assignment {duplicate.Key.AssignmentId} has duplicate student {duplicate.Key.StudentId} rows.");
        }

        foreach (var studentAssignment in studentAssignments)
        {
            if (!assignmentIds.Contains(studentAssignment.AssignmentId))
                throw new InvalidOperationException(
                    $"Student assignment {studentAssignment.Id} references missing assignment {studentAssignment.AssignmentId}.");
            if (studentAssignment.StudentId == Guid.Empty)
                throw new InvalidOperationException($"Student assignment {studentAssignment.Id} has no student.");
            if (studentAssignment.ResultId == Guid.Empty)
                throw new InvalidOperationException(
                    $"Student assignment {studentAssignment.Id} contains an empty result reference.");
            if (studentAssignment.ResultId.HasValue && !resultIds.Contains(studentAssignment.ResultId.Value))
                throw new InvalidOperationException(
                    $"Student assignment {studentAssignment.Id} references missing result {studentAssignment.ResultId}.");
        }
    }

    private static string? ToAuditValue(Guid? value) => value?.ToString();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue ? NormalizeUtc(value.Value) : null;
}
