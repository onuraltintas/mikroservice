using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

/// <summary>KocApp uyumu (22.11.2026): a book or set the student works through, as the student or the teacher writes it.</summary>
public sealed record StudentResourceInput(
    [Required, MaxLength(200)] string Name,
    [MaxLength(120)] string? Publisher,
    [MaxLength(100)] string? LessonName,
    [Required, MaxLength(20)] string Kind,
    [Required, MaxLength(20)] string Unit,
    [Range(1, StudentResource.MaxUnits)] int TotalUnits,
    [Range(0, StudentResource.MaxUnits)] int DoneBefore = 0,
    IReadOnlyList<StudentResourceSectionInput>? Sections = null,
    [Range(0, int.MaxValue)] int? ExpectedVersion = null);

public sealed record StudentResourceSectionInput([Required, MaxLength(200)] string Title,
    [Range(1, StudentResource.MaxUnits)] int FromUnit, [Range(1, StudentResource.MaxUnits)] int ToUnit, Guid? TopicId = null);

public sealed record StudentResourceArchiveInput([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion,
    [property: JsonRequired] bool IsArchived);

/// <summary>KocApp plan, phase 2: a teacher gives the same book to other students of hers (at most 50 at once).</summary>
public sealed record StudentResourceCopyInput([Required] IReadOnlyList<Guid> StudentIds);
/// <summary>How many copies were made, and how many students already had a resource of that name (skipped).</summary>
public sealed record StudentResourceCopyResult(int Copied, int Skipped);

public sealed record StudentResourceSectionView(string Title, int FromUnit, int ToUnit, Guid? TopicId, string? TopicName, int DoneUnits);

/// <summary>
/// The next part to plan: the first section (or, without sections, the first units) neither done nor already in a planned task.
/// Phase 2 of the KocApp plan: the plan editor offers it for a new task of the resource's lesson.
/// </summary>
public sealed record StudentResourceNext(int FromUnit, int ToUnit, string? SectionTitle, Guid? TopicId);

public sealed record StudentResourceView(Guid Id, int Version, string Name, string? Publisher, string? LessonName, string Kind,
    string Unit, int TotalUnits, int DoneBefore, int DoneUnits, int Percent, int PlannedUnits, bool IsArchived, bool CanDelete,
    DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<StudentResourceSectionView> Sections, StudentResourceNext? Next);
